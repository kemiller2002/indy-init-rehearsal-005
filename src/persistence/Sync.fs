namespace Causa.Persistence

open System

/// The connection to the data repository (EXT-DATA-007: eight distinguishable states; "conflict" is projected from
/// the documents, see Sync.connectionLabel).
type Connection =
    | Disconnected
    /// Probing the repository and the branch with the supplied credential. Never "connected" yet (EXT-DATA-007-O2).
    | Connecting
    /// Reading the workspace (catalog pointer, listings, files) at the probed head.
    | Loading
    /// Connected; the workspace holds no investigation.
    | AuthorizedEmpty
    | Connected
    /// The repository answered but its data fails the contract or the catalog (fail closed, EXT-DATA-005-O2).
    | Incompatible of reason: string
    | AccessDenied of reason: string
    | Offline of reason: string

/// One publication: what was sent, against which head (CONTRACT §9.4.2).
type Attempt =
    { RequestId: string
      Base: string
      /// The file set as sent: success is claimed only for exactly this (and only after a re-read).
      Sent: Verified
      /// Git blob ids of the sent files by name: a re-read proves by content, not by ancestry, what was applied.
      BlobShas: (string * string) list }

/// The remote side of one document. Pending, sending, conflicted and unknown remain distinct (CONTRACT §9.4.3).
type Remote =
    /// The local copy equals the last remotely confirmed one.
    | InSync
    /// Local changes not yet sent.
    | Ahead
    | Sending of Attempt
    /// GitHub acknowledged a commit; the re-read at it is in flight. Not yet "saved".
    | Confirming of Attempt * commit: string
    /// The request may have been applied (answer lost or unverifiable). Only reconciliation leaves this state.
    | OutcomeUnknown of Attempt
    | Reconciling of Attempt * stage: ReconcileStage
    /// Another writer changed the branch or the investigation. Local work is kept (UI-006-O4).
    | Conflicted of remoteHead: string option * remote: Verified option * detail: string
    /// The provider certainly did not apply the last send. Local work is kept; Save may be retried.
    | SendFailed of ProviderFailure

and ReconcileStage =
    | ReadingHead of requestId: string
    | ReadingListing of requestId: string * head: string

type Durability =
    | NotSaved
    | SavingLocally of writeId: string
    | SavedLocally
    | LocalSaveFailed of detail: string

type Document =
    { InvestigationId: string
      /// Historical catalog and frozen data are read-only (UI-006-O5).
      ReadOnly: bool
      /// The last remotely confirmed file set and the head it was confirmed at (the provider version token).
      Confirmed: Verified option
      ConfirmedHead: string option
      /// The local copy: Confirmed plus pending local events (the optimistic local projection).
      Local: Verified
      Remote: Remote
      Durability: Durability
      LastSyncedAt: string option
      /// An in-flight remote reload of this document (after a conflict), if any.
      Reload: ReloadJob option }

/// Reading one investigation directory at a commit: listing, then each blob.
and DirRead =
    { InvestigationId: string
      Commit: string
      Entries: ListedEntry list option
      Blobs: Map<string, byte[]> }

and ReloadJob =
    | ReloadHead of requestId: string
    | ReloadDir of DirRead

type CatalogStatus =
    | NotCatalog
    | CatalogLoading of CatalogPointer
    | CatalogVerified of CatalogManifest * CatalogCaseResult list
    | CatalogFailed of reason: string

/// The workspace load after Connect (the Sync machine owns loading: Rehearsal 4's lesson).
type LoadJob =
    { Head: string
      Pointer: CatalogPointer option
      Manifest: CatalogManifest option
      Listed: bool
      Dirs: Map<string, DirRead>
      Done: Map<string, Result<Verified, ContractError>> }

/// Why a request is in flight. Every answer is matched to exactly the request it answers (PF-009).
type Purpose =
    | ProbeRepository
    | ProbeHead
    | ReadPointer
    | ReadCatalogManifest
    | ListInvestigations
    | ListDir of investigationId: string * reload: bool
    | ReadBlob of investigationId: string * name: string * reload: bool
    | Commit of investigationId: string
    | ConfirmListing of investigationId: string
    | ReconcileHead of investigationId: string
    | ReconcileListing of investigationId: string
    | ReloadHeadRead of investigationId: string

type InFlight = { Purpose: Purpose; Generation: int }

type Model =
    { Target: Target option
      /// Memory only. Never stored, logged, projected or put anywhere but the Authorization header.
      Credential: Credential option
      /// Incremented by Connect, Disconnect and credential rejection: answers of an older generation are refused.
      Generation: int
      Connection: Connection
      Repository: RepositoryInfo option
      Head: string option
      Load: LoadJob option
      Catalog: CatalogStatus
      Documents: Map<string, Document>
      LoadErrors: (string * ContractError) list
      InFlight: Map<string, InFlight>
      NextId: int }

type Msg =
    /// Restore after a restart from the local store's text (None: nothing stored). First message only.
    | Restore of stored: string option
    | Connect of Target * Credential
    | Disconnect
    /// A new investigation: events decided by the engine for the create command (expected version 0).
    | Create of investigationId: string * events: JValue list
    /// An engine-decided change: the events and the version they were decided at.
    | Edit of investigationId: string * expectedVersion: int64 * events: JValue list
    | Save of investigationId: string
    | Reconcile of investigationId: string
    | ReloadRemote of investigationId: string
    | DiscardLocal of investigationId: string
    | HttpAnswered of requestId: string * HttpResponse
    | HttpFailed of requestId: string * TransportFailure
    | LocalStored of writeId: string * ok: bool

type Effect =
    | Send of requestId: string * HttpRequest
    /// Persist the local record (never a credential) under `key`.
    | StoreLocal of writeId: string * key: string * value: string

/// A refused step: the model is unchanged and nothing is sent (DCT-002-B).
type Refusal = { Reason: string }

type Env =
    { Engine: HistoryEngine
      Now: unit -> string }

[<RequireQualifiedAccess>]
module Sync =
    let storageKey = "causa.persistence/1"

    let initial =
        { Target = None
          Credential = None
          Generation = 0
          Connection = Disconnected
          Repository = None
          Head = None
          Load = None
          Catalog = NotCatalog
          Documents = Map.empty
          LoadErrors = []
          InFlight = Map.empty
          NextId = 1 }

    type private Step = Result<Model * Effect list, Refusal>

    let private refuse reason : Step = Error { Reason = reason }

    let private nextId (m: Model) = sprintf "r%d" m.NextId, { m with NextId = m.NextId + 1 }

    let private send (purpose: Purpose) (build: Credential option -> Target -> string -> HttpRequest) (m: Model, effects: Effect list) =
        match m.Target with
        | None -> m, effects
        | Some t ->
            let id, m = nextId m
            { m with InFlight = m.InFlight |> Map.add id { Purpose = purpose; Generation = m.Generation } },
            effects @ [ Send(id, build m.Credential t id) ]

    let private sendWithId (purpose: Purpose) (build: Credential option -> Target -> string -> HttpRequest) (m: Model, effects: Effect list) =
        let m2, e2 = send purpose build (m, effects)
        let id = sprintf "r%d" m.NextId
        (m2, e2), id

    let unsettled (d: Document) =
        match d.Remote with
        | Sending _
        | Confirming _
        | Reconciling _ -> true
        | _ -> d.Reload.IsSome

    let private anyUnsettled (m: Model) = m.Documents |> Map.exists (fun _ d -> unsettled d)

    let private isCatalogWorkspace (m: Model) =
        match m.Target with
        | Some t ->
            let root = Target.workspaceRoot t
            match m.Catalog with
            | CatalogLoading p -> p.Workspace = root
            | CatalogVerified(man, _) -> man.Workspace = root
            | _ -> m.Documents |> Map.exists (fun _ d -> d.ReadOnly)
        | None -> false

    // ---------- local durability (T19) ----------

    let private verifiedText (v: Verified) =
        JObj
            [ "manifest", JStr(Json.strictUtf8.GetString v.Manifest)
              "events", JStr(Json.strictUtf8.GetString v.EventsFile)
              "snapshot", JStr(Json.strictUtf8.GetString v.Snapshot) ]

    let private remoteLabel (r: Remote) =
        match r with
        | InSync -> "inSync"
        | Ahead -> "ahead"
        | SendFailed _ -> "failed"
        | Conflicted _ -> "conflict"
        | Sending _
        | Confirming _
        | OutcomeUnknown _
        | Reconciling _ -> "unknown"

    let private attemptOf (r: Remote) =
        match r with
        | Sending a
        | Confirming(a, _)
        | OutcomeUnknown a
        | Reconciling(a, _) -> Some a
        | _ -> None

    /// The durable local record: target (no credential), and per writable document the confirmed files, the
    /// provider token, the pending events and the remote state. A send that had started is stored as unknown:
    /// after a restart nobody knows whether it was applied (reload safety).
    let record (m: Model) : string =
        let target =
            match m.Target with
            | Some t -> JObj [ "owner", JStr t.Owner; "repo", JStr t.Repo; "branch", JStr t.Branch; "workspace", JStr t.Workspace ]
            | None -> JNull
        let doc (d: Document) =
            let confirmedVersion = d.Confirmed |> Option.map (fun v -> v.Version) |> Option.defaultValue 0L
            let pending = d.Local.EventLines |> List.skip (int confirmedVersion)
            let attempt =
                match attemptOf d.Remote with
                | Some a ->
                    JObj
                        [ "base", JStr a.Base
                          "sentVersion", JInt a.Sent.Version
                          "blobShas", JObj(a.BlobShas |> List.map (fun (n, s) -> n, JStr s)) ]
                | None -> JNull
            JObj
                [ "investigationId", JStr d.InvestigationId
                  "confirmedHead", (match d.ConfirmedHead with Some h -> JStr h | None -> JNull)
                  "confirmed", (match d.Confirmed with Some v -> verifiedText v | None -> JNull)
                  "pendingEvents", JArr(pending |> List.map JStr)
                  "remote", JStr(remoteLabel d.Remote)
                  "attempt", attempt
                  "lastSyncedAt", (match d.LastSyncedAt with Some s -> JStr s | None -> JNull) ]
        Json.canonicalText (
            JObj
                [ "schema", JStr "causa.local-store/1"
                  "target", target
                  "documents", JArr(m.Documents |> Map.toList |> List.map snd |> List.filter (fun d -> not d.ReadOnly) |> List.map doc) ]
        )

    /// Queue a local write of the record and mark the given documents as saving locally.
    let private persistLocal (ids: string list) (m: Model, effects: Effect list) =
        let writeId, m = nextId m
        let m =
            { m with
                Documents =
                    ids
                    |> List.fold (fun docs id ->
                        match Map.tryFind id docs with
                        | Some d when not d.ReadOnly -> docs |> Map.add id { d with Durability = SavingLocally writeId }
                        | _ -> docs) m.Documents }
        m, effects @ [ StoreLocal(writeId, storageKey, record m) ]

    let private parseStored (env: Env) (text: string) : Result<Target option * Document list, string> =
        let fileOf (o: (string * JValue) list) id =
            match Json.str "manifest" o, Json.str "events" o, Json.str "snapshot" o with
            | Some mf, Some ev, Some sn ->
                FileSet.read
                    env.Engine
                    { DirectoryName = id
                      Entries =
                        [ FileSet.manifestName, Json.strictUtf8.GetBytes mf
                          FileSet.eventsName, Json.strictUtf8.GetBytes ev
                          FileSet.snapshotName, Json.strictUtf8.GetBytes sn ] }
                |> Result.mapError (fun e -> e.Code + ": " + e.Detail)
            | _ -> Error "stored confirmed files are incomplete"
        match Json.parse text with
        | Ok(JObj o) when Json.str "schema" o = Some "causa.local-store/1" ->
            let target =
                match Json.field "target" o with
                | Some(JObj t) ->
                    match Json.str "owner" t, Json.str "repo" t, Json.str "branch" t, Json.str "workspace" t with
                    | Some a, Some b, Some c, Some d -> Target.create a b c d |> Result.toOption
                    | _ -> None
                | _ -> None
            let docs =
                match Json.field "documents" o with
                | Some(JArr ds) ->
                    ds
                    |> traverse (fun dv ->
                        match dv with
                        | JObj d ->
                            let id = Json.str "investigationId" d |> Option.defaultValue ""
                            let confirmed =
                                match Json.field "confirmed" d with
                                | Some(JObj f) -> fileOf f id |> Result.map Some
                                | _ -> Ok None
                            let pending =
                                match Json.field "pendingEvents" d with
                                | Some(JArr ls) ->
                                    ls
                                    |> traverse (fun l ->
                                        match l with
                                        | JStr s -> Json.parse s
                                        | _ -> Error "pending event is not text")
                                | _ -> Ok []
                            match confirmed, pending with
                            | Error e, _
                            | _, Error e -> Error e
                            | Ok c, Ok events ->
                                let cv = c |> Option.map (fun v -> v.Version) |> Option.defaultValue 0L
                                let local =
                                    if events.IsEmpty then
                                        match c with
                                        | Some v -> Ok v
                                        | None -> Error "a stored document has neither confirmed files nor pending events"
                                    else FileSet.append env.Engine id c cv events |> Result.mapError (fun e -> e.Code + ": " + e.Detail)
                                match local with
                                | Error e -> Error e
                                | Ok local ->
                                    let attempt =
                                        match Json.field "attempt" d with
                                        | Some(JObj a) ->
                                            let sentVersion = Json.int "sentVersion" a |> Option.defaultValue 0L
                                            let sentEvents = events |> List.truncate (int (sentVersion - cv))
                                            let sent =
                                                if sentEvents.IsEmpty then c
                                                else FileSet.append env.Engine id c cv sentEvents |> Result.toOption
                                            match sent, Json.str "base" a, Json.field "blobShas" a with
                                            | Some s, Some b, Some(JObj shas) ->
                                                Some
                                                    { RequestId = "restored"
                                                      Base = b
                                                      Sent = s
                                                      BlobShas = shas |> List.choose (fun (n, v) -> match v with JStr x -> Some(n, x) | _ -> None) }
                                            | _ -> None
                                        | _ -> None
                                    let remote =
                                        match Json.str "remote" d, attempt with
                                        | Some "unknown", Some a -> OutcomeUnknown a
                                        | Some "conflict", _ -> Conflicted(None, None, "A conflict was found before the restart; reload the remote copy.")
                                        | Some "failed", _ -> SendFailed(ProviderFailure.Offline "restored after a failed send")
                                        | _ when local.Version > cv -> Ahead
                                        | _ -> InSync
                                    Ok
                                        { InvestigationId = id
                                          ReadOnly = false
                                          Confirmed = c
                                          ConfirmedHead = Json.str "confirmedHead" d
                                          Local = local
                                          Remote = remote
                                          Durability = SavedLocally
                                          LastSyncedAt = Json.str "lastSyncedAt" d
                                          Reload = None }
                        | _ -> Error "stored document is not an object")
                | _ -> Ok []
            docs |> Result.map (fun ds -> target, ds)
        | Ok _ -> Error "the local store holds an unknown record format"
        | Error e -> Error("the local store is unreadable: " + e)

    // ---------- loading (connect, workspace, catalog) ----------

    let private dirOf (m: Model) (id: string) =
        match m.Target with
        | Some t -> Target.investigationDir t id
        | None -> ""

    /// Fold a remote file set read at `head` into the documents, preserving every local change.
    let private integrate (head: string) (id: string) (remote: Verified option) (readOnly: bool) (m: Model) : Model =
        let existing = Map.tryFind id m.Documents
        let fresh (v: Verified) =
            { InvestigationId = id
              ReadOnly = readOnly
              Confirmed = Some v
              ConfirmedHead = Some head
              Local = v
              Remote = InSync
              Durability = SavedLocally
              LastSyncedAt = None
              Reload = None }
        let sameBytes (a: Verified option) (b: Verified option) =
            match a, b with
            | Some x, Some y -> x.EventsFile = y.EventsFile
            | None, None -> true
            | _ -> false
        let doc =
            match existing, remote with
            | None, Some r -> Some(fresh r)
            | None, None -> None
            | Some d, _ when d.ReadOnly || readOnly ->
                remote |> Option.map fresh
            | Some d, _ ->
                match d.Remote with
                | InSync ->
                    match remote with
                    | Some r when d.Confirmed.IsNone || FileSet.successor d.Local r |> Result.isOk ->
                        Some { d with Confirmed = Some r; ConfirmedHead = Some head; Local = r; Reload = None }
                    | Some r -> Some { d with Remote = Conflicted(Some head, Some r, "The remote history differs from the copy on this device."); Reload = None }
                    | None when d.Confirmed.IsNone -> Some { d with Reload = None }
                    | None -> Some { d with Remote = Conflicted(Some head, None, "The remote copy is gone; the copy on this device is kept."); Reload = None }
                | Ahead
                | SendFailed _
                | Conflicted _ ->
                    if sameBytes d.Confirmed remote then
                        // Nothing changed remotely for this investigation: rebase onto the new head (§9.4.2 a).
                        Some { d with ConfirmedHead = Some head; Remote = Ahead; Reload = None }
                    else
                        let detail =
                            match remote with
                            | Some _ -> "Another writer changed this investigation. Your local changes are kept."
                            | None -> "The remote copy is gone or unreadable; the copy on this device is kept."
                        Some { d with Remote = Conflicted(Some head, remote, detail); Reload = None }
                | OutcomeUnknown a ->
                    let remoteShas =
                        remote |> Option.map (fun r -> FileSet.files r |> List.map (fun (n, b) -> n, GitHub.gitBlobSha b) |> List.sort)
                    if remoteShas = Some(List.sort a.BlobShas) then
                        let remote' = remote |> Option.get
                        Some
                            { d with
                                Confirmed = Some remote'
                                ConfirmedHead = Some head
                                Remote = (if d.Local.Version > remote'.Version then Ahead else InSync)
                                Reload = None }
                    elif sameBytes d.Confirmed remote then Some { d with ConfirmedHead = Some head; Remote = Ahead; Reload = None }
                    else Some { d with Remote = Conflicted(Some head, remote, "The repository changed while the outcome of a save was unknown."); Reload = None }
                | Sending _
                | Confirming _
                | Reconciling _ -> Some d
        match doc with
        | Some d -> { m with Documents = m.Documents |> Map.add id d }
        | None -> m

    let private finishLoad (m: Model, effects: Effect list) =
        match m.Load with
        | None -> m, effects
        | Some job when not job.Listed || not job.Dirs.IsEmpty -> m, effects
        | Some job ->
            let catalogIds = job.Manifest |> Option.map (fun man -> man.Cases |> List.map (fun c -> c.InvestigationId) |> Set.ofList)
            let isCatalog id = catalogIds |> Option.exists (Set.contains id)
            let m =
                job.Done
                |> Map.fold (fun m id r ->
                    match r with
                    | Ok v -> integrate job.Head id (Some v) (isCatalog id || job.Manifest.IsSome) m
                    | Error _ -> m) { m with Head = Some job.Head }
            // Writable documents this device knows but the workspace no longer lists.
            let m =
                m.Documents
                |> Map.fold (fun m id d ->
                    if not d.ReadOnly && not (job.Done.ContainsKey id) then integrate job.Head id None false m else m) m
            let errors = job.Done |> Map.toList |> List.choose (fun (id, r) -> match r with Error e -> Some(id, e) | Ok _ -> None)
            let catalog =
                match job.Manifest with
                | None -> NotCatalog
                | Some man ->
                    let results = man.Cases |> List.map (fun c -> Catalog.checkCase c (Map.tryFind c.InvestigationId job.Done))
                    let extras = job.Done |> Map.toList |> List.map fst |> List.filter (fun id -> not (isCatalog id))
                    match results |> List.tryPick (fun r -> match r.Outcome with Error e -> Some e | Ok _ -> None) with
                    | Some e -> CatalogFailed(e.Code + ": " + e.Detail)
                    | None when not extras.IsEmpty -> CatalogFailed "The catalog workspace holds investigations the catalog manifest does not list."
                    | None -> CatalogVerified(man, results)
            // A catalog that fails verification presents none of its cases (fail closed, EXT-DATA-013-O2).
            let m =
                match catalog with
                | CatalogFailed _ -> { m with Documents = m.Documents |> Map.filter (fun _ d -> not d.ReadOnly) }
                | _ -> m
            let connection =
                match catalog, errors with
                | CatalogFailed reason, _ -> Incompatible reason
                | _, (_, e) :: _ -> Incompatible(sprintf "%d investigation(s) failed validation; first: %s %s" errors.Length e.Code e.Detail)
                | _ when m.Documents.IsEmpty -> AuthorizedEmpty
                | _ -> Connected
            { m with Load = None; LoadErrors = errors; Catalog = catalog; Connection = connection }, effects

    let private startListing (head: string) (m: Model, effects: Effect list) =
        let m = { m with Load = m.Load |> Option.map (fun j -> { j with Head = head }) }
        send ListInvestigations (fun c t _ -> GitHub.listDirectory c t head (Target.investigationsDir t)) (m, effects)

    /// Answer to a directory listing or blob of one investigation (workspace load or reload).
    let private dirProgress (env: Env) (id: string) (reload: bool) (update: DirRead -> DirRead) (m: Model, effects: Effect list) =
        let current =
            if reload then
                m.Documents |> Map.tryFind id |> Option.bind (fun d -> match d.Reload with Some(ReloadDir r) -> Some r | _ -> None)
            else m.Load |> Option.bind (fun j -> Map.tryFind id j.Dirs)
        match current with
        | None -> m, effects
        | Some dir ->
            let dir = update dir
            let complete =
                match dir.Entries with
                | Some entries -> entries |> List.forall (fun e -> dir.Blobs.ContainsKey e.Name)
                | None -> false
            if complete then
                let raw = { DirectoryName = id; Entries = dir.Blobs |> Map.toList }
                let result = FileSet.read env.Engine raw
                if reload then
                    let m =
                        match result with
                        | Ok v -> integrate dir.Commit id (Some v) false m
                        | Error e ->
                            let m = { m with LoadErrors = m.LoadErrors @ [ id, e ] }
                            match Map.tryFind id m.Documents with
                            | Some d -> { m with Documents = m.Documents |> Map.add id { d with Reload = None; Remote = Conflicted(Some dir.Commit, None, "The remote copy failed validation; the copy on this device is kept.") } }
                            | None -> m
                    { m with Head = Some dir.Commit }, effects
                else
                    let m =
                        { m with
                            Load =
                                m.Load
                                |> Option.map (fun j -> { j with Dirs = j.Dirs |> Map.remove id; Done = j.Done |> Map.add id result }) }
                    finishLoad (m, effects)
            else
                let setDir m =
                    if reload then
                        { m with Documents = m.Documents |> Map.change id (Option.map (fun d -> { d with Reload = Some(ReloadDir dir) })) }
                    else
                        { m with Load = m.Load |> Option.map (fun j -> { j with Dirs = j.Dirs |> Map.add id dir }) }
                setDir m, effects

    let private failDir (env: Env) (id: string) (reload: bool) (e: ContractError) (m: Model, effects: Effect list) =
        if reload then
            let m =
                match Map.tryFind id m.Documents with
                | Some d -> { m with Documents = m.Documents |> Map.add id { d with Reload = None; Remote = Conflicted(None, None, "The remote copy could not be read; the copy on this device is kept.") } }
                | None -> m
            { m with LoadErrors = m.LoadErrors @ [ id, e ] }, effects
        else
            let m =
                { m with
                    Load = m.Load |> Option.map (fun j -> { j with Dirs = j.Dirs |> Map.remove id; Done = j.Done |> Map.add id (Error e) }) }
            ignore env
            finishLoad (m, effects)

    /// A credential GitHub rejected is dropped at once: nothing cached may mask it (PF-009-O2).
    let private credentialRejected (m: Model) =
        { m with
            Credential = None
            Generation = m.Generation + 1
            Connection = AccessDenied "GitHub rejected the credential. Reconnect with a valid token."
            Load = None
            InFlight = m.InFlight |> Map.filter (fun _ f -> match f.Purpose with Commit _ | ConfirmListing _ | ReconcileHead _ | ReconcileListing _ -> true | _ -> false)
            Documents = m.Documents |> Map.map (fun _ d -> { d with Reload = None }) }

    let private failureConnection (f: ProviderFailure) =
        match f with
        | Unauthorized -> AccessDenied(ProviderFailure.describe f)
        | Forbidden _
        | NotFound -> AccessDenied(ProviderFailure.describe f)
        | MissingRef _ -> Incompatible(ProviderFailure.describe f)
        | Malformed _ -> Incompatible(ProviderFailure.describe f)
        | _ -> Offline(ProviderFailure.describe f)

    /// A read failed during the connect/load phase: the connection takes the failure's state; nothing is loaded.
    let private loadFailed (f: ProviderFailure) (m: Model, effects: Effect list) =
        let m = if f = Unauthorized then credentialRejected m else m
        { m with Load = None; Connection = failureConnection f }, effects

    // ---------- publication ----------

    let private blobShas (v: Verified) = FileSet.files v |> List.map (fun (n, b) -> n, GitHub.gitBlobSha b)

    let private setDoc (id: string) (f: Document -> Document) (m: Model) =
        { m with Documents = m.Documents |> Map.change id (Option.map f) }

    let private afterConfirmed (env: Env) (id: string) (a: Attempt) (commit: string) (m: Model, effects: Effect list) =
        let m =
            m
            |> setDoc id (fun d ->
                { d with
                    Confirmed = Some a.Sent
                    ConfirmedHead = Some commit
                    Remote = (if d.Local.Version > a.Sent.Version then Ahead else InSync)
                    LastSyncedAt = Some(env.Now()) })
        persistLocal [ id ] ({ m with Head = Some commit }, effects)

    let private toUnknown (id: string) (a: Attempt) (m: Model, effects: Effect list) =
        persistLocal [ id ] (m |> setDoc id (fun d -> { d with Remote = OutcomeUnknown a }), effects)

    // ---------- the step function ----------

    let private answered (env: Env) (requestId: string) (outcome: Result<HttpResponse, TransportFailure>) (m: Model) : Step =
        match Map.tryFind requestId m.InFlight with
        | None -> refuse "The answer is not for a request in flight."
        | Some f when f.Generation <> m.Generation && (match f.Purpose with Commit _ | ConfirmListing _ | ReconcileHead _ | ReconcileListing _ -> false | _ -> true) ->
            refuse "The answer belongs to an earlier connection."
        | Some f ->
            let m = { m with InFlight = m.InFlight |> Map.remove requestId }
            let st = m, []
            let asFailure (t: TransportFailure) =
                match t with
                | NotSent d -> ProviderFailure.Offline d
                | TransportFailure.OutcomeUnknown d -> ProviderFailure.Unknown d
                | Cancelled -> ProviderFailure.Offline "cancelled"
            let read (parse: JValue -> Result<'T, ProviderFailure>) =
                match outcome with
                | Ok r -> GitHub.classify r |> Result.bind parse
                | Error t -> Error(asFailure t)
            match f.Purpose with
            | ProbeRepository ->
                match read GitHub.parseRepository with
                | Ok info -> Ok(send ProbeHead (fun c t id -> GitHub.getBranchHead c t id) ({ m with Repository = Some info }, []))
                | Error e -> Ok(loadFailed e st)
            | ProbeHead ->
                match read GitHub.parseHead with
                | Ok head ->
                    let m = { m with Connection = Loading; Load = Some { Head = head; Pointer = None; Manifest = None; Listed = false; Dirs = Map.empty; Done = Map.empty } }
                    Ok(send ReadPointer (fun c t _ -> GitHub.getFile c t head Catalog.pointerPath) (m, []))
                | Error e -> Ok(loadFailed e st)
            | ReadPointer ->
                let head = m.Load |> Option.map (fun j -> j.Head) |> Option.defaultValue ""
                match read GitHub.parseFile with
                | Error NotFound -> Ok(startListing head st)
                | Error e -> Ok(loadFailed e st)
                | Ok bytes ->
                    match Catalog.parsePointer bytes, m.Target with
                    | Ok p, Some t when p.Workspace = Target.workspaceRoot t ->
                        let m = { m with Catalog = CatalogLoading p; Load = m.Load |> Option.map (fun j -> { j with Pointer = Some p }) }
                        Ok(send ReadCatalogManifest (fun c t _ -> GitHub.getFile c t head p.ManifestPath) (m, []))
                    | Ok _, _ -> Ok(startListing head st)
                    | Error e, _ -> Ok({ m with Load = None; Catalog = CatalogFailed(e.Code + ": " + e.Detail); Connection = Incompatible e.Detail }, [])
            | ReadCatalogManifest ->
                let head = m.Load |> Option.map (fun j -> j.Head) |> Option.defaultValue ""
                match read GitHub.parseFile, m.Load |> Option.bind (fun j -> j.Pointer) with
                | Ok bytes, Some p ->
                    match Catalog.parseManifest p bytes with
                    | Ok man -> Ok(startListing head ({ m with Load = m.Load |> Option.map (fun j -> { j with Manifest = Some man }) }, []))
                    | Error e -> Ok({ m with Load = None; Catalog = CatalogFailed(e.Code + ": " + e.Detail); Connection = Incompatible e.Detail }, [])
                | Error e, _ -> Ok(loadFailed e st)
                | Ok _, None -> refuse "No catalog pointer is being read."
            | ListInvestigations ->
                let head = m.Load |> Option.map (fun j -> j.Head) |> Option.defaultValue ""
                let entries =
                    match read GitHub.parseListing with
                    | Error NotFound -> Ok []
                    | other -> other
                match entries with
                | Error e -> Ok(loadFailed e st)
                | Ok entries ->
                    let dirs = entries |> List.filter (fun e -> e.Kind = "dir")
                    let invalid = entries |> List.filter (fun e -> e.Kind <> "dir" || not (Uuid.isValid e.Name))
                    let m =
                        { m with
                            Load =
                                m.Load
                                |> Option.map (fun j ->
                                    { j with
                                        Listed = true
                                        Dirs = dirs |> List.filter (fun e -> Uuid.isValid e.Name) |> List.map (fun e -> e.Name, { InvestigationId = e.Name; Commit = head; Entries = None; Blobs = Map.empty }) |> Map.ofList
                                        Done =
                                            invalid
                                            |> List.map (fun e -> e.Name, Error(ContractError.make Codes.pathInvalid "an entry in the investigations directory is not a canonical investigation directory"))
                                            |> Map.ofList }) }
                    let st =
                        dirs
                        |> List.filter (fun e -> Uuid.isValid e.Name)
                        |> List.fold (fun st e -> send (ListDir(e.Name, false)) (fun c t _ -> GitHub.listDirectory c t head (Target.investigationDir t e.Name)) st) (m, [])
                    Ok(finishLoad st)
            | ListDir(id, reload) ->
                match read GitHub.parseListing with
                | Error e when e = Unauthorized -> Ok(loadFailed e st)
                | Error e -> Ok(failDir env id reload (ContractError.make Codes.pathInvalid ("the investigation directory could not be listed: " + ProviderFailure.describe e)) st)
                | Ok entries ->
                    let commit =
                        if reload then m.Documents |> Map.tryFind id |> Option.bind (fun d -> match d.Reload with Some(ReloadDir r) -> Some r.Commit | _ -> None)
                        else m.Load |> Option.map (fun j -> j.Head)
                    let files = entries |> List.filter (fun e -> e.Kind = "file")
                    if files.Length <> entries.Length || (files |> List.map (fun e -> e.Name) |> List.sort) <> List.sort FileSet.names then
                        Ok(failDir env id reload (ContractError.make Codes.pathInvalid "the investigation directory must contain exactly events.ndjson, manifest.json and snapshot.json") st)
                    else
                        let st = dirProgress env id reload (fun d -> { d with Entries = Some files }) st
                        ignore commit
                        Ok(files |> List.fold (fun st e -> send (ReadBlob(id, e.Name, reload)) (fun c t _ -> GitHub.getBlob c t e.Sha) st) st)
            | ReadBlob(id, name, reload) ->
                match read GitHub.parseBlob with
                | Error e when e = Unauthorized -> Ok(loadFailed e st)
                | Error e -> Ok(failDir env id reload (ContractError.make Codes.pathInvalid ("a file could not be read: " + ProviderFailure.describe e)) st)
                | Ok bytes -> Ok(dirProgress env id reload (fun d -> { d with Blobs = d.Blobs |> Map.add name bytes }) st)
            | Commit id ->
                match Map.tryFind id m.Documents with
                | Some({ Remote = Sending a }) ->
                    let result =
                        match outcome with
                        | Ok r -> GitHub.parseCommit r
                        | Error t -> Error(asFailure t)
                    match result with
                    | Ok commit ->
                        let m = m |> setDoc id (fun d -> { d with Remote = Confirming(a, commit) })
                        Ok(send (ConfirmListing id) (fun c t _ -> GitHub.listDirectory c t commit (Target.investigationDir t id)) (m, []))
                    | Error(HeadMoved detail) ->
                        Ok(persistLocal [ id ] (m |> setDoc id (fun d -> { d with Remote = Conflicted(None, None, "Another writer moved the branch; nothing was written. Reload the remote copy; your changes are kept. (" + detail + ")") }), []))
                    | Error Unauthorized ->
                        let m = m |> setDoc id (fun d -> { d with Remote = SendFailed Unauthorized }) |> credentialRejected
                        Ok(persistLocal [ id ] (m, []))
                    | Error(ProviderFailure.Unknown _ as e)
                    | Error(Unavailable _ as e)
                    | Error(Malformed _ as e) ->
                        ignore e
                        Ok(toUnknown id a st)
                    | Error(ProviderFailure.Offline _ as e) ->
                        Ok(persistLocal [ id ] ({ m with Connection = Offline(ProviderFailure.describe e) } |> setDoc id (fun d -> { d with Remote = SendFailed e }), []))
                    | Error e -> Ok(persistLocal [ id ] (m |> setDoc id (fun d -> { d with Remote = SendFailed e }), []))
                | _ -> refuse "No save of this investigation is in flight."
            | ConfirmListing id ->
                match Map.tryFind id m.Documents with
                | Some({ Remote = Confirming(a, commit) }) ->
                    match read GitHub.parseListing with
                    | Ok entries ->
                        let found = entries |> List.map (fun e -> e.Name, e.Sha) |> List.sort
                        if found = List.sort a.BlobShas then Ok(afterConfirmed env id a commit st)
                        else Ok(toUnknown id a st)
                    | Error _ -> Ok(toUnknown id a st)
                | _ -> refuse "No confirmation of this investigation is in flight."
            | ReconcileHead id ->
                match Map.tryFind id m.Documents with
                | Some({ Remote = Reconciling(a, ReadingHead _) }) ->
                    match read GitHub.parseHead with
                    | Ok head ->
                        let (m2, e2), rid = sendWithId (ReconcileListing id) (fun c t _ -> GitHub.listDirectory c t head (Target.investigationDir t id)) st
                        Ok(m2 |> setDoc id (fun d -> { d with Remote = Reconciling(a, ReadingListing(rid, head)) }), e2)
                    | Error Unauthorized -> Ok(m |> setDoc id (fun d -> { d with Remote = OutcomeUnknown a }) |> credentialRejected, [])
                    | Error _ -> Ok(m |> setDoc id (fun d -> { d with Remote = OutcomeUnknown a }), [])
                | _ -> refuse "No reconciliation of this investigation is in flight."
            | ReconcileListing id ->
                match Map.tryFind id m.Documents with
                | Some({ Remote = Reconciling(a, ReadingListing(_, head)) } as d) ->
                    let listing =
                        match read GitHub.parseListing with
                        | Error NotFound -> Ok []
                        | other -> other
                    match listing with
                    | Ok entries ->
                        let found = entries |> List.map (fun e -> e.Name, e.Sha) |> List.sort
                        let confirmedShas = d.Confirmed |> Option.map (blobShas >> List.sort) |> Option.defaultValue []
                        if found = List.sort a.BlobShas then Ok(afterConfirmed env id a head st)
                        elif found = confirmedShas then
                            // Not applied: the copy there is still the one this device confirmed. Safe to send again.
                            Ok(persistLocal [ id ] ({ m with Head = Some head } |> setDoc id (fun d -> { d with Remote = Ahead; ConfirmedHead = Some head }), []))
                        else
                            Ok(persistLocal [ id ] (m |> setDoc id (fun d -> { d with Remote = Conflicted(Some head, None, "The repository holds a different version of this investigation. Reload it; your changes are kept.") }), []))
                    | Error Unauthorized -> Ok(m |> setDoc id (fun d -> { d with Remote = OutcomeUnknown a }) |> credentialRejected, [])
                    | Error _ -> Ok(m |> setDoc id (fun d -> { d with Remote = OutcomeUnknown a }), [])
                | _ -> refuse "No reconciliation of this investigation is in flight."
            | ReloadHeadRead id ->
                match Map.tryFind id m.Documents with
                | Some({ Reload = Some(ReloadHead _) }) ->
                    match read GitHub.parseHead with
                    | Ok head ->
                        let m = m |> setDoc id (fun d -> { d with Reload = Some(ReloadDir { InvestigationId = id; Commit = head; Entries = None; Blobs = Map.empty }) })
                        Ok(send (ListDir(id, true)) (fun c t _ -> GitHub.listDirectory c t head (Target.investigationDir t id)) (m, []))
                    | Error Unauthorized -> Ok(credentialRejected m, [])
                    | Error e -> Ok(m |> setDoc id (fun d -> { d with Reload = None }) |> fun m -> { m with Connection = failureConnection e }, [])
                | _ -> refuse "No reload of this investigation is in flight."

    let private canWrite (m: Model) =
        match m.Connection with
        | Connected
        | AuthorizedEmpty -> m.Credential.IsSome && m.Target.IsSome
        | _ -> false

    /// The single entry point: every load, edit, save, confirmation and reconciliation goes through here.
    /// A refused step returns Error and the caller keeps the previous model (nothing changes, nothing is sent).
    let update (env: Env) (msg: Msg) (m: Model) : Step =
        match msg with
        | Restore stored ->
            if m <> initial then refuse "Restore is only possible at start-up."
            else
                match stored with
                | None -> Ok(m, [])
                | Some text ->
                    match parseStored env text with
                    | Ok(target, docs) ->
                        Ok({ m with Target = target; Documents = docs |> List.map (fun d -> d.InvestigationId, d) |> Map.ofList }, [])
                    | Error e -> refuse ("The local copy could not be restored and was left untouched: " + e)
        | Connect(target, credential) ->
            if anyUnsettled m then refuse "A save or reload is still settling; wait for it before connecting again."
            elif m.Target.IsSome && m.Target <> Some target && m.Documents |> Map.exists (fun _ d -> not d.ReadOnly && d.Remote <> InSync) then
                refuse "Unsaved changes belong to the currently selected workspace; save or discard them before selecting another."
            else
                let documents = if m.Target = Some target then m.Documents else Map.empty
                let m = { m with Documents = documents }
                let m =
                    { m with
                        Target = Some target
                        Credential = Some credential
                        Generation = m.Generation + 1
                        Connection = Connecting
                        Repository = None
                        Head = None
                        Load = None
                        Catalog = NotCatalog
                        LoadErrors = []
                        InFlight = Map.empty
                        Documents = m.Documents |> Map.filter (fun _ d -> not d.ReadOnly) }
                Ok(send ProbeRepository (fun c t _ -> GitHub.getRepository c t) (m, []))
        | Disconnect ->
            if anyUnsettled m then refuse "A save is still settling; disconnecting now could lose track of it."
            else
                Ok(
                    { m with
                        Credential = None
                        Generation = m.Generation + 1
                        Connection = Disconnected
                        Load = None
                        Head = None
                        Catalog = NotCatalog
                        InFlight = Map.empty
                        Documents = m.Documents |> Map.filter (fun _ d -> not d.ReadOnly) },
                    []
                )
        | Create(id, events) ->
            match m.Target with
            | None -> refuse "Select a data repository and workspace before creating an investigation."
            | Some _ when isCatalogWorkspace m -> refuse "The historical catalog workspace is read-only."
            | Some _ when m.Documents.ContainsKey id -> refuse "An investigation with this id already exists."
            | Some _ when not (Uuid.isValid id) -> refuse "The investigation id is not a canonical UUID."
            | Some _ ->
                match FileSet.append env.Engine id None 0L events with
                | Error e -> refuse (e.Code + ": " + e.Detail)
                | Ok local ->
                    let d =
                        { InvestigationId = id
                          ReadOnly = false
                          Confirmed = None
                          ConfirmedHead = m.Head
                          Local = local
                          Remote = Ahead
                          Durability = NotSaved
                          LastSyncedAt = None
                          Reload = None }
                    Ok(persistLocal [ id ] ({ m with Documents = m.Documents |> Map.add id d }, []))
        | Edit(id, expectedVersion, events) ->
            match Map.tryFind id m.Documents with
            | None -> refuse "There is no such investigation."
            | Some d when d.ReadOnly -> refuse "This investigation is read-only."
            | Some d ->
                match FileSet.append env.Engine id (Some d.Local) expectedVersion events with
                | Error e -> refuse (e.Code + ": " + e.Detail)
                | Ok local ->
                    let remote =
                        match d.Remote with
                        | InSync -> Ahead
                        | other -> other
                    Ok(persistLocal [ id ] (m |> setDoc id (fun d -> { d with Local = local; Remote = remote; Durability = NotSaved }), []))
        | Save id ->
            match Map.tryFind id m.Documents with
            | None -> refuse "There is no such investigation."
            | Some d when d.ReadOnly -> refuse "This investigation is read-only."
            | Some d ->
                match d.Remote with
                | InSync -> refuse "Nothing to save."
                | OutcomeUnknown _ -> refuse "The last save may have been applied; check the repository (Reconcile) before saving again."
                | Conflicted _ -> refuse "Another writer changed the repository; reload the remote copy before saving."
                | Sending _
                | Confirming _
                | Reconciling _ -> refuse "A save is already in flight."
                | Ahead
                | SendFailed _ ->
                    if not (canWrite m) then refuse "Connect to the data repository before saving; the change stays on this device."
                    elif d.Reload.IsSome then refuse "A reload of this investigation is in flight."
                    else
                        match d.ConfirmedHead |> Option.orElse m.Head with
                        | None -> refuse "The branch head is not known; reconnect first."
                        | Some baseHead ->
                            let headline = sprintf "causa: %s v%d" id d.Local.Version
                            let files t = FileSet.files d.Local |> List.map (fun (n, b) -> Target.investigationDir t id + "/" + n, b)
                            match m.Target with
                            | Some t when files t |> List.forall (fst >> Target.pathAllowed t) ->
                                let (m2, effects), rid = sendWithId (Commit id) (fun c t _ -> GitHub.createCommit c t baseHead headline (files t)) (m, [])
                                let attempt = { RequestId = rid; Base = baseHead; Sent = d.Local; BlobShas = blobShas d.Local }
                                Ok(persistLocal [ id ] (m2 |> setDoc id (fun d -> { d with Remote = Sending attempt }), effects))
                            | _ -> refuse "The files would be written outside the selected workspace."
        | Reconcile id ->
            match Map.tryFind id m.Documents with
            | Some({ Remote = OutcomeUnknown a }) ->
                if not (canWrite m) then refuse "Connect to the data repository to check what happened to the last save."
                else
                    let (m2, effects), rid = sendWithId (ReconcileHead id) (fun c t nonce -> GitHub.getBranchHead c t nonce) (m, [])
                    Ok(m2 |> setDoc id (fun d -> { d with Remote = Reconciling(a, ReadingHead rid) }), effects)
            | Some _ -> refuse "Only a save whose outcome is unknown needs reconciling."
            | None -> refuse "There is no such investigation."
        | ReloadRemote id ->
            match Map.tryFind id m.Documents with
            | None -> refuse "There is no such investigation."
            | Some d when unsettled d -> refuse "A save or reload of this investigation is in flight."
            | Some { Remote = OutcomeUnknown _ } -> refuse "Reconcile the unknown save first."
            | Some _ when m.Credential.IsNone || m.Target.IsNone || (match m.Connection with Connecting | Loading | Disconnected -> true | _ -> false) ->
                refuse "Connect to the data repository first."
            | Some _ ->
                let (m2, effects), rid = sendWithId (ReloadHeadRead id) (fun c t nonce -> GitHub.getBranchHead c t nonce) (m, [])
                Ok(m2 |> setDoc id (fun d -> { d with Reload = Some(ReloadHead rid) }), effects)
        | DiscardLocal id ->
            match Map.tryFind id m.Documents with
            | None -> refuse "There is no such investigation."
            | Some d when d.ReadOnly -> refuse "This investigation is read-only."
            | Some d ->
                match d.Remote with
                | OutcomeUnknown _ -> refuse "The last save may have been applied; reconcile before discarding."
                | Sending _
                | Confirming _
                | Reconciling _ -> refuse "A save is in flight."
                | InSync -> refuse "There are no local changes to discard."
                | Conflicted(Some head, Some remote, _) ->
                    Ok(persistLocal [ id ] (m |> setDoc id (fun d -> { d with Confirmed = Some remote; ConfirmedHead = Some head; Local = remote; Remote = InSync }), []))
                | Conflicted _
                | Ahead
                | SendFailed _ ->
                    match d.Confirmed with
                    | Some c -> Ok(persistLocal [ id ] (m |> setDoc id (fun d -> { d with Local = c; Remote = InSync }), []))
                    | None ->
                        let m = { m with Documents = m.Documents |> Map.remove id }
                        Ok(persistLocal [] (m, []))
        | HttpAnswered(rid, r) -> answered env rid (Ok r) m
        | HttpFailed(rid, f) -> answered env rid (Error f) m
        | LocalStored(writeId, ok) ->
            let matching = m.Documents |> Map.exists (fun _ d -> d.Durability = SavingLocally writeId)
            if not matching then refuse "The local write is not the latest one."
            else
                Ok(
                    { m with
                        Documents =
                            m.Documents
                            |> Map.map (fun _ d ->
                                if d.Durability = SavingLocally writeId then
                                    { d with Durability = (if ok then SavedLocally else LocalSaveFailed "The browser refused to store the change; it exists only in this tab.") }
                                else d) },
                    []
                )

    // ---------- projection (what the UI may claim) ----------

    /// UI-006-O1 distinct states, plus `unknown` (CONTRACT §9.4.3).
    let documentStatus (m: Model) (d: Document) =
        match d.Remote, d.Durability with
        | Conflicted _, _ -> "conflict"
        | _, LocalSaveFailed _ -> "failed"
        | SendFailed _, _ -> "failed"
        | OutcomeUnknown _, _
        | Reconciling _, _ -> "unknown"
        | Sending _, _
        | Confirming _, _ -> "syncingRemotely"
        | _, SavingLocally _ -> "savingLocally"
        | _, NotSaved -> "unsaved"
        | Ahead, SavedLocally ->
            match m.Connection with
            | Offline _
            | Disconnected -> "queuedOffline"
            | _ -> "savedLocally"
        | InSync, _ -> if d.LastSyncedAt.IsSome then "remotelySynced" else "clean"

    /// EXT-DATA-007-O1: the eight connection states. A document conflict is shown as the connection's conflict.
    let connectionLabel (m: Model) =
        match m.Connection with
        | Connected
        | AuthorizedEmpty when m.Documents |> Map.exists (fun _ d -> match d.Remote with Conflicted _ -> true | _ -> false) -> "conflict"
        | Disconnected -> "disconnected"
        | Connecting -> "connecting"
        | Loading -> "loading"
        | AuthorizedEmpty -> "authorizedEmpty"
        | Connected -> "connected"
        | Incompatible _ -> "incompatible"
        | AccessDenied _ -> "accessDenied"
        | Offline _ -> "offline"

    let connectionDetail (m: Model) =
        match m.Connection with
        | Incompatible r
        | AccessDenied r
        | Offline r -> Some r
        | _ -> None

    let admissibilityLabel (v: Verified) =
        match v.Admissibility with
        | Admissible -> "verified"
        | Inadmissible _ -> "inadmissible"
        | NotEvaluated _ -> "structurallyValidAdmissibilityNotEvaluated"

    let canSave (m: Model) (d: Document) =
        not d.ReadOnly && canWrite m && d.Reload.IsNone && (match d.Remote with Ahead | SendFailed _ -> true | _ -> false)

    let canReconcile (m: Model) (d: Document) = canWrite m && (match d.Remote with OutcomeUnknown _ -> true | _ -> false)
