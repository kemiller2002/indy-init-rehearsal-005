namespace Causa.Persistence

open System

/// Core's request book (Boundary D): the single authority for every write-path transition of one document.
module Book = IndyInitRootCauseInvestigator.Engine.Sync

type CommandEnvelope = IndyInitRootCauseInvestigator.Engine.CommandEnvelope

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

type Durability =
    | NotSaved
    | SavingLocally of writeId: string
    | SavedLocally
    | LocalSaveFailed of detail: string

/// A remote copy that differs from what this device last confirmed, found by a read (load or reload). The local
/// change is kept (UI-006-O4); the person discards it or decides again on the remote copy.
type RemoteConflict =
    { Head: string
      Remote: Verified option
      Detail: string }

/// Reading one investigation directory at a commit: listing, then each blob.
type DirRead =
    { InvestigationId: string
      Commit: string
      Entries: ListedEntry list option
      Blobs: Map<string, byte[]> }

type ReloadJob =
    | ReloadHead of requestId: string
    | ReloadDir of DirRead

type Document =
    { InvestigationId: string
      /// Historical catalog and frozen data are read-only (UI-006-O5).
      ReadOnly: bool
      /// The last remotely confirmed file set and the commit it was confirmed at.
      Confirmed: Verified option
      ConfirmedHead: string option
      /// The local copy: Confirmed plus local changes, verified by the engine.
      Local: Verified
      /// Core's request book for this document: publication, confirmation, conflict and unknown outcome.
      Book: Book.State
      RemoteConflict: RemoteConflict option
      Durability: Durability
      LastSyncedAt: string option
      Reload: ReloadJob option }

type CatalogStatus =
    | NotCatalog
    | CatalogLoading of CatalogPointer
    | CatalogVerified of CatalogManifest * CatalogCaseResult list
    | CatalogFailed of reason: string

/// The workspace load after Connect (this program owns loading: Rehearsal 4's lesson).
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
    | ReloadHeadRead of investigationId: string
    /// The book's requests, performed for it and answered to it by its own request id.
    | BookPublish of investigationId: string * Book.RequestId
    | BookReadCommit of investigationId: string * Book.RequestId * expected: (string * string) list
    | BookReadHead of investigationId: string * Book.RequestId * since: string
    | BookCompare of investigationId: string * Book.RequestId * since: string * head: string

type InFlight = { Purpose: Purpose; Generation: int }

type Model =
    { Target: Target option
      /// Memory only. Never stored, logged, projected or put anywhere but the Authorization header.
      Credential: Credential option
      /// Incremented by Connect, Disconnect and credential rejection: read answers of an older generation are refused.
      Generation: int
      Connection: Connection
      Repository: RepositoryInfo option
      Head: string option
      Load: LoadJob option
      Catalog: CatalogStatus
      Documents: Map<string, Document>
      LoadErrors: (string * ContractError) list
      InFlight: Map<string, InFlight>
      NextId: int
      /// The page-load nonce (Env.Nonce) of this session; mutable-read URLs carry it with the request id.
      Nonce: string }

type Msg =
    /// Restore after a restart from the local store's text (None: nothing stored). First message only.
    | Restore of stored: string option
    | Connect of Target * Credential
    | Disconnect
    /// A new investigation: its start command, decided by the engine (Boundary A).
    | Create of CommandEnvelope
    /// A command on an open investigation, decided by the engine against the local copy (Boundary A).
    | Submit of CommandEnvelope
    /// Events the engine already accepted for a command the caller decided with Kernel.submit at `expected`
    /// (version 0 with no local copy: a new investigation). Serialised and verified by the storage writer.
    | Append of investigationId: string * expected: int64 * events: IndyInitRootCauseInvestigator.Engine.DomainEvent list
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
    { Now: unit -> string
      /// Unique per page load (and so per session): every mutable read's URL carries it with the request id, so no
      /// cache can answer a branch-head or repository read with an earlier one (DC-005).
      Nonce: string }

[<RequireQualifiedAccess>]
module Sync =
    let storageKey = "causa.persistence/2"

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
          NextId = 1
          Nonce = "" }

    type private Step = Result<Model * Effect list, Refusal>

    let private refuse reason : Step = Error { Reason = reason }

    let private nextId (m: Model) = sprintf "r%d" m.NextId, { m with NextId = m.NextId + 1 }

    let private send (purpose: Purpose) (build: Credential option -> Target -> string -> HttpRequest) (m: Model, effects: Effect list) =
        match m.Target with
        | None -> m, effects
        | Some t ->
            let id, m = nextId m
            { m with InFlight = m.InFlight |> Map.add id { Purpose = purpose; Generation = m.Generation } },
            effects @ [ Send(id, build m.Credential t (m.Nonce + "-" + id)) ]

    let private sendWithId purpose build st =
        let m, _ = st
        let id = sprintf "r%d" m.NextId
        send purpose build st, id

    let private setDoc (id: string) (f: Document -> Document) (m: Model) =
        { m with Documents = m.Documents |> Map.change id (Option.map f) }

    // ---------- the book (core Sync.step) ----------

    let oidText (Book.Oid s) = s

    /// A new book observing `head` (core's attach on its initial state).
    let private bookAt (head: string) : Book.State = Book.attach (Book.Oid head) Book.initial

    let private stepOr (b: Book.State) (e: Book.Event) =
        match Book.step b e with
        | Ok(b2, _) -> b2
        | Error _ -> b

    /// Re-observe the branch head through core's own transitions: attach after a restore, Reloaded otherwise. A
    /// pending change decided at another head becomes a held conflict there (core: never re-targeted, DC-001).
    let private observe (head: string) (book: Book.State) : Book.State =
        let h = Book.Oid head
        match book.Phase with
        | Book.Disconnected -> Book.attach h book
        | Book.Idle _
        | Book.Held(_, _, Book.Conflict _) -> stepOr book (Book.Reloaded h)
        | Book.Held(_, _, Book.NotWritten _) -> stepOr (stepOr book Book.Discard) (Book.Reloaded h)
        | _ -> book

    let private bookInFlight (s: Book.State) =
        match s.Phase with
        | Book.Writing _
        | Book.Confirming _
        | Book.Reconciling _ -> true
        | _ -> false

    let private bookUnknown (s: Book.State) =
        match s.Phase with
        | Book.Unknown _ -> true
        | _ -> false

    let private bookHeld (s: Book.State) =
        match s.Phase with
        | Book.Held _ -> true
        | _ -> false

    let private pending (d: Document) =
        match d.Confirmed with
        | None -> true
        | Some c -> not (Store.sameBytes c d.Local)

    let private filesFor (t: Target) (v: Verified) =
        Store.files v |> List.map (fun (n, b) -> Target.investigationDir t v.InvestigationId + "/" + n, b)

    let private changeOf (t: Target) (marker: string) (decidedAt: string) (v: Verified) : Book.Change =
        { DecidedAt = Book.Oid decidedAt
          Marker = marker
          Files = filesFor t v |> List.map (fun (p, b) -> { Book.FileWrite.Path = p; Book.FileWrite.Bytes = Json.strictUtf8.GetString b }) }

    /// After observing `head`, when the investigation's remote files are the ones this device confirmed: the local
    /// change is handed back by the book (Discard -> Released) and reapplied as decided at that head.
    let private offer (t: Target) (marker: string) (head: string) (local: Verified) (book: Book.State) : Book.State =
        let reapply b = stepOr b (Book.Reapply(changeOf t marker head local))
        match book.Phase with
        | Book.Held(_, _, Book.Conflict _) -> reapply (stepOr book Book.Discard)
        | Book.Idle(_, None) -> reapply book
        | _ -> book

    let unsettled (d: Document) = bookInFlight d.Book || d.Reload.IsSome

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

    let private filesJson (v: Verified) =
        JObj
            [ "manifest", JStr(Json.strictUtf8.GetString v.Manifest)
              "events", JStr(Json.strictUtf8.GetString v.EventsFile)
              "snapshot", JStr(Json.strictUtf8.GetString v.Snapshot) ]

    /// The book's own durable form (core Sync.encode, causa.sync-state/1, no credentials).
    let private bookRecord (b: Book.State) = JStr(IndyInitRootCauseInvestigator.Engine.Json.canonical (Book.encode b))

    /// The durable local record: target (no credential) and, per writable document, the confirmed files and the
    /// commit they were confirmed at, the local files, the book state and the last sync time.
    let record (m: Model) : string =
        let target =
            match m.Target with
            | Some t -> JObj [ "owner", JStr t.Owner; "repo", JStr t.Repo; "branch", JStr t.Branch; "workspace", JStr t.Workspace ]
            | None -> JNull
        let opt f v =
            match v with
            | Some x -> f x
            | None -> JNull
        let doc (d: Document) =
            JObj
                [ "investigationId", JStr d.InvestigationId
                  "confirmedHead", opt JStr d.ConfirmedHead
                  "confirmed", opt filesJson d.Confirmed
                  "local", filesJson d.Local
                  "book", bookRecord d.Book
                  "conflict", opt (fun (c: RemoteConflict) -> JObj [ "head", JStr c.Head; "detail", JStr c.Detail ]) d.RemoteConflict
                  "lastSyncedAt", opt JStr d.LastSyncedAt ]
        Json.canonicalText (
            JObj
                [ "schema", JStr "causa.local-store/2"
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

    let private parseStored (text: string) : Result<Target option * Document list, string> =
        let filesOf (o: (string * JValue) list) id =
            match Json.str "manifest" o, Json.str "events" o, Json.str "snapshot" o with
            | Some mf, Some ev, Some sn ->
                Store.read
                    { DirectoryName = id
                      Entries =
                        [ Store.manifestName, Json.strictUtf8.GetBytes mf
                          Store.eventsName, Json.strictUtf8.GetBytes ev
                          Store.snapshotName, Json.strictUtf8.GetBytes sn ] }
                |> Result.mapError (fun e -> e.Code + ": " + e.Detail)
            | _ -> Error "stored files are incomplete"
        match Json.parse text with
        | Ok(JObj o) when Json.str "schema" o = Some "causa.local-store/2" ->
            let target =
                match Json.field "target" o with
                | Some(JObj t) ->
                    match Json.str "owner" t, Json.str "repo" t, Json.str "branch" t, Json.str "workspace" t with
                    | Some a, Some b, Some c, Some d -> Target.create a b c d |> Result.toOption
                    | _ -> None
                | _ -> None
            let doc (dv: JValue) =
                match dv, target with
                | JObj d, Some t ->
                    let id = Json.str "investigationId" d |> Option.defaultValue ""
                    let confirmed =
                        match Json.field "confirmed" d with
                        | Some(JObj f) -> filesOf f id |> Result.map Some
                        | _ -> Ok None
                    let local =
                        match Json.field "local" d with
                        | Some(JObj f) -> filesOf f id
                        | _ -> Error "a stored document has no local files"
                    match confirmed, local with
                    | Error e, _
                    | _, Error e -> Error e
                    | Ok c, Ok l ->
                        match c |> Option.map (fun c -> Store.successor c l) with
                        | Some(Error e) -> Error("the stored local copy does not extend its confirmed copy: " + e.Code)
                        | _ ->
                            // Core's decode + restore: anything in flight becomes Unknown; the counter is kept.
                            let book =
                                match Json.str "book" d with
                                | Some text ->
                                    match IndyInitRootCauseInvestigator.Engine.Json.parse text |> Result.bind Book.decode with
                                    | Ok b -> Book.restore b
                                    | Error _ -> Book.initial
                                | None -> Book.initial
                            let conflict =
                                match Json.field "conflict" d with
                                | Some(JObj c) ->
                                    Some
                                        { Head = Json.str "head" c |> Option.defaultValue ""
                                          Remote = None
                                          Detail = Json.str "detail" c |> Option.defaultValue "A conflict was found before the restart; reload the remote copy." }
                                | _ -> None
                            Ok
                                { InvestigationId = id
                                  ReadOnly = false
                                  Confirmed = c
                                  ConfirmedHead = Json.str "confirmedHead" d
                                  Local = l
                                  Book = book
                                  RemoteConflict = conflict
                                  Durability = SavedLocally
                                  LastSyncedAt = Json.str "lastSyncedAt" d
                                  Reload = None }
                | _ -> Error "stored document without a target"
            match Json.field "documents" o with
            | Some(JArr ds) -> ds |> traverse doc |> Result.map (fun ds -> target, ds)
            | _ -> Ok(target, [])
        | Ok _ -> Error "the local store holds an unknown record format"
        | Error e -> Error("the local store is unreadable: " + e)

    // ---------- the book's effects, performed by this program ----------

    let private headline (id: string) (v: Verified) = sprintf "causa: %s v%d" id v.Version

    let private blobShasOf (files: Book.FileWrite list) =
        files
        |> List.map (fun f ->
            let name = f.Path.Substring(f.Path.LastIndexOf '/' + 1)
            name, GitHub.gitBlobSha (Json.strictUtf8.GetBytes f.Bytes))
        |> List.sort

    let rec private bookEffects (id: string) (effects: Book.Effect list) (st: Model * Effect list) : Model * Effect list =
        effects
        |> List.fold (fun st e ->
            match e with
            | Book.Publish(r, parent, marker, files) ->
                let m, _ = st
                let doc = Map.tryFind id m.Documents
                let title = doc |> Option.map (fun d -> headline id d.Local) |> Option.defaultValue ("causa: " + id)
                send
                    (BookPublish(id, r))
                    (fun c t _ ->
                        GitHub.createCommit c t (oidText parent) title (GitHub.markerTrailer + marker)
                            (files |> List.map (fun f -> f.Path, Json.strictUtf8.GetBytes f.Bytes)))
                    st
            | Book.ReadCommit(r, commit, files) ->
                send (BookReadCommit(id, r, blobShasOf files)) (fun c t _ -> GitHub.listDirectory c t (oidText commit) (Target.investigationDir t id)) st
            | Book.ReadHead(r, since) -> send (BookReadHead(id, r, oidText since)) (fun c t nonce -> GitHub.getBranchHead c t nonce) st
            | Book.ReloadRequired _ -> startReload id st
            | Book.Released _
            | Book.OpenConnection _ -> st) st

    /// Step the document's book. A fault refuses the whole step (nothing changes). When the book reaches a clean
    /// Idle from Confirming or Reconciling, the publication is confirmed: the local copy is now the confirmed one.
    and private stepBook (env: Env) (id: string) (event: Book.Event) (m: Model, effects: Effect list) : Result<Model * Effect list, Refusal> =
        match Map.tryFind id m.Documents with
        | None -> Error { Reason = "There is no such investigation." }
        | Some d ->
            match Book.step d.Book event with
            | Error f -> Error { Reason = f.Reason }
            | Ok(book, fx) ->
                let confirmed =
                    match d.Book.Phase, book.Phase with
                    | (Book.Confirming _ | Book.Reconciling _), Book.Idle(commit, None) -> Some(oidText commit)
                    | _ -> None
                let d2 =
                    match confirmed with
                    | Some commit ->
                        { d with
                            Book = book
                            Confirmed = Some d.Local
                            ConfirmedHead = Some commit
                            LastSyncedAt = Some(env.Now()) }
                    | None -> { d with Book = book }
                let m = { m with Documents = m.Documents |> Map.add id d2 }
                let m = match confirmed with Some c -> { m with Head = Some c } | None -> m
                Ok(bookEffects id fx (m, effects))

    and private startReload (id: string) (st: Model * Effect list) =
        let m, _ = st
        match Map.tryFind id m.Documents with
        | Some d when d.Reload.IsNone && m.Credential.IsSome ->
            let (m2, e2), rid = sendWithId (ReloadHeadRead id) (fun c t nonce -> GitHub.getBranchHead c t nonce) st
            setDoc id (fun d -> { d with Reload = Some(ReloadHead rid) }) m2, e2
        | _ -> st

    // ---------- loading (connect, workspace, catalog) ----------

    /// Fold a remote file set read at `head` into the documents, preserving every local change. The book is
    /// re-observed at the new head only when nothing is in flight or unknown; then a pending change is re-offered to
    /// it as decided at that head (it does not touch the investigation's remote files: a rebase, CONTRACT §9.4.2 a).
    let private integrate (head: string) (id: string) (remote: Verified option) (readOnly: bool) (m: Model) : Model =
        let marker = sprintf "%s-%d" id m.NextId
        let m = { m with NextId = m.NextId + 1 }
        let fresh (v: Verified) =
            { InvestigationId = id
              ReadOnly = readOnly
              Confirmed = Some v
              ConfirmedHead = Some head
              Local = v
              Book = bookAt head
              RemoteConflict = None
              Durability = SavedLocally
              LastSyncedAt = None
              Reload = None }
        let sameAsConfirmed (d: Document) =
            match d.Confirmed, remote with
            | Some c, Some r -> Store.sameBytes c r
            | None, None -> true
            | _ -> false
        let rebased (d: Document) =
            let book = observe head d.Book
            let book =
                match m.Target with
                | Some t when pending d -> offer t marker head d.Local book
                | _ -> book
            { d with Book = book; ConfirmedHead = (if d.Confirmed.IsSome then Some head else d.ConfirmedHead); RemoteConflict = None; Reload = None }
        let doc =
            match Map.tryFind id m.Documents, remote with
            | None, Some r -> Some(fresh r)
            | None, None -> None
            | Some d, _ when d.ReadOnly || readOnly -> remote |> Option.map fresh
            | Some d, _ when bookInFlight d.Book || bookUnknown d.Book -> Some { d with Reload = None }
            | Some d, _ when sameAsConfirmed d -> Some(rebased d)
            | Some d, Some r when not (pending d) && (d.Confirmed.IsNone || Store.successor d.Local r |> Result.isOk) ->
                Some { fresh r with LastSyncedAt = d.LastSyncedAt; Book = observe head d.Book }
            | Some d, _ when d.Confirmed.IsNone && remote.IsNone -> Some(rebased d)
            | Some d, _ ->
                let detail =
                    match remote with
                    | Some _ -> "Another writer changed this investigation. Your local changes are kept."
                    | None -> "The remote copy is gone or unreadable; the copy on this device is kept."
                Some { d with RemoteConflict = Some { Head = head; Remote = remote; Detail = detail }; Reload = None }
        match doc with
        | Some d -> { m with Documents = m.Documents |> Map.add id d }
        | None -> m

    let private canWrite (m: Model) =
        match m.Connection with
        | Connected
        | AuthorizedEmpty -> m.Credential.IsSome && m.Target.IsSome
        | _ -> false

    let private finishLoad (env: Env) (m: Model, effects: Effect list) : Model * Effect list =
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
            // Writable documents this device knows but the workspace does not list.
            let m =
                m.Documents
                |> Map.fold (fun m id d -> if not d.ReadOnly && not (job.Done.ContainsKey id) then integrate job.Head id None false m else m) m
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
            let m = { m with Load = None; LoadErrors = errors; Catalog = catalog; Connection = connection }
            let writable = m.Documents |> Map.toList |> List.filter (fun (_, d) -> not d.ReadOnly) |> List.map fst
            let st = if writable.IsEmpty then m, effects else persistLocal writable (m, effects)
            // Restart recovery (PERSISTENCE §21): a send whose outcome is unknown is reconciled by the book now.
            if canWrite m then
                m.Documents
                |> Map.toList
                |> List.filter (fun (_, d) -> bookUnknown d.Book)
                |> List.fold (fun st (id, _) ->
                    match stepBook env id Book.Reconcile st with
                    | Ok st2 -> st2
                    | Error _ -> st) st
            else st

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
                let result = Store.read { DirectoryName = id; Entries = dir.Blobs |> Map.toList }
                if reload then
                    let m =
                        match result with
                        | Ok v -> integrate dir.Commit id (Some v) false m
                        | Error e ->
                            { m with LoadErrors = m.LoadErrors @ [ id, e ] }
                            |> setDoc id (fun d ->
                                { d with
                                    Reload = None
                                    RemoteConflict = Some { Head = dir.Commit; Remote = None; Detail = "The remote copy failed validation; the copy on this device is kept." } })
                    persistLocal [ id ] ({ m with Head = Some dir.Commit }, effects)
                else
                    let m =
                        { m with Load = m.Load |> Option.map (fun j -> { j with Dirs = j.Dirs |> Map.remove id; Done = j.Done |> Map.add id result }) }
                    finishLoad env (m, effects)
            else if reload then
                setDoc id (fun d -> { d with Reload = Some(ReloadDir dir) }) m, effects
            else
                { m with Load = m.Load |> Option.map (fun j -> { j with Dirs = j.Dirs |> Map.add id dir }) }, effects

    let private failDir (env: Env) (id: string) (reload: bool) (head: string) (e: ContractError) (m: Model, effects: Effect list) =
        if reload then
            let m =
                { m with LoadErrors = m.LoadErrors @ [ id, e ] }
                |> setDoc id (fun d ->
                    let gone = e.Code = Codes.pathInvalid && e.Detail.Contains("could not be listed", StringComparison.Ordinal)
                    { d with
                        Reload = None
                        RemoteConflict =
                            Some
                                { Head = head
                                  Remote = None
                                  Detail =
                                    if gone then "The remote copy is gone; the copy on this device is kept."
                                    else "The remote copy could not be read; the copy on this device is kept." } })
            persistLocal [ id ] (m, effects)
        else
            let m = { m with Load = m.Load |> Option.map (fun j -> { j with Dirs = j.Dirs |> Map.remove id; Done = j.Done |> Map.add id (Error e) }) }
            finishLoad env (m, effects)

    /// A credential GitHub rejected is dropped at once: nothing cached may mask it (PF-009-O2). The book's own
    /// requests stay matched so their answers still settle the book.
    let private credentialRejected (m: Model) =
        { m with
            Credential = None
            Generation = m.Generation + 1
            Connection = AccessDenied "GitHub rejected the credential. Reconnect with a valid token."
            Load = None
            InFlight = m.InFlight |> Map.filter (fun _ f -> match f.Purpose with BookPublish _ | BookReadCommit _ | BookReadHead _ | BookCompare _ -> true | _ -> false)
            Documents = m.Documents |> Map.map (fun _ d -> { d with Reload = None }) }

    let private failureConnection (f: ProviderFailure) =
        match f with
        | Unauthorized
        | Forbidden _
        | NotFound -> AccessDenied(ProviderFailure.describe f)
        | MissingRef _
        | Malformed _ -> Incompatible(ProviderFailure.describe f)
        | _ -> Offline(ProviderFailure.describe f)

    let private loadFailed (f: ProviderFailure) (m: Model, effects: Effect list) =
        let m = if f = Unauthorized then credentialRejected m else m
        { m with Load = None; Connection = failureConnection f }, effects

    // ---------- answers ----------

    let private bookOwned =
        function
        | BookPublish _
        | BookReadCommit _
        | BookReadHead _
        | BookCompare _ -> true
        | _ -> false

    let private answered (env: Env) (requestId: string) (outcome: Result<HttpResponse, TransportFailure>) (m: Model) : Step =
        match Map.tryFind requestId m.InFlight with
        | None -> refuse "The answer is not for a request in flight."
        | Some f when f.Generation <> m.Generation && not (bookOwned f.Purpose) -> refuse "The answer belongs to an earlier connection."
        | Some f ->
            let m = { m with InFlight = m.InFlight |> Map.remove requestId }
            let st = m, []
            let asFailure (t: TransportFailure) =
                match t with
                | NotSent d -> ProviderFailure.Offline d
                | TransportFailure.OutcomeUnknown d -> ProviderFailure.Unknown d
                | Cancelled -> ProviderFailure.Unknown "cancelled"
            let read (parse: JValue -> Result<'T, ProviderFailure>) =
                match outcome with
                | Ok r -> GitHub.classify r |> Result.bind parse
                | Error t -> Error(asFailure t)
            let head () = m.Load |> Option.map (fun j -> j.Head) |> Option.defaultValue ""
            match f.Purpose with
            | ProbeRepository ->
                match outcome with
                | Ok r when GitHub.broadClassicScopes r ->
                    Ok(
                        { m with
                            Credential = None
                            Generation = m.Generation + 1
                            InFlight = Map.empty
                            Connection = AccessDenied "Classic tokens with repo or admin scopes are refused. Use a fine-grained token for this repository only (Contents read/write, Metadata read)." },
                        []
                    )
                | _ ->
                    match read GitHub.parseRepository with
                    | Ok info -> Ok(send ProbeHead (fun c t id -> GitHub.getBranchHead c t id) ({ m with Repository = Some info }, []))
                    | Error e -> Ok(loadFailed e st)
            | ProbeHead ->
                match read GitHub.parseHead with
                | Ok h ->
                    let m = { m with Connection = Loading; Load = Some { Head = h; Pointer = None; Manifest = None; Listed = false; Dirs = Map.empty; Done = Map.empty } }
                    Ok(send ReadPointer (fun c t _ -> GitHub.getFile c t h Catalog.pointerPath) (m, []))
                | Error e -> Ok(loadFailed e st)
            | ReadPointer ->
                match read GitHub.parseFile with
                | Error NotFound -> Ok(startListing (head ()) st)
                | Error e -> Ok(loadFailed e st)
                | Ok bytes ->
                    match Catalog.parsePointer bytes, m.Target with
                    | Ok p, Some t when p.Workspace = Target.workspaceRoot t ->
                        let m = { m with Catalog = CatalogLoading p; Load = m.Load |> Option.map (fun j -> { j with Pointer = Some p }) }
                        Ok(send ReadCatalogManifest (fun c t _ -> GitHub.getFile c t (head ()) p.ManifestPath) (m, []))
                    | Ok _, _ -> Ok(startListing (head ()) st)
                    | Error e, _ -> Ok({ m with Load = None; Catalog = CatalogFailed(e.Code + ": " + e.Detail); Connection = Incompatible e.Detail }, [])
            | ReadCatalogManifest ->
                match read GitHub.parseFile, m.Load |> Option.bind (fun j -> j.Pointer) with
                | Ok bytes, Some p ->
                    match Catalog.parseManifest p bytes with
                    | Ok man -> Ok(startListing (head ()) ({ m with Load = m.Load |> Option.map (fun j -> { j with Manifest = Some man }) }, []))
                    | Error e -> Ok({ m with Load = None; Catalog = CatalogFailed(e.Code + ": " + e.Detail); Connection = Incompatible e.Detail }, [])
                | Error e, _ -> Ok(loadFailed e st)
                | Ok _, None -> refuse "No catalog pointer is being read."
            | ListInvestigations ->
                let entries =
                    match read GitHub.parseListing with
                    | Error NotFound -> Ok []
                    | other -> other
                match entries with
                | Error e -> Ok(loadFailed e st)
                | Ok entries ->
                    let h = head ()
                    let dirs = entries |> List.filter (fun e -> e.Kind = "dir" && Uuid.isValid e.Name)
                    let invalid = entries |> List.filter (fun e -> e.Kind <> "dir" || not (Uuid.isValid e.Name))
                    let m =
                        { m with
                            Load =
                                m.Load
                                |> Option.map (fun j ->
                                    { j with
                                        Listed = true
                                        Dirs = dirs |> List.map (fun e -> e.Name, { InvestigationId = e.Name; Commit = h; Entries = None; Blobs = Map.empty }) |> Map.ofList
                                        Done =
                                            invalid
                                            |> List.map (fun e -> e.Name, Error(ContractError.make Codes.pathInvalid "an entry in the investigations directory is not a canonical investigation directory"))
                                            |> Map.ofList }) }
                    let st = dirs |> List.fold (fun st e -> send (ListDir(e.Name, false)) (fun c t _ -> GitHub.listDirectory c t h (Target.investigationDir t e.Name)) st) (m, [])
                    Ok(finishLoad env st)
            | ListDir(id, reload) ->
                let commit =
                    if reload then
                        m.Documents |> Map.tryFind id |> Option.bind (fun d -> match d.Reload with Some(ReloadDir r) -> Some r.Commit | _ -> None) |> Option.defaultValue ""
                    else head ()
                match read GitHub.parseListing with
                | Error Unauthorized -> Ok(loadFailed Unauthorized st)
                | Error e -> Ok(failDir env id reload commit (ContractError.make Codes.pathInvalid ("the investigation directory could not be listed: " + ProviderFailure.describe e)) st)
                | Ok entries ->
                    let files = entries |> List.filter (fun e -> e.Kind = "file")
                    if files.Length <> entries.Length || (files |> List.map (fun e -> e.Name) |> List.sort) <> List.sort Store.names then
                        Ok(failDir env id reload commit (ContractError.make Codes.pathInvalid "the investigation directory must contain exactly events.ndjson, manifest.json and snapshot.json") st)
                    else
                        let st = dirProgress env id reload (fun d -> { d with Entries = Some files }) st
                        Ok(files |> List.fold (fun st e -> send (ReadBlob(id, e.Name, reload)) (fun c t _ -> GitHub.getBlob c t e.Sha) st) st)
            | ReadBlob(id, name, reload) ->
                match read GitHub.parseBlob with
                | Error Unauthorized -> Ok(loadFailed Unauthorized st)
                | Error e -> Ok(failDir env id reload "" (ContractError.make Codes.pathInvalid ("a file could not be read: " + ProviderFailure.describe e)) st)
                | Ok bytes -> Ok(dirProgress env id reload (fun d -> { d with Blobs = d.Blobs |> Map.add name bytes }) st)
            | ReloadHeadRead id ->
                match read GitHub.parseHead with
                | Ok h ->
                    let m = setDoc id (fun d -> { d with Reload = Some(ReloadDir { InvestigationId = id; Commit = h; Entries = None; Blobs = Map.empty }) }) m
                    Ok(send (ListDir(id, true)) (fun c t _ -> GitHub.listDirectory c t h (Target.investigationDir t id)) (m, []))
                | Error Unauthorized -> Ok(credentialRejected m, [])
                | Error e -> Ok({ (setDoc id (fun d -> { d with Reload = None }) m) with Connection = failureConnection e }, [])
            | BookPublish(id, r) ->
                // Whether the request left the device decides what a failure means (DC-003): NotSent is certainly
                // not applied; anything after sending may have been applied.
                let sent st = stepBook env id (Book.PublicationSent r) st
                match outcome with
                | Error(NotSent _) ->
                    stepBook env id (Book.PublicationFailed(r, Book.NetworkError)) ({ m with Connection = Offline "The network is unavailable." }, [])
                    |> Result.map (fun st -> persistLocal [ id ] st)
                | Error t ->
                    let transport = match t with Cancelled -> Book.Cancelled | _ -> Book.TimedOut
                    sent st |> Result.bind (stepBook env id (Book.PublicationFailed(r, transport))) |> Result.map (persistLocal [ id ])
                | Ok resp ->
                    let event, rejected =
                        match GitHub.parseCommit resp with
                        | Ok oid -> Book.PublicationAcknowledged(r, Book.Oid oid), false
                        | Error(HeadMoved _) -> Book.PublicationRefused(r, Book.StaleHead), false
                        | Error Unauthorized -> Book.PublicationRefused(r, Book.Refusal.Unauthorized), true
                        | Error(Forbidden _) -> Book.PublicationRefused(r, Book.Refusal.Forbidden), false
                        | Error NotFound
                        | Error(MissingRef _) -> Book.PublicationRefused(r, Book.Refusal.NotFound), false
                        | Error(RateLimited _) -> Book.PublicationRefused(r, Book.Refusal.RateLimited), false
                        | Error(Rejected(_, d)) -> Book.PublicationRefused(r, Book.Invalid d), false
                        | Error _ -> Book.PublicationFailed(r, Book.NetworkError), false
                    sent st
                    |> Result.bind (stepBook env id event)
                    |> Result.map (fun (m, fx) -> persistLocal [ id ] ((if rejected then credentialRejected m else m), fx))
            | BookReadCommit(id, r, expected) ->
                match read GitHub.parseListing with
                | Ok entries ->
                    let found = entries |> List.map (fun e -> e.Name, e.Sha) |> List.sort
                    stepBook env id (Book.ReadBack(r, (found = expected))) st |> Result.map (persistLocal [ id ])
                | Error _ -> stepBook env id (Book.ReadBackFailed r) st |> Result.map (persistLocal [ id ])
            | BookReadHead(id, r, since) ->
                match read GitHub.parseHead with
                | Ok h when h = since -> stepBook env id (Book.HeadObserved(r, Book.Oid h, Book.Descends [])) st |> Result.map (persistLocal [ id ])
                | Ok h -> Ok(send (BookCompare(id, r, since, h)) (fun c t _ -> GitHub.compare c t since h) st)
                | Error e ->
                    let m = if e = Unauthorized then credentialRejected m else m
                    stepBook env id (Book.HeadReadFailed r) (m, []) |> Result.map (persistLocal [ id ])
            | BookCompare(id, r, _, h) ->
                match read GitHub.parseCompare with
                | Ok(GitHub.Descends commits) ->
                    let ancestry =
                        Book.Descends(commits |> List.map (fun c -> { Book.CommitInfo.Oid = Book.Oid c.Sha; Parent = Book.Oid c.Parent; Marker = c.Marker }))
                    stepBook env id (Book.HeadObserved(r, Book.Oid h, ancestry)) st |> Result.map (persistLocal [ id ])
                | Ok GitHub.Unrelated -> stepBook env id (Book.HeadObserved(r, Book.Oid h, Book.Unrelated)) st |> Result.map (persistLocal [ id ])
                | Error _ -> stepBook env id (Book.HeadReadFailed r) st |> Result.map (persistLocal [ id ])

    /// A new local investigation (from Create or an accepted start command).
    let private createWith (m: Model) (id: string) (created: Result<Verified, Store.ChangeRefusal>) : Step =
        match m.Target with
        | None -> refuse "Select a data repository and workspace before creating an investigation."
        | Some _ when isCatalogWorkspace m -> refuse "The historical catalog workspace is read-only."
        | Some _ when m.Documents.ContainsKey id -> refuse "An investigation with this id already exists."
        | Some t ->
            match created with
            | Error r -> refuse (String.Join(" ", r.Reasons))
            | Ok local ->
                let marker = sprintf "%s-%d" id m.NextId
                let book =
                    match m.Head with
                    | Some h when canWrite m ->
                        match Book.step (bookAt h) (Book.Edit(changeOf t marker h local)) with
                        | Ok(b, _) -> b
                        | Error _ -> Book.initial
                    | _ -> Book.initial
                let d =
                    { InvestigationId = id
                      ReadOnly = false
                      Confirmed = None
                      ConfirmedHead = None
                      Local = local
                      Book = book
                      RemoteConflict = None
                      Durability = NotSaved
                      LastSyncedAt = None
                      Reload = None }
                Ok(persistLocal [ id ] ({ m with NextId = m.NextId + 1; Documents = m.Documents |> Map.add id d }, []))

    /// A change to an open investigation: the new local copy is offered to the book as decided at the commit the
    /// book observes. Refused while a save is in flight or its outcome is unknown.
    let private changeLocal (m: Model) (id: string) (change: Document -> Result<Verified, Store.ChangeRefusal>) : Step =
        match Map.tryFind id m.Documents, m.Target with
        | None, _
        | _, None -> refuse "There is no such investigation."
        | Some d, _ when d.ReadOnly -> refuse "This investigation is read-only."
        | Some d, _ when bookInFlight d.Book || d.Reload.IsSome -> refuse "A save or reload of this investigation is in flight; wait for it to settle."
        | Some d, _ when bookUnknown d.Book -> refuse "The last save may have been applied; check the repository (Reconcile) before changing it again."
        | Some d, Some t ->
            match change d with
            | Error r -> refuse (String.Join(" ", r.Reasons))
            | Ok local ->
                let marker = sprintf "%s-%d" id m.NextId
                let book =
                    match d.Book.Phase with
                    | Book.Idle(h, _) -> Book.step d.Book (Book.Edit(changeOf t marker (oidText h) local)) |> Result.map fst
                    | Book.Held(h, _, Book.NotWritten _) ->
                        Book.step d.Book Book.Discard
                        |> Result.bind (fun (b, _) -> Book.step b (Book.Edit(changeOf t marker (oidText h) local)))
                        |> Result.map fst
                    | _ -> Ok d.Book
                match book with
                | Error f -> refuse f.Reason
                | Ok book -> Ok(persistLocal [ id ] ({ m with NextId = m.NextId + 1 } |> setDoc id (fun d -> { d with Local = local; Book = book; Durability = NotSaved }), []))

    // ---------- the step function ----------

    /// The single entry point the shell drives: every load, edit, save, confirmation and reconciliation goes
    /// through here. A refused step returns Error and the caller keeps the previous model (nothing changes).
    let update (env: Env) (msg: Msg) (m: Model) : Step =
        match msg with
        | Restore stored ->
            if { m with Nonce = "" } <> initial then refuse "Restore is only possible at start-up."
            else
                match stored with
                | None -> Ok(m, [])
                | Some text ->
                    match parseStored text with
                    | Ok(target, docs) -> Ok({ m with Target = target; Documents = docs |> List.map (fun d -> d.InvestigationId, d) |> Map.ofList }, [])
                    | Error e -> refuse ("The local copy could not be restored and was left untouched: " + e)
        | Connect(target, credential) ->
            if anyUnsettled m then refuse "A save or reload is still settling; wait for it before connecting again."
            elif m.Target.IsSome && m.Target <> Some target && m.Documents |> Map.exists (fun _ d -> not d.ReadOnly && (pending d || bookUnknown d.Book)) then
                refuse "Unsaved changes belong to the currently selected workspace; save or discard them before selecting another."
            else
                let documents = if m.Target = Some target then m.Documents |> Map.filter (fun _ d -> not d.ReadOnly) else Map.empty
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
                        Documents = documents }
                Ok(send ProbeRepository (fun c t nonce -> GitHub.getRepository c t nonce) ({ m with Nonce = env.Nonce }, []))
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
        | Create env' -> createWith m env'.Investigation.Text (Store.create env')
        | Submit env' -> changeLocal m env'.Investigation.Text (fun d -> Store.submit d.Local env')
        | Append(id, expected, events) ->
            match Map.tryFind id m.Documents with
            | None -> createWith m id (Store.appendAccepted None id expected events)
            | Some _ -> changeLocal m id (fun d -> Store.appendAccepted (Some d.Local) id expected events)
        | Save id ->
            match Map.tryFind id m.Documents with
            | None -> refuse "There is no such investigation."
            | Some d when d.ReadOnly -> refuse "This investigation is read-only."
            | Some d when d.RemoteConflict.IsSome -> refuse "The remote copy differs from the one this change was decided on; discard or decide again on the remote copy."
            | Some d when d.Reload.IsSome -> refuse "A reload of this investigation is in flight."
            | Some _ when not (canWrite m) -> refuse "Connect to the data repository before saving; the change stays on this device."
            | Some _ -> stepBook env id Book.Write (m, []) |> Result.map (persistLocal [ id ])
        | Reconcile id ->
            match Map.tryFind id m.Documents with
            | None -> refuse "There is no such investigation."
            | Some _ when not (canWrite m) -> refuse "Connect to the data repository to check what happened to the last save."
            | Some _ -> stepBook env id Book.Reconcile (m, [])
        | ReloadRemote id ->
            match Map.tryFind id m.Documents with
            | None -> refuse "There is no such investigation."
            | Some d when unsettled d -> refuse "A save or reload of this investigation is in flight."
            | Some d when bookUnknown d.Book -> refuse "Reconcile the unknown save first."
            | Some _ when m.Credential.IsNone || m.Target.IsNone || (match m.Connection with Connecting | Loading | Disconnected -> true | _ -> false) ->
                refuse "Connect to the data repository first."
            | Some _ -> Ok(startReload id (m, []))
        | DiscardLocal id ->
            match Map.tryFind id m.Documents with
            | None -> refuse "There is no such investigation."
            | Some d when d.ReadOnly -> refuse "This investigation is read-only."
            | Some d when bookInFlight d.Book -> refuse "A save is in flight."
            | Some d when bookUnknown d.Book -> refuse "The last save may have been applied; reconcile before discarding."
            | Some d when not (pending d) && d.RemoteConflict.IsNone && not (bookHeld d.Book) -> refuse "There are no local changes to discard."
            | Some d ->
                // Core's Discard leaves a held change; the book is then re-observed at the copy being adopted.
                let book =
                    match d.Book.Phase with
                    | Book.Held _
                    | Book.Idle(_, Some _) -> Book.step d.Book Book.Discard |> Result.map fst
                    | _ -> Ok d.Book
                match book with
                | Error f -> refuse f.Reason
                | Ok book ->
                    let adopt, head =
                        match d.RemoteConflict with
                        | Some c -> c.Remote, Some c.Head
                        | None -> d.Confirmed, d.ConfirmedHead
                    match adopt with
                    | Some v ->
                        let book = match head with Some h -> observe h book | None -> book
                        Ok(persistLocal [ id ] (m |> setDoc id (fun d -> { d with Confirmed = Some v; ConfirmedHead = head; Local = v; Book = book; RemoteConflict = None }), []))
                    | None -> Ok(persistLocal [] ({ m with Documents = m.Documents |> Map.remove id }, []))
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

    /// UI-006-O1 distinct states, plus `unknown` (CONTRACT §9.4.3), from core's book status and local durability.
    let documentStatus (m: Model) (d: Document) =
        if d.RemoteConflict.IsSome then "conflict"
        else
            match Book.status d.Book.Phase, d.Book.Phase with
            | Book.Conflicted, _ -> "conflict"
            | Book.Failed, Book.Held(_, _, Book.NotWritten(Book.NeverSent _)) -> "queuedOffline"
            | Book.Failed, _ -> "failed"
            | (Book.MayHaveSaved | Book.Checking), _ -> "unknown"
            | (Book.Saving | Book.Verifying), _ -> "syncingRemotely"
            | _ ->
                match d.Durability with
                | LocalSaveFailed _ -> "failed"
                | SavingLocally _ -> "savingLocally"
                | NotSaved -> "unsaved"
                | SavedLocally when pending d ->
                    match m.Connection with
                    | Offline _
                    | Disconnected -> "queuedOffline"
                    | _ -> "savedLocally"
                | SavedLocally -> if d.LastSyncedAt.IsSome then "remotelySynced" else "clean"

    /// EXT-DATA-007-O1: the eight connection states. A document conflict is shown as the connection's conflict.
    let connectionLabel (m: Model) =
        let conflict = m.Documents |> Map.exists (fun _ d -> documentStatus m d = "conflict")
        match m.Connection with
        | Connected
        | AuthorizedEmpty when conflict -> "conflict"
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

    let canSave (m: Model) (d: Document) =
        not d.ReadOnly && canWrite m && d.Reload.IsNone && d.RemoteConflict.IsNone
        && (match d.Book.Phase with
            | Book.Idle(_, Some _) -> true
            | Book.Held(_, _, Book.NotWritten _) -> true
            | _ -> false)

    let canReconcile (m: Model) (d: Document) = canWrite m && bookUnknown d.Book
