/// The shell's persistence (EnginePort.Persistence) over stream persist's program, Causa.Persistence.Sync: the
/// connection and its states, loading, the historical catalog, local durability and restore, and publication
/// through core's request book. This module only translates — Sync messages in, Limen effects out, the program's
/// state into the shell's projection.
///
/// Inputs the shell sends (EnginePort.PersistenceInput, from Adapter.AdapterInput):
///   Boot                  page start, once from Initialize: restores this device's stored work (storage Get).
///                         If the shell never sends it, the first other input triggers the same restore first.
///   Connect request       explicit Connect (token, owner/name, branch, workspace).
///   Disconnect            forget the credential; this device's unsaved work stays.
///   Accepted(...)         a command the kernel accepted: appended, verified, saved automatically when connected.
///   CheckRemote           "Check now": settle a save whose outcome is unknown by re-reading the branch first.
///   DiscardLocal id       conflict resolution: drop this device's change, adopt the repository's copy.
///   ReloadRemote id       re-read the investigation from the repository, keeping this device's change.
///   Answer(corr, result)  effect results whose correlation id starts with "persist-".
/// Output fields beyond the shell's projection:
///   ConnectionState       EnginePort.ConnectionState (render with EnginePort.ConnectionState.label);
///   ConnectionDetail      the plain-language reason for incompatible / accessDenied / offline / conflict.
/// Policies: a pending change is saved automatically when connected; a save whose outcome is unknown is checked once
/// automatically (reconcile, which reads the branch first); a failed save is never retried blindly.
/// Local store: tabs of one browser share one key. Each local write reads the stored record and stores
/// Sync.mergeRecords of it (another tab's unsaved, held, unknown or conflict changes are kept, never replaced).
/// Known limit (review R3-F2): the read and the write are two storage calls, not one atomic step, so a write another
/// tab makes between them can be overwritten. There is no `storage`-event merge or lock yet.
module Causa.App.PersistencePort

open System
open Limen.Contract
open Limen.Contract.Core
open Causa.Persistence
module A = Causa.App.Adapter
module EP = Causa.App.EnginePort
module Book = IndyInitRootCauseInvestigator.Engine.Sync

let private prefix = "persist-"
let private bootId = "persist-boot"
let private storePrefix = "persist-store-"
/// A local write first reads the shared record, then stores the merge (review R2-F1: tabs share one store).
let private mergePrefix = "persist-merge-"

type private Boot =
    | NotBooted
    | Booting of held: EP.PersistenceInput list
    | Booted

type private State =
    { Model: Model
      Boot: Boot
      /// Methods of requests in flight: a network failure during a write may have reached GitHub.
      Methods: Map<string, string>
      /// Documents already checked automatically since their save became unknown (checked once, then by hand).
      Checked: Set<string>
      /// Local fixture mode (test build only): the in-page provider that answers instead of GitHub.
      Fixture: RecordedGitHub option
      Notice: (string * string) option
      /// Local writes waiting for their read of the shared record: write id -> key, this page's record.
      Writes: Map<string, string * string> }

/// Fixture mode exists only where the test build enables it (Dispatch.enableTestHooks); production never has it.
let mutable private fixtureAllowed = false

let enableFixtureMode () = fixtureAllowed <- true

let private fixtureLabel = "Local fixture: synthetic data, not GitHub"

let private env (entropy: A.Entropy) : Env =
    let t = DateTimeOffset(DateTime.SpecifyKind(entropy.Now, DateTimeKind.Utc))
    let ms = t.AddTicks(-(t.Ticks % TimeSpan.TicksPerMillisecond))
    { Now = (fun () -> Timestamp.ofDateTimeOffset ms)
      Nonce = entropy.Nonce }

let private httpMethod (m: string) =
    match m with
    | "POST" -> HttpMethod.Post
    | "PATCH" -> HttpMethod.Patch
    | "PUT" -> HttpMethod.Put
    | "DELETE" -> HttpMethod.Delete
    | _ -> HttpMethod.Get

/// The token leaves the program only here, as the Authorization header of the request that needs it.
let private toLimen (e: Effect) : EffectRequest =
    match e with
    | Send(id, r) ->
        let headers = r.Headers @ (r.Credential |> Option.map (fun c -> "Authorization", c.AuthorizationHeader) |> Option.toList)
        EffectRequest.Http
            { CorrelationId = CorrelationId(prefix + id)
              Method = httpMethod r.Method
              Url = r.Url
              Headers = Some(Map.ofList headers)
              Body = r.Body
              TimeoutMs = 20_000L
              Response = Some HttpResponseKind.Text
              ResponseHeaders = Some [ "x-oauth-scopes"; "x-ratelimit-remaining"; "x-ratelimit-reset"; "retry-after" ]
              Credentials = Some HttpCredentials.Omit
              Xsrf = None }
    | StoreLocal(writeId, key, _) -> EffectRequest.Storage(StorageEffectRequest.Get(CorrelationId(mergePrefix + writeId), key))

let private connection (m: Model) : A.Connection =
    match Sync.connectionLabel m, m.Target with
    | ("connected" | "authorizedEmpty" | "conflict"), Some t when Fixture.isFixture t -> A.ConnectedTo(fixtureLabel, "local", t.Workspace)
    | ("connected" | "authorizedEmpty" | "conflict"), Some t -> A.ConnectedTo(Target.fullName t, t.Branch, t.Workspace)
    | ("connecting" | "loading"), _ -> A.Connecting
    | ("incompatible" | "accessDenied" | "offline"), _ ->
        A.ConnectionFailed(Sync.connectionDetail m |> Option.defaultValue "The data repository could not be used.")
    | _ -> A.NotConnected

let private syncStatus (m: Model) (d: Document) : A.SyncStatus =
    let pendingEvents = d.Local.Version - (d.Confirmed |> Option.map (fun c -> c.Version) |> Option.defaultValue 0L)
    if d.ReadOnly then A.ReadOnly
    // Fixture mode never claims GitHub: changes live in this page only (PF-003-O4).
    // (A conflict, an unknown outcome or a failure is still shown as such: those are the program's own states.)
    elif m.Target |> Option.exists Fixture.isFixture
         && not (List.contains (Sync.documentStatus m d) [ "conflict"; "unknown"; "failed" ]) then
        A.LocalOnly "Fixture mode: kept in this page only; nothing is saved to GitHub."
    else
        match Sync.documentStatus m d with
        | "clean" -> A.NoChanges
        | "remotelySynced" -> A.RemotelySynced d.LastSyncedAt
        | "syncingRemotely" -> A.Saving
        | "conflict" -> A.Conflict
        | "unknown" -> A.MayHaveSaved "The last save may or may not have reached GitHub; checking the branch before anything else is sent."
        | "failed" -> A.SaveFailed "The change is kept on this device."
        | _ -> A.Unsaved(int pendingEvents)

/// The program's connection, exhaustively, as the shell's union.
let private connectionState (m: Model) : EP.ConnectionState =
    let conflict = m.Documents |> Map.exists (fun _ d -> Sync.documentStatus m d = "conflict")
    match m.Connection with
    | Connection.Connected
    | Connection.AuthorizedEmpty when conflict -> EP.ConnectionState.Conflict
    | Connection.Disconnected -> EP.ConnectionState.Disconnected
    | Connection.Connecting -> EP.ConnectionState.Connecting
    | Connection.Loading -> EP.ConnectionState.Loading
    | Connection.AuthorizedEmpty -> EP.ConnectionState.AuthorizedEmpty
    | Connection.Connected -> EP.ConnectionState.Connected
    | Connection.Incompatible _ -> EP.ConnectionState.Incompatible
    | Connection.AccessDenied _ -> EP.ConnectionState.AccessDenied
    | Connection.Offline _ -> EP.ConnectionState.Offline

let private connectionDetail (m: Model) =
    match Sync.connectionDetail m with
    | Some d -> Some d
    | None ->
        m.Documents
        |> Map.toList
        |> List.tryPick (fun (_, d) -> d.RemoteConflict |> Option.map (fun c -> c.Detail))

let rec private persistenceOf (s: State) : EP.Persistence =
    let m = s.Model
    let order =
        match m.Catalog with
        | CatalogVerified(man, _) -> man.Cases |> List.map (fun c -> c.InvestigationId)
        | _ -> m.Documents |> Map.toList |> List.map fst
    { Name = (if s.Fixture.IsSome then "fixture" else "github")
      Connection = connection m
      ConnectionState = connectionState m
      ConnectionDetail = connectionDetail m
      Catalog =
        (match m.Connection with
         | Connection.Disconnected when m.Documents.IsEmpty -> A.NotRequested
         | Connection.Connecting
         | Connection.Loading -> A.Loading
         | Connection.Incompatible r
         | Connection.AccessDenied r
         | Connection.Offline r when m.Documents.IsEmpty -> A.Failed r
         | _ -> A.Loaded order)
      Documents =
        m.Documents
        |> Map.map (fun _ d ->
            A.Loaded
                { EP.Document.Session = IndyInitRootCauseInvestigator.Engine.Kernel.ofLoaded d.Local.Loaded
                  EP.Document.Writable = not d.ReadOnly
                  EP.Document.Integrity =
                    Some
                        { EventsSha256 = d.Local.EventsSha256
                          StateSha256 = d.Local.StateSha256
                          AdmissibilityChecked = true }
                  EP.Document.Sync = syncStatus m d })
      Message = s.Notice
      Administration =
        [ { Id = "provider"; Section = "persistence"; Label = "Data provider"
            Value =
                (match m.Target with
                 | Some t when Fixture.isFixture t -> "Local fixture: synthetic data generated in this page; nothing is read from or saved to GitHub"
                 | Some t -> "GitHub " + Target.fullName t + " · " + t.Branch + " · " + t.Workspace
                 | None -> "GitHub (not connected)")
            State = "Reported by the running application" }
          { Id = "connection"; Section = "persistence"; Label = "Connection"; Value = EP.ConnectionState.label (connectionState m); State = "Reported by the running application" } ]
      Handle = fun entropy input -> handle entropy input s }

/// Apply one program message, then the automatic policies, and translate the effects.
and private apply (entropy: A.Entropy) (msg: Msg) (s: State) : State * EffectRequest list =
    // One message, then everything it causes. In fixture mode the in-page provider answers every request at once and
    // nothing is stored in the browser: no effect leaves the program.
    let rec drain (s: State, out: Effect list) (queue: Msg list) =
        match queue with
        | [] -> s, out
        | msg :: rest ->
            match Sync.update (env entropy) msg s.Model with
            // An older local write confirming after a newer one is expected, not news for the person.
            | Error _ when (match msg with LocalStored _ -> true | _ -> false) -> drain (s, out) rest
            | Error r -> drain ({ s with Notice = Some("warning", r.Reason) }, out) rest
            | Ok(m, effects) ->
                let s = { s with Model = m; Notice = None }
                match s.Fixture with
                | Some provider ->
                    let answers =
                        effects
                        |> List.map (fun e ->
                            match e with
                            | Send(id, request) -> HttpAnswered(id, provider.Handle request)
                            | StoreLocal(writeId, _, _) -> LocalStored(writeId, true))
                    drain (s, out) (rest @ answers)
                | None -> drain (s, out @ effects) rest
    let policies (s: State) =
        s.Model.Documents
        |> Map.toList
        |> List.choose (fun (id, d) ->
            if Sync.canSave s.Model d && (match d.Book.Phase with Book.Idle(_, Some _) -> true | _ -> false) then Some(Save id, None)
            elif Sync.canReconcile s.Model d && not (s.Checked.Contains id) then Some(Reconcile id, Some id)
            else None)
    let rec settle (s: State, out: Effect list) (rounds: int) =
        // Automatic save of pending changes, and one automatic check of an unknown outcome (bounded).
        match policies s with
        | [] -> s, out
        | _ when rounds = 0 -> s, out
        | due ->
            let s = { s with Checked = due |> List.choose snd |> List.fold (fun acc id -> Set.add id acc) s.Checked }
            settle (drain (s, out) (due |> List.map fst)) (rounds - 1)
    let s, effects = settle (drain (s, []) [ msg ]) 8
    let checkedNow = s.Checked |> Set.filter (fun id -> s.Model.Documents |> Map.tryFind id |> Option.exists (fun d -> Sync.documentStatus s.Model d = "unknown"))
    let methods = effects |> List.fold (fun acc e -> match e with Send(id, r) -> Map.add (prefix + id) r.Method acc | _ -> acc) s.Methods
    let writes = effects |> List.fold (fun acc e -> match e with StoreLocal(w, k, v) -> Map.add w (k, v) acc | _ -> acc) s.Writes
    { s with Methods = methods; Checked = checkedNow; Writes = writes }, effects |> List.map toLimen

and private answer (entropy: A.Entropy) (c: string) (result: EffectResult) (s: State) : State * EffectRequest list =
    match result with
    | EffectResult.StorageResult(_, outcome) when c = bootId ->
        let stored =
            match outcome with
            | StorageOutcome.Success value -> value
            | StorageOutcome.Failure _ -> None
        let held = match s.Boot with Booting held -> held | _ -> []
        let s, fx = apply entropy (Restore stored) { s with Boot = Booted }
        // A record that cannot be restored is left untouched and reported; the workspace starts empty.
        let s, fx =
            held
            |> List.fold (fun (s, fx) input ->
                let s2, fx2 = handleState entropy input s
                s2, fx @ fx2) (s, fx)
        match Sync.preservedElsewhere s.Model with
        | [] -> s, fx
        | ids ->
            let notice =
                sprintf "Another tab of this browser kept a different unsaved change to %d investigation(s). It is kept in this browser and was not overwritten; finish or discard the change in that tab." ids.Length
            { s with Notice = Some("warning", notice) }, fx
    | EffectResult.StorageResult(_, outcome) when c.StartsWith(mergePrefix, StringComparison.Ordinal) ->
        let writeId = c.Substring mergePrefix.Length
        match Map.tryFind writeId s.Writes with
        | Some(key, value) ->
            let merged =
                match outcome with
                | StorageOutcome.Success stored -> Sync.mergeRecords value stored
                | StorageOutcome.Failure _ -> value
            { s with Writes = s.Writes |> Map.remove writeId },
            [ EffectRequest.Storage(StorageEffectRequest.Set(CorrelationId(storePrefix + writeId), key, merged)) ]
        | None -> s, []
    | EffectResult.StorageResult(_, outcome) when c.StartsWith(storePrefix, StringComparison.Ordinal) ->
        let ok = match outcome with StorageOutcome.Success _ -> true | StorageOutcome.Failure _ -> false
        apply entropy (LocalStored(c.Substring storePrefix.Length, ok)) s
    | EffectResult.HttpResult(_, outcome) ->
        let id = c.Substring prefix.Length
        let isWrite = s.Methods |> Map.tryFind c |> Option.exists (fun m -> m <> "GET")
        let s = { s with Methods = s.Methods |> Map.remove c }
        let msg =
            match outcome with
            | EffectOutcome.Success(status, RawJson body, headers) ->
                // The kernel delivers a text response as a JSON string literal.
                let text =
                    try
                        Text.Json.JsonSerializer.Deserialize<string>(body: string) |> Option.ofObj |> Option.defaultValue ""
                    with _ ->
                        body
                let hs = headers |> Option.map (Map.toList >> List.map (fun (k: string, v: string) -> k.ToLowerInvariant(), v)) |> Option.defaultValue []
                HttpAnswered(id, { Status = int status; Headers = hs; Body = text })
            | EffectOutcome.Failure _ when isWrite -> HttpFailed(id, TransportFailure.OutcomeUnknown "the connection failed during a write")
            | EffectOutcome.Failure _ -> HttpFailed(id, NotSent "the network request failed")
            | EffectOutcome.Cancelled -> HttpFailed(id, Cancelled)
            | EffectOutcome.OutcomeUnknown _ -> HttpFailed(id, TransportFailure.OutcomeUnknown "no answer")
        apply entropy msg s
    | _ -> s, []

and private handleState (entropy: A.Entropy) (input: EP.PersistenceInput) (s: State) : State * EffectRequest list =
    match s.Boot, input with
    | _, EP.Answer(c, result) when c.StartsWith(prefix, StringComparison.Ordinal) -> answer entropy c result s
    | _, EP.Answer _ -> s, []
    | NotBooted, EP.Boot -> { s with Boot = Booting [] }, [ EffectRequest.Storage(StorageEffectRequest.Get(CorrelationId bootId, Sync.storageKey)) ]
    | (Booting _ | Booted), EP.Boot -> s, []
    | NotBooted, _ ->
        { s with Boot = Booting [ input ] }, [ EffectRequest.Storage(StorageEffectRequest.Get(CorrelationId bootId, Sync.storageKey)) ]
    | Booting held, _ -> { s with Boot = Booting(held @ [ input ]) }, []
    | Booted, EP.Connect r when r.Repository.Trim() = Fixture.owner + "/" + Fixture.repo ->
        if not fixtureAllowed then
            { s with Notice = Some("warning", "Fixture mode exists only in the test build. Nothing was sent.") }, []
        else
            let ws = if List.contains (r.Workspace.Trim()) Fixture.workspaces then r.Workspace.Trim() else Fixture.workspace
            match Fixture.providerFor ws, Credential.create "fixture-mode-no-credential" with
            | Ok provider, Ok placeholder ->
                let t = { Owner = Fixture.owner; Repo = Fixture.repo; Branch = Fixture.branch; Workspace = ws }
                apply entropy (Msg.Connect(t, placeholder)) { s with Fixture = Some provider }
            | Error e, _
            | _, Error e -> { s with Notice = Some("warning", "The fixture could not be generated: " + e) }, []
    | Booted, EP.Connect r ->
        let s = { s with Fixture = None }
        match r.Repository.Trim().Split('/'), Credential.create r.Token with
        | [| owner; repo |], Ok cred ->
            match Target.create owner repo (r.Branch.Trim()) (r.Workspace.Trim()) with
            | Ok t -> apply entropy (Msg.Connect(t, cred)) s
            | Error problems -> { s with Notice = Some("warning", String.Join(" ", problems)) }, []
        | _, Error reason -> { s with Notice = Some("warning", reason) }, []
        | _ -> { s with Notice = Some("warning", "Enter the repository as owner/name.") }, []
    | Booted, EP.Disconnect -> apply entropy Msg.Disconnect s
    | Booted, EP.Open _ -> s, []
    | Booted, EP.DiscardLocal id -> apply entropy (Msg.DiscardLocal id) s
    | Booted, EP.ReloadRemote id -> apply entropy (Msg.ReloadRemote id) s
    // "Check now": settle every save whose outcome is unknown by re-reading the branch (never a blind retry).
    | Booted, EP.CheckRemote ->
        s.Model.Documents
        |> Map.toList
        |> List.filter (fun (_, d) -> Sync.canReconcile s.Model d)
        |> List.fold (fun (s, fx) (id, _) ->
            let s2, fx2 = apply entropy (Reconcile id) s
            s2, fx @ fx2) (s, [])
    | Booted, EP.Accepted(id, expected, _, events) -> apply entropy (Append(id, defaultArg expected 0L, events)) s

and private handle (entropy: A.Entropy) (input: EP.PersistenceInput) (s: State) : EP.Persistence * EffectRequest list =
    let s, fx = handleState entropy input s
    persistenceOf s, fx

/// The persistence the shell composes in production.
let persistence: EP.Persistence =
    persistenceOf
        { Model = Sync.initial
          Boot = NotBooted
          Methods = Map.empty
          Checked = Set.empty
          Fixture = None
          Notice = None
          Writes = Map.empty }
