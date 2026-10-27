namespace IndyInitRootCauseInvestigator.Engine

open System
open System.Text

/// A refusal by the storage contract reader/writer (contract §11). `DomainCode` is set for stage 15.
type StorageError =
    { Code: string
      DomainCode: string option
      Detail: string }

/// A verified investigation: the history, its replayed state, and the exact bytes it was read from.
type LoadedInvestigation =
    { Events: DomainEvent list
      State: Investigation
      EventsBytes: byte[]
      ManifestBytes: byte[]
      SnapshotBytes: byte[]
      EventsSha256: string
      SnapshotSha256: string
      StateSha256: string }

/// One investigation's three files as written (contract §3, §9).
type FileSet =
    { Events: byte[]
      Manifest: byte[]
      Snapshot: byte[] }

/// Boundary C: the Causa storage contract v1.0.0 reader and writer. Every input is untrusted; the reader fails
/// closed at the first failing stage, in the normative order of contract §11.1, and never repairs anything.
/// Admissibility (stage 15) is decided by this engine's own History.validate: no second validator exists.
[<RequireQualifiedAccess>]
module Storage =
    let manifestFile = "manifest.json"
    let eventsFile = "events.ndjson"
    let snapshotFile = "snapshot.json"

    /// The three identities of the release this reader implements (CAUSA-STORAGE-RELEASE-v1.0.0, STORAGE-007).
    let schemaIdentity = "0a68af8c93190d2e4d6c27beb2f8cb67c3aebdda586d01beb7122b71af179152"
    let datasetIdentity = "f650a2e509a420872b46177b21f57ef6ebd7625ae4c69dd92198d3ca472c2b86"
    let releaseIdentity = "6a7e7802ff2a3ff500ab6065662514a96e09e6c15eb30212ba1377fcc5bf8fc5"

    /// The SHA-256 of the embedded schemas/bundle.json: must equal `schemaIdentity`.
    let embeddedSchemaIdentity () =
        Schema.files.Value |> Map.tryFind "bundle.json" |> Option.map Hashing.sha256Hex

    let private fail code detail = Error { Code = code; DomainCode = None; Detail = detail }

    let private strictUtf8 = UTF8Encoding(false, true)

    let private uuidPattern = Text.RegularExpressions.Regex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\\z")

    let isCanonicalUuid (s: string) =
        uuidPattern.IsMatch s && s <> "00000000-0000-0000-0000-000000000000"

    /// Stage 2 for one file: UTF-8 without BOM, LF only, non-empty, final LF; no blank line in NDJSON.
    let private framing (name: string) (bytes: byte[]) (ndjson: bool) : Result<string, StorageError> =
        if bytes.Length = 0 then fail "CAUSA.ENCODING" $"{name} is empty"
        elif bytes.Length >= 3 && bytes[0] = 0xEFuy && bytes[1] = 0xBBuy && bytes[2] = 0xBFuy then fail "CAUSA.ENCODING" $"{name} starts with a byte order mark"
        else
            match (try Ok(strictUtf8.GetString bytes) with :? ArgumentException -> Error()) with
            | Error() -> fail "CAUSA.ENCODING" $"{name} is not valid UTF-8"
            | Ok text ->
                if text.IndexOf('\r') >= 0 then fail "CAUSA.ENCODING" $"{name} contains a carriage return"
                elif not (text.EndsWith("\n", StringComparison.Ordinal)) then fail "CAUSA.ENCODING" $"{name} has no final line feed"
                elif ndjson && (text.StartsWith("\n", StringComparison.Ordinal) || text.Contains("\n\n")) then fail "CAUSA.ENCODING" $"{name} contains a blank line"
                else Ok text

    let private lines (text: string) =
        text.Substring(0, text.Length - 1).Split('\n') |> List.ofArray

    let private parse (name: string) (text: string) =
        match Json.parse text with
        | Ok j -> Ok j
        | Error e -> fail "CAUSA.JSON_MALFORMED" $"{name}: {e}"

    let private canonicalCheck (name: string) (text: string) (j: Json) =
        if Json.canonical j = text then Ok() else fail "CAUSA.NON_CANONICAL" $"{name} is not in RFC 8785 canonical form"

    let private versionCheck (name: string) (j: Json) (fields: string list) =
        fields
        |> List.tryPick (fun f ->
            match Json.field f j with
            | Some(JString v) when v <> "1.0.0" -> Some $"{name}: {f} is {v}"
            | _ -> None)
        |> function
            | Some detail -> fail "CAUSA.UNSUPPORTED_VERSION" detail
            | None -> Ok()

    let private sequenceAll (results: Result<unit, StorageError> list) =
        results |> List.tryPick (function Error e -> Some e | Ok() -> None) |> function
            | Some e -> Error e
            | None -> Ok()

    /// Stages 2–7 over the event lines only (append step 2 numbers them from `firstVersion`).
    let private eventLines (firstVersion: int64) (text: string) : Result<(Json * DomainEvent) list, StorageError> =
        let ls = lines text
        let numbered = ls |> List.mapi (fun i l -> firstVersion + int64 i, l)
        result {
            let! parsed =
                numbered
                |> List.fold
                    (fun acc (n, l) ->
                        acc
                        |> Result.bind (fun xs ->
                            match Json.parse l with
                            | Ok j -> Ok((n, l, j) :: xs)
                            | Error e -> fail "CAUSA.JSON_MALFORMED" $"event line {n}: {e}"))
                    (Ok [])
                |> Result.map List.rev
            do! parsed |> List.map (fun (n, l, j) -> canonicalCheck $"event line {n}" l j) |> sequenceAll
            do! parsed |> List.map (fun (n, _, j) -> versionCheck $"event line {n}" j [ "formatVersion"; "semanticVersion" ]) |> sequenceAll
            do!
                parsed
                |> List.map (fun (n, _, j) ->
                    match Json.field "eventType" j with
                    | Some(JString t) when not (List.contains t Wire.eventTypes) -> fail "CAUSA.UNKNOWN_EVENT_TYPE" $"event line {n}: {t}"
                    | _ -> Ok())
                |> sequenceAll
            do!
                parsed
                |> List.map (fun (n, _, j) ->
                    if Schema.validate "event-envelope.schema.json" j then Ok()
                    else fail "CAUSA.SCHEMA_VIOLATION" $"event line {n} violates the event schema")
                |> sequenceAll
            return!
                parsed
                |> List.fold
                    (fun acc (n, _, j) ->
                        acc
                        |> Result.bind (fun xs ->
                            match Wire.parseEvent j with
                            | Ok e -> Ok((j, e) :: xs)
                            | Error d -> fail "CAUSA.SCHEMA_VIOLATION" $"event line {n}: {d}"))
                    (Ok [])
                |> Result.map List.rev
        }

    let private groupsOf (events: DomainEvent list) = History.groups events

    /// Stages 10–15 over a complete history.
    let private historyStages (investigationId: string) (events: (Json * DomainEvent) list) : Result<unit, StorageError> =
        result {
            match events |> List.tryFind (fun (_, e) -> e.Investigation.Text <> investigationId) with
            | Some(_, e) -> return! fail "CAUSA.INVESTIGATION_MISMATCH" $"event {e.Version} names investigation {e.Investigation.Text}"
            | None -> ()
            let eventIds = events |> List.map (fun (j, _) -> Json.field "eventId" j)
            if (List.distinct eventIds).Length <> eventIds.Length then return! fail "CAUSA.EVENT_ID_DUPLICATE" "an eventId appears twice"
            match events |> List.mapi (fun i (_, e) -> int64 i + 1L, e) |> List.tryFind (fun (want, e) -> e.Version <> want) with
            | Some(want, e) -> return! fail "CAUSA.SEQUENCE_INVALID" $"aggregateVersion {e.Version} found where {want} was expected"
            | None -> ()
            match
                events
                |> List.tryFind (fun (j, e) ->
                    Json.field "eventId" j <> Some(JString(Wire.uuid (Hashing.eventId e.Investigation.Value e.Version e.CommandId))))
            with
            | Some(_, e) -> return! fail "CAUSA.EVENT_ID_MISMATCH" $"event {e.Version} has an eventId that is not its derivation"
            | None -> ()
            let correlations = groupsOf (events |> List.map snd) |> List.map (fun g -> (List.head g).CommandId)
            if (List.distinct correlations).Length <> correlations.Length then
                return! fail "CAUSA.CORRELATION_REUSED" "a correlationId names two non-adjacent command groups"
            match History.validate (events |> List.map snd) with
            | [] -> ()
            | first :: _ -> return! Error { Code = "CAUSA.HISTORY_INADMISSIBLE"; DomainCode = Some first.Code; Detail = first.Message }
        }

    let private snapshotText (state: Investigation) (lastAt: DateTimeOffset) = Json.canonical (Wire.snapshot state lastAt)

    /// Read and fully verify one investigation directory (contract §11.1, stages 1–17).
    let read (directoryName: string) (files: Map<string, byte[]>) : Result<LoadedInvestigation, StorageError> =
        result {
            // 1. path
            if not (isCanonicalUuid directoryName) then return! fail "CAUSA.PATH_INVALID" "the directory name is not a canonical UUID"
            let names = files |> Map.toList |> List.map fst |> Set.ofList
            if names <> Set.ofList [ manifestFile; eventsFile; snapshotFile ] then
                return! fail "CAUSA.PATH_INVALID" "the directory must contain exactly manifest.json, events.ndjson and snapshot.json"
            let manifestBytes = files[manifestFile]
            let eventsBytes = files[eventsFile]
            let snapshotBytes = files[snapshotFile]
            // 2. encoding
            let! manifestText = framing manifestFile manifestBytes false
            let! eventsText = framing eventsFile eventsBytes true
            let! snapshotText' = framing snapshotFile snapshotBytes false
            let manifestBody = manifestText.Substring(0, manifestText.Length - 1)
            let snapshotBody = snapshotText'.Substring(0, snapshotText'.Length - 1)
            // 3. JSON
            let! manifest = parse manifestFile manifestBody
            let eventLinesText = lines eventsText
            let! eventJsons =
                eventLinesText
                |> List.mapi (fun i l -> i, l)
                |> List.fold
                    (fun acc (i, l) ->
                        acc
                        |> Result.bind (fun xs ->
                            match Json.parse l with
                            | Ok j -> Ok(j :: xs)
                            | Error e -> fail "CAUSA.JSON_MALFORMED" $"event line {i + 1}: {e}"))
                    (Ok [])
                |> Result.map List.rev
            let! snapshot = parse snapshotFile snapshotBody
            // 4. canonical form
            do! canonicalCheck manifestFile manifestBody manifest
            do! List.zip eventLinesText eventJsons |> List.mapi (fun i (l, j) -> canonicalCheck $"event line {i + 1}" l j) |> sequenceAll
            do! canonicalCheck snapshotFile snapshotBody snapshot
            // 5. versions
            do! versionCheck manifestFile manifest [ "formatVersion"; "schemaVersion"; "semanticVersion" ]
            do! eventJsons |> List.mapi (fun i j -> versionCheck $"event line {i + 1}" j [ "formatVersion"; "semanticVersion" ]) |> sequenceAll
            do! versionCheck snapshotFile snapshot [ "formatVersion"; "semanticVersion" ]
            // 6. event types
            do!
                eventJsons
                |> List.mapi (fun i j ->
                    match Json.field "eventType" j with
                    | Some(JString t) when not (List.contains t Wire.eventTypes) -> fail "CAUSA.UNKNOWN_EVENT_TYPE" $"event line {i + 1}: {t}"
                    | _ -> Ok())
                |> sequenceAll
            // 7. schemas (calendar validity included)
            if not (Schema.validate "investigation-manifest.schema.json" manifest) then
                return! fail "CAUSA.SCHEMA_VIOLATION" "manifest.json violates its schema"
            do!
                eventJsons
                |> List.mapi (fun i j ->
                    if Schema.validate "event-envelope.schema.json" j then Ok()
                    else fail "CAUSA.SCHEMA_VIOLATION" $"event line {i + 1} violates the event schema")
                |> sequenceAll
            if not (Schema.validate "snapshot.schema.json" snapshot) then
                return! fail "CAUSA.SCHEMA_VIOLATION" "snapshot.json violates its schema"
            let! decoded =
                eventJsons
                |> List.mapi (fun i j -> i, j)
                |> List.fold
                    (fun acc (i, j) ->
                        acc
                        |> Result.bind (fun xs ->
                            match Wire.parseEvent j with
                            | Ok e -> Ok((j, e) :: xs)
                            | Error d -> fail "CAUSA.SCHEMA_VIOLATION" $"event line {i + 1}: {d}"))
                    (Ok [])
                |> Result.map List.rev
            let events = decoded |> List.map snd
            // 8. manifest
            let count = int64 events.Length
            let firstAt = (List.head events).At
            let lastAt = (List.last events).At
            let manifestMatches =
                Json.field "investigationId" manifest = Some(JString directoryName)
                && Json.field "aggregateVersion" manifest = Some(JInt count)
                && Json.field "eventCount" manifest = Some(JInt count)
                && Json.field "createdAt" manifest = Some(JString(Wire.timestamp firstAt))
                && Json.field "updatedAt" manifest = Some(JString(Wire.timestamp lastAt))
            if not manifestMatches then
                return! fail "CAUSA.MANIFEST_MISMATCH" "the manifest's id, counts or times do not match the directory and history"
            // 9. events digest
            let eventsSha = Hashing.sha256Hex eventsBytes
            if Json.field "eventsSha256" manifest <> Some(JString eventsSha) then
                return! fail "CAUSA.EVENTS_HASH_MISMATCH" "eventsSha256 does not match events.ndjson"
            // 10-15. history
            do! historyStages directoryName decoded
            // 16. snapshot digest
            let snapshotSha = Hashing.sha256OfText snapshotBody
            if Json.field "snapshotSha256" manifest <> Some(JString snapshotSha) then
                return! fail "CAUSA.SNAPSHOT_HASH_MISMATCH" "snapshotSha256 does not match snapshot.json"
            // 17. snapshot = projection of the replayed history
            let state = Evolve.replay events |> Option.get
            if snapshotText state lastAt <> snapshotBody then
                return! fail "CAUSA.SNAPSHOT_MISMATCH" "snapshot.json is not the projection of the replayed history"
            return
                { Events = events
                  State = state
                  EventsBytes = eventsBytes
                  ManifestBytes = manifestBytes
                  SnapshotBytes = snapshotBytes
                  EventsSha256 = eventsSha
                  SnapshotSha256 = snapshotSha
                  StateSha256 = Hashing.sha256OfText (Json.canonical (Wire.state state)) }
        }

    /// Write the three files for a complete, admissible history (the writer refuses what it cannot represent).
    let write (events: DomainEvent list) : Result<FileSet, StorageError> =
        result {
            if events.IsEmpty then return! fail "CAUSA.UNREPRESENTABLE" "an investigation has at least one event"
            match events |> List.tryPick (fun e -> Wire.unrepresentable e |> Option.map (fun why -> e.Version, why)) with
            | Some(v, why) -> return! fail "CAUSA.UNREPRESENTABLE" $"event {v}: {why}"
            | None -> ()
            match History.validate events with
            | [] -> ()
            | first :: _ -> return! Error { Code = "CAUSA.HISTORY_INADMISSIBLE"; DomainCode = Some first.Code; Detail = first.Message }
            let state = Evolve.replay events |> Option.get
            let eventsText = events |> List.map (fun e -> Json.canonical (Wire.envelope e) + "\n") |> String.concat ""
            let eventsBytes = Encoding.UTF8.GetBytes eventsText
            let lastAt = (List.last events).At
            let snapshotBody = snapshotText state lastAt
            let manifestBody =
                Json.canonical (
                    Wire.manifest
                        state
                        (int64 events.Length)
                        (Hashing.sha256Hex eventsBytes)
                        (Hashing.sha256OfText snapshotBody)
                        (List.head events).At
                        lastAt
                )
            return
                { Events = eventsBytes
                  Manifest = Encoding.UTF8.GetBytes(manifestBody + "\n")
                  Snapshot = Encoding.UTF8.GetBytes(snapshotBody + "\n") }
        }

    let filesOf (set: FileSet) =
        Map.ofList [ manifestFile, set.Manifest; eventsFile, set.Events; snapshotFile, set.Snapshot ]

    /// Optimistic append (contract §9.2): (1) version check; (2) the appended lines through stages 2–7, numbered from
    /// current + 1; (3) the combined history through every reader stage, manifest and snapshot rebuilt here.
    let append (current: LoadedInvestigation) (expectedVersion: int64) (appended: byte[]) : Result<LoadedInvestigation * FileSet, StorageError> =
        result {
            if expectedVersion <> current.State.Version then
                return! fail "CAUSA.VERSION_STALE" $"expected version {expectedVersion}, current {current.State.Version}; reload and re-decide"
            let! text = framing "appended events" appended true
            let! _ = eventLines (current.State.Version + 1L) text
            let combined = Array.append current.EventsBytes appended
            let! combinedText = framing eventsFile combined true
            let! decoded = eventLines 1L combinedText
            let events = decoded |> List.map snd
            let lastAt = (List.last events).At
            let state = Evolve.replay events |> Option.get
            let snapshotBody = snapshotText state lastAt
            let manifestBody =
                Json.canonical (
                    Wire.manifest state (int64 events.Length) (Hashing.sha256Hex combined) (Hashing.sha256OfText snapshotBody) (List.head events).At lastAt
                )
            let set =
                { Events = combined
                  Manifest = Encoding.UTF8.GetBytes(manifestBody + "\n")
                  Snapshot = Encoding.UTF8.GetBytes(snapshotBody + "\n") }
            let! loaded = read current.State.Id.Text (filesOf set)
            return loaded, set
        }

    let private startsWith (prefix: byte[]) (bytes: byte[]) =
        bytes.Length >= prefix.Length && Seq.forall2 (=) prefix (Array.sub bytes 0 prefix.Length)

    /// A successor file set must verify on its own and extend the accepted history byte-for-byte (contract §9.3).
    let successor (accepted: LoadedInvestigation) (directoryName: string) (candidate: Map<string, byte[]>) : Result<LoadedInvestigation, StorageError> =
        result {
            let! loaded = read directoryName candidate
            let prefix = accepted.EventsBytes
            let bytes = loaded.EventsBytes
            if not (startsWith prefix bytes) then
                return! fail "CAUSA.HISTORY_REWRITTEN" "the successor does not start with the accepted events.ndjson bytes"
            return loaded
        }

    /// The domain events a command would add, as the exact NDJSON bytes to append (Boundary C write path).
    let appendBytes (events: DomainEvent list) : Result<byte[], StorageError> =
        match events |> List.tryPick (fun e -> Wire.unrepresentable e |> Option.map (fun why -> e.Version, why)) with
        | Some(v, why) -> fail "CAUSA.UNREPRESENTABLE" $"event {v}: {why}"
        | None -> Ok(Encoding.UTF8.GetBytes(events |> List.map (fun e -> Json.canonical (Wire.envelope e) + "\n") |> String.concat ""))
