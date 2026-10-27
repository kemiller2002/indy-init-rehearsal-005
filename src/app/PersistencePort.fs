/// The shell's persistence (EnginePort.Persistence) over stream persist's program, Causa.Persistence.Sync: the
/// connection and its eight states, loading, the historical catalog, local durability and restore, and publication
/// through core's request book. This module only translates — Sync messages in, Limen effects out, the program's
/// state into the shell's projection — plus two policies the shell has no controls for yet:
///  - restore: the first input restores this device's stored record (storage Get) before it is handled;
///  - automatic save and check: a pending change is saved when connected, and a save whose outcome is unknown is
///    checked once (reconcile, which reads the branch first); a failed save is never retried blindly.
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
      Notice: (string * string) option }

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
    | StoreLocal(writeId, key, value) -> EffectRequest.Storage(StorageEffectRequest.Set(CorrelationId(storePrefix + writeId), key, value))

let private connection (m: Model) : A.Connection =
    match Sync.connectionLabel m, m.Target with
    | ("connected" | "authorizedEmpty" | "conflict"), Some t -> A.ConnectedTo(Target.fullName t, t.Branch, t.Workspace)
    | ("connecting" | "loading"), _ -> A.Connecting
    | ("incompatible" | "accessDenied" | "offline"), _ ->
        A.ConnectionFailed(Sync.connectionDetail m |> Option.defaultValue "The data repository could not be used.")
    | _ -> A.NotConnected

let private syncStatus (m: Model) (d: Document) : A.SyncStatus =
    let pendingEvents = d.Local.Version - (d.Confirmed |> Option.map (fun c -> c.Version) |> Option.defaultValue 0L)
    if d.ReadOnly then A.ReadOnly
    else
        match Sync.documentStatus m d with
        | "clean" -> A.NoChanges
        | "remotelySynced" -> A.RemotelySynced d.LastSyncedAt
        | "syncingRemotely" -> A.Saving
        | "conflict" -> A.Conflict
        | "unknown" -> A.MayHaveSaved "The last save may or may not have reached GitHub; checking the branch before anything else is sent."
        | "failed" -> A.SaveFailed "The change is kept on this device."
        | _ -> A.Unsaved(int pendingEvents)

let private connectionLabelText (m: Model) =
    match Sync.connectionLabel m with
    | "disconnected" -> "Not connected"
    | "connecting" -> "Connecting: checking the repository and branch"
    | "loading" -> "Connected: loading the workspace"
    | "authorizedEmpty" -> "Connected: the workspace has no investigation yet"
    | "connected" -> "Connected"
    | "incompatible" -> "The repository's data is not compatible"
    | "accessDenied" -> "Access denied"
    | "offline" -> "Offline"
    | "conflict" -> "Connected: someone else changed an investigation"
    | other -> other

let rec private persistenceOf (s: State) : EP.Persistence =
    let m = s.Model
    let order =
        match m.Catalog with
        | CatalogVerified(man, _) -> man.Cases |> List.map (fun c -> c.InvestigationId)
        | _ -> m.Documents |> Map.toList |> List.map fst
    { Name = "github"
      Connection = connection m
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
            Value = (match m.Target with Some t -> "GitHub " + Target.fullName t + " · " + t.Branch + " · " + t.Workspace | None -> "GitHub (not connected)")
            State = "Reported by the running application" }
          { Id = "connection"; Section = "persistence"; Label = "Connection"; Value = connectionLabelText m; State = "Reported by the running application" } ]
      Handle = fun entropy input -> handle entropy input s }

/// Apply one program message, then the automatic policies, and translate the effects.
and private apply (entropy: A.Entropy) (msg: Msg) (s: State) : State * EffectRequest list =
    let step (s: State, fx: Effect list) (msg: Msg) =
        match Sync.update (env entropy) msg s.Model with
        | Ok(m, effects) -> { s with Model = m; Notice = None }, fx @ effects
        | Error r -> { s with Notice = Some("warning", r.Reason) }, fx
    let s, effects = step (s, []) msg
    // Automatic save of pending changes, and one automatic check of an unknown outcome.
    let s, effects =
        s.Model.Documents
        |> Map.fold (fun (s: State, fx) id d ->
            if Sync.canSave s.Model d && (match d.Book.Phase with Book.Idle(_, Some _) -> true | _ -> false) then step (s, fx) (Save id)
            elif Sync.canReconcile s.Model d && not (s.Checked.Contains id) then
                let s2, fx2 = step (s, fx) (Reconcile id)
                { s2 with Checked = s2.Checked.Add id }, fx2
            else s, fx) (s, effects)
    let checkedNow = s.Checked |> Set.filter (fun id -> s.Model.Documents |> Map.tryFind id |> Option.exists (fun d -> Sync.documentStatus s.Model d = "unknown"))
    let methods = effects |> List.fold (fun acc e -> match e with Send(id, r) -> Map.add (prefix + id) r.Method acc | _ -> acc) s.Methods
    { s with Methods = methods; Checked = checkedNow }, effects |> List.map toLimen

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
        held
        |> List.fold (fun (s, fx) input ->
            let s2, fx2 = handleState entropy input s
            s2, fx @ fx2) (s, fx)
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
    | NotBooted, _ ->
        { s with Boot = Booting [ input ] }, [ EffectRequest.Storage(StorageEffectRequest.Get(CorrelationId bootId, Sync.storageKey)) ]
    | Booting held, _ -> { s with Boot = Booting(held @ [ input ]) }, []
    | Booted, EP.Connect r ->
        match r.Repository.Trim().Split('/'), Credential.create r.Token with
        | [| owner; repo |], Ok cred ->
            match Target.create owner repo (r.Branch.Trim()) (r.Workspace.Trim()) with
            | Ok t -> apply entropy (Msg.Connect(t, cred)) s
            | Error problems -> { s with Notice = Some("warning", String.Join(" ", problems)) }, []
        | _, Error reason -> { s with Notice = Some("warning", reason) }, []
        | _ -> { s with Notice = Some("warning", "Enter the repository as owner/name.") }, []
    | Booted, EP.Disconnect -> apply entropy Msg.Disconnect s
    | Booted, EP.Open _ -> s, []
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
          Notice = None }
