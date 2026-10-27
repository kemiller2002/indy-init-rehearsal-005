namespace Causa.Persistence

open System
open IndyInitRootCauseInvestigator.Engine

/// One investigation directory as read from a provider: directory name and every entry in it.
type RawFileSet =
    { DirectoryName: string
      Entries: (string * byte[]) list }

/// An investigation verified by the engine's storage reader (Boundary C), with the identities persistence needs.
type Verified =
    { InvestigationId: string
      Version: int64
      Title: string
      Lifecycle: string
      /// The engine's own load: history, replayed state and exact bytes.
      Loaded: LoadedInvestigation
      StateSha256: string
      EventsSha256: string
      ManifestSha256: string
      SnapshotSha256: string
      Manifest: byte[]
      EventsFile: byte[]
      Snapshot: byte[] }

/// The adapter from persistence to the engine's storage contract (Boundary C, T14 freeze). Persistence has no
/// validator of its own: every load goes through Storage.read (17 stages, History.validate at stage 15), every
/// change through Kernel.submit -> Storage.appendBytes -> Storage.append (or Storage.write for a new history).
[<RequireQualifiedAccess>]
module Store =
    let manifestName = Storage.manifestFile
    let eventsName = Storage.eventsFile
    let snapshotName = Storage.snapshotFile
    let names = [ eventsName; manifestName; snapshotName ]

    let private ofStorage (e: StorageError) : ContractError =
        { Code = e.Code; Detail = e.Detail; DomainCode = e.DomainCode }

    let private header (snapshot: byte[]) (key: string) =
        match Causa.Persistence.Json.decodeUtf8 snapshot |> Result.bind (fun t -> Causa.Persistence.Json.parse (t.TrimEnd('\n'))) with
        | Ok(JObj o) ->
            match Causa.Persistence.Json.field "state" o with
            | Some(JObj st) -> Causa.Persistence.Json.str key st |> Option.defaultValue ""
            | _ -> ""
        | _ -> ""

    let private verified (id: string) (loaded: LoadedInvestigation) : Verified =
        let manifestText = Causa.Persistence.Json.strictUtf8.GetString loaded.ManifestBytes
        { InvestigationId = id
          Version = loaded.State.Version
          Title = loaded.State.Title.Value
          Lifecycle = header loaded.SnapshotBytes "lifecycle"
          Loaded = loaded
          StateSha256 = loaded.StateSha256
          EventsSha256 = loaded.EventsSha256
          ManifestSha256 = Causa.Persistence.Json.sha256OfText (manifestText.TrimEnd('\n'))
          SnapshotSha256 = loaded.SnapshotSha256
          Manifest = loaded.ManifestBytes
          EventsFile = loaded.EventsBytes
          Snapshot = loaded.SnapshotBytes }

    /// Read and fully verify one directory through the engine (fails closed at the first failing stage).
    let read (raw: RawFileSet) : Result<Verified, ContractError> =
        Storage.read raw.DirectoryName (Map.ofList raw.Entries)
        |> Result.map (verified raw.DirectoryName)
        |> Result.mapError ofStorage

    let files (v: Verified) = [ manifestName, v.Manifest; eventsName, v.EventsFile; snapshotName, v.Snapshot ]

    /// Why a change was not made: the engine's typed rejection or a storage refusal, as safe text.
    type ChangeRefusal = { Code: string; Reasons: string list }

    let private rejected (r: Rejection) = { Code = Kernel.code r; Reasons = Kernel.reasons r }
    let private storageRefusal (e: StorageError) = { Code = e.Code; Reasons = [ e.Detail ] }

    /// Create a new investigation from its start command (Boundary A then C).
    let create (env: CommandEnvelope) : Result<Verified, ChangeRefusal> =
        match Kernel.submit Kernel.empty env with
        | Error r -> Error(rejected r)
        | Ok accepted ->
            Storage.write accepted.Events
            |> Result.bind (fun set -> Storage.read env.Investigation.Text (Storage.filesOf set))
            |> Result.map (verified env.Investigation.Text)
            |> Result.mapError storageRefusal

    /// Decide one command against the local copy and append its events, verified exactly as a reader would.
    let submit (local: Verified) (env: CommandEnvelope) : Result<Verified, ChangeRefusal> =
        if env.Investigation.Text <> local.InvestigationId then Error { Code = "COMMAND.WRONG_INVESTIGATION"; Reasons = [ "The command names another investigation." ] }
        else
            match Kernel.submit (Kernel.ofLoaded local.Loaded) env with
            | Error r -> Error(rejected r)
            | Ok accepted ->
                Storage.appendBytes accepted.Events
                |> Result.bind (fun bytes -> Storage.append local.Loaded local.Version bytes)
                |> Result.map (fst >> verified local.InvestigationId)
                |> Result.mapError storageRefusal

    /// A newer file set is acceptable only if it extends the accepted history byte-for-byte (contract §9.3).
    let successor (accepted: Verified) (candidate: Verified) : Result<Verified, ContractError> =
        Storage.successor accepted.Loaded candidate.InvestigationId (Map.ofList (files candidate))
        |> Result.map (verified candidate.InvestigationId)
        |> Result.mapError ofStorage

    let sameBytes (a: Verified) (b: Verified) = a.EventsFile = b.EventsFile && a.Manifest = b.Manifest && a.Snapshot = b.Snapshot
