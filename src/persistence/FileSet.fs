namespace Causa.Persistence

open System
open System.Text

/// The verdict of the domain engine on a history (causa-data CONTRACT §11.3).
type Admissibility =
    | Admissible
    | Inadmissible of domainCode: string * detail: string
    /// The engine was not consulted. A reader that cannot evaluate admissibility MUST NOT report the history as
    /// valid (§11.3): such a load is "structurally valid, admissibility not evaluated" and is labelled so.
    | NotEvaluated of reason: string

/// The port to the core F# domain engine (History.validate over a decoded history). Persistence owns no domain
/// policy (PF-002): it hands the wire events to whichever engine the application wires in.
type HistoryEngine =
    { Name: string
      Evaluate: JValue list -> Admissibility }

[<RequireQualifiedAccess>]
module HistoryEngine =
    /// The default until the application wires the core engine in: never claims admissibility.
    let notWired =
        { Name = "not-wired"
          Evaluate = fun _ -> NotEvaluated "The domain engine is not connected to the persistence reader." }

/// One investigation directory as read from a provider: directory name and every entry in it.
type RawFileSet =
    { DirectoryName: string
      Entries: (string * byte[]) list }

/// A file set that passed every reader stage it could evaluate (§11.1).
type Verified =
    { InvestigationId: string
      Version: int64
      Title: string
      Lifecycle: string
      Events: JValue list
      /// Canonical text of each event line, without the LF.
      EventLines: string list
      State: JValue
      StateSha256: string
      EventsSha256: string
      ManifestSha256: string
      SnapshotSha256: string
      Manifest: byte[]
      EventsFile: byte[]
      Snapshot: byte[]
      Admissibility: Admissibility }

[<RequireQualifiedAccess>]
module FileSet =
    let manifestName = "manifest.json"
    let eventsName = "events.ndjson"
    let snapshotName = "snapshot.json"
    let names = [ eventsName; manifestName; snapshotName ]

    let private fail code detail : Result<'T, ContractError> = ContractError.fail code detail

    let private firstError (checks: (unit -> ContractError option) list) =
        checks |> List.tryPick (fun c -> c ())

    /// Stage 2 framing for a JSON file: non-empty, no CR, exactly one final LF, valid UTF-8 without BOM.
    let private frameJson (name: string) (bytes: byte[]) : Result<string, ContractError> =
        match Json.decodeUtf8 bytes with
        | Error e -> fail Codes.encoding (name + ": " + e)
        | Ok text ->
            if text.Length = 0 then fail Codes.encoding (name + ": file is empty")
            elif text.Contains '\r' then fail Codes.encoding (name + ": CR is not allowed")
            elif not (text.EndsWith '\n') then fail Codes.encoding (name + ": missing final LF")
            else Ok(text.Substring(0, text.Length - 1))

    /// Stage 2 framing for events.ndjson: also no blank line.
    let private frameEvents (bytes: byte[]) : Result<string list, ContractError> =
        match Json.decodeUtf8 bytes with
        | Error e -> fail Codes.encoding (eventsName + ": " + e)
        | Ok text ->
            if text.Length = 0 then fail Codes.encoding (eventsName + ": file is empty")
            elif text.Contains '\r' then fail Codes.encoding (eventsName + ": CR is not allowed")
            elif not (text.EndsWith '\n') then fail Codes.encoding (eventsName + ": missing final LF")
            else
                let lines = text.Substring(0, text.Length - 1).Split('\n') |> List.ofArray
                match lines |> List.tryFindIndex String.IsNullOrEmpty with
                | Some i -> fail Codes.encoding (sprintf "%s line %d: blank line" eventsName (i + 1))
                | None -> Ok lines

    let private parse (label: string) (text: string) =
        Json.parse text |> Result.mapError (fun e -> ContractError.make Codes.jsonMalformed (label + ": " + e))

    let private canonicalCheck (label: string) (text: string) (v: JValue) =
        match Json.canonical v with
        | Ok c when c = text -> Ok()
        | Ok _ -> fail Codes.nonCanonical (label + ": bytes are not the RFC 8785 canonical form")
        | Error e -> fail Codes.jsonMalformed (label + ": " + e)

    let private seqResults (xs: Result<'T, ContractError> list) : Result<'T list, ContractError> = traverse id xs

    let private versionStrings (label: string) (keys: string list) (v: JValue) =
        match v with
        | JObj o ->
            keys
            |> List.tryPick (fun k ->
                match Json.field k o with
                | Some(JStr s) when s <> "1.0.0" -> Some(ContractError.make Codes.unsupportedVersion (sprintf "%s.%s is '%s', not 1.0.0" label k s))
                | _ -> None)
        | _ -> None

    let private bytesOf (text: string) = Json.strictUtf8.GetBytes text

    /// Read and verify one investigation directory through every stage of CONTRACT §11.1, in order, reporting the
    /// first failing stage. Fails closed: nothing is partially loaded or repaired (§11.2).
    let read (engine: HistoryEngine) (raw: RawFileSet) : Result<Verified, ContractError> =
        result {
            // Stage 1: directory name and exactly the three files.
            do!
                if not (Uuid.isValid raw.DirectoryName) then fail Codes.pathInvalid ("directory name is not a canonical investigation id")
                else
                    let present = raw.Entries |> List.map fst
                    if (List.sort present) <> (List.sort names) then
                        fail Codes.pathInvalid ("the investigation directory must contain exactly events.ndjson, manifest.json and snapshot.json")
                    else Ok()
            let file name = raw.Entries |> List.find (fst >> (=) name) |> snd
            let manifestBytes, eventsBytes, snapshotBytes = file manifestName, file eventsName, file snapshotName
            // Stage 2: encoding and framing, all three files.
            let! manifestText = frameJson manifestName manifestBytes
            let! eventLines = frameEvents eventsBytes
            let! snapshotText = frameJson snapshotName snapshotBytes
            // Stage 3: JSON syntax.
            let! manifest = parse manifestName manifestText
            let! events = eventLines |> List.mapi (fun i l -> parse (sprintf "%s line %d" eventsName (i + 1)) l) |> seqResults
            let! snapshot = parse snapshotName snapshotText
            // Stage 4: canonical bytes.
            do! canonicalCheck manifestName manifestText manifest
            do! List.zip eventLines events |> List.mapi (fun i (l, e) -> canonicalCheck (sprintf "%s line %d" eventsName (i + 1)) l e) |> seqResults |> Result.map ignore
            do! canonicalCheck snapshotName snapshotText snapshot
            // Stage 5: unsupported version strings.
            let versionKeys = [ "formatVersion"; "schemaVersion"; "semanticVersion" ]
            do!
                match
                    versionStrings manifestName versionKeys manifest
                    |> Option.orElse (events |> List.tryPick (versionStrings eventsName versionKeys))
                    |> Option.orElse (versionStrings snapshotName versionKeys snapshot)
                with
                | Some e -> Error e
                | None -> Ok()
            // Stage 6: unknown event type (string discriminators only).
            do!
                match
                    events
                    |> List.tryPick (fun e ->
                        match e with
                        | JObj o ->
                            match Json.field "eventType" o with
                            | Some(JStr t) when not (Registry.isRegistered t) -> Some t
                            | _ -> None
                        | _ -> None)
                with
                | Some t -> fail Codes.unknownEventType ("event type '" + t + "' is not registered")
                | None -> Ok()
            // Stage 7: schemas.
            do!
                match
                    Schema.manifest manifest
                    |> Option.orElse (events |> List.mapi (fun i e -> i, e) |> List.tryPick (fun (i, e) -> Schema.event e |> Option.map (sprintf "%s line %d: %s" eventsName (i + 1))))
                    |> Option.orElse (Schema.snapshot snapshot)
                with
                | Some reason -> fail Codes.schemaViolation reason
                | None -> Ok()
            let mo = Json.obj manifest |> Option.defaultValue []
            let eo = events |> List.map (Json.obj >> Option.defaultValue [])
            let so = Json.obj snapshot |> Option.defaultValue []
            let count = int64 events.Length
            let manifestId = Json.str "investigationId" mo |> Option.defaultValue ""
            // Stage 8: manifest consistency.
            do!
                if manifestId <> raw.DirectoryName then fail Codes.manifestMismatch "manifest.investigationId differs from the directory name"
                elif Json.int "aggregateVersion" mo <> Some count || Json.int "eventCount" mo <> Some count then
                    fail Codes.manifestMismatch "manifest counts differ from the number of event lines"
                elif Json.str "createdAt" mo <> Json.str "occurredAt" eo.Head then fail Codes.manifestMismatch "manifest.createdAt differs from the first event"
                elif Json.str "updatedAt" mo <> Json.str "occurredAt" (List.last eo) then fail Codes.manifestMismatch "manifest.updatedAt differs from the last event"
                else Ok()
            // Stage 9: events digest over exact bytes.
            let eventsSha = Json.sha256Hex eventsBytes
            do! if Json.str "eventsSha256" mo <> Some eventsSha then fail Codes.eventsHashMismatch "eventsSha256 does not match events.ndjson" else Ok()
            // Stage 10: every event names this investigation.
            do!
                if eo |> List.exists (fun e -> Json.str "investigationId" e <> Some manifestId) then
                    fail Codes.investigationMismatch "an event names another investigation"
                else Ok()
            // Stage 11: duplicate event ids.
            let ids = eo |> List.map (Json.str "eventId" >> Option.defaultValue "")
            do! if (List.distinct ids).Length <> ids.Length then fail Codes.eventIdDuplicate "duplicate eventId" else Ok()
            // Stage 12: versions exactly 1..N in file order.
            do!
                if eo |> List.mapi (fun i e -> Json.int "aggregateVersion" e = Some(int64 (i + 1))) |> List.forall id then Ok()
                else fail Codes.sequenceInvalid "aggregateVersion is not exactly 1..N in file order"
            // Stage 13: event id derivation.
            do!
                match
                    eo
                    |> List.tryFind (fun e ->
                        let expected =
                            Uuid.eventId manifestId (Json.int "aggregateVersion" e |> Option.defaultValue 0L) (Json.str "correlationId" e |> Option.defaultValue "")
                        Json.str "eventId" e <> Some expected)
                with
                | Some e -> fail Codes.eventIdMismatch (sprintf "eventId at version %d is not the §5.2 derivation" (Json.int "aggregateVersion" e |> Option.defaultValue 0L))
                | None -> Ok()
            // Stage 14: a correlationId names exactly one (adjacent) group.
            do!
                let groups =
                    eo
                    |> List.map (Json.str "correlationId" >> Option.defaultValue "")
                    |> List.fold (fun acc c ->
                        match acc with
                        | last :: _ when last = c -> acc
                        | _ -> c :: acc) []
                if (List.distinct groups).Length <> groups.Length then fail Codes.correlationReused "a correlationId names two non-adjacent command groups"
                else Ok()
            // Stage 15: domain admissibility, decided by the engine (never by this reader).
            let admissibility = engine.Evaluate events
            do!
                match admissibility with
                | Inadmissible(code, detail) -> Error { Code = Codes.historyInadmissible; Detail = detail; DomainCode = Some code }
                | _ -> Ok()
            // Stage 16: snapshot digest.
            let snapshotSha = Json.sha256OfText snapshotText
            do! if Json.str "snapshotSha256" mo <> Some snapshotSha then fail Codes.snapshotHashMismatch "snapshotSha256 does not match snapshot.json" else Ok()
            // Stage 17: snapshot is exactly the projection of the replayed history.
            let! expected =
                match Projection.snapshot events with
                | Some s -> Ok s
                | None -> fail Codes.snapshotMismatch "the history does not project to a state"
            do! if Json.canonical expected <> Ok snapshotText then fail Codes.snapshotMismatch "snapshot.json is not the projection of the history" else Ok()
            let state = Json.field "state" so |> Option.defaultValue JNull
            return
                { InvestigationId = manifestId
                  Version = count
                  Title = (match state with JObj st -> Json.str "title" st | _ -> None) |> Option.defaultValue ""
                  Lifecycle = (match state with JObj st -> Json.str "lifecycle" st | _ -> None) |> Option.defaultValue ""
                  Events = events
                  EventLines = eventLines
                  State = state
                  StateSha256 = Json.sha256OfText (Json.canonicalText state)
                  EventsSha256 = eventsSha
                  ManifestSha256 = Json.sha256OfText manifestText
                  SnapshotSha256 = snapshotSha
                  Manifest = manifestBytes
                  EventsFile = eventsBytes
                  Snapshot = snapshotBytes
                  Admissibility = admissibility }
        }

    /// Build the event envelope for one engine-decided event: the eventId is derived (§5.2) and the versions fixed.
    let envelope (investigationId: string) (aggregateVersion: int64) (correlationId: string) (occurredAt: string) (actor: JValue) (eventType: string) (payload: JValue) =
        JObj
            [ "formatVersion", JStr ContractVersion.formatVersion
              "eventId", JStr(Uuid.eventId investigationId aggregateVersion correlationId)
              "investigationId", JStr investigationId
              "aggregateVersion", JInt aggregateVersion
              "eventType", JStr eventType
              "occurredAt", JStr occurredAt
              "actor", actor
              "semanticVersion", JStr ContractVersion.semanticVersion
              "correlationId", JStr correlationId
              "payload", payload ]

    /// Optimistic append (§9.2): refuse a stale expected version before reading the new lines; otherwise build the
    /// combined file set and verify it exactly as a reader would before returning it. Nothing is written here.
    let append (engine: HistoryEngine) (investigationId: string) (current: Verified option) (expectedVersion: int64) (newEvents: JValue list) : Result<Verified, ContractError> =
        result {
            let currentVersion = current |> Option.map (fun v -> v.Version) |> Option.defaultValue 0L
            do!
                if expectedVersion <> currentVersion then
                    fail Codes.versionStale (sprintf "the change was decided at version %d but the investigation is at version %d" expectedVersion currentVersion)
                else Ok()
            do! if List.isEmpty newEvents then fail Codes.unrepresentable "an append carries at least one event" else Ok()
            let! newLines =
                newEvents
                |> List.mapi (fun i e ->
                    match Json.canonical e with
                    | Error m -> fail Codes.unrepresentable m
                    | Ok text ->
                        match Schema.event e with
                        | Some reason -> fail Codes.schemaViolation (sprintf "appended line %d: %s" (currentVersion + int64 i + 1L) reason)
                        | None -> Ok text)
                |> traverse id
            let allEvents = (current |> Option.map (fun v -> v.Events) |> Option.defaultValue []) @ newEvents
            let lines = (current |> Option.map (fun v -> v.EventLines) |> Option.defaultValue []) @ newLines
            let eventsBytes = bytesOf (String.Join("", lines |> List.map (fun l -> l + "\n")))
            let! snapshot =
                match Projection.snapshot allEvents with
                | Some s -> Ok s
                | None -> fail Codes.unrepresentable "the history does not start an investigation"
            let snapshotText = Json.canonicalText snapshot
            let first = allEvents.Head |> Json.obj |> Option.defaultValue []
            let last = List.last allEvents |> Json.obj |> Option.defaultValue []
            let manifest =
                JObj
                    [ "aggregateVersion", JInt(int64 allEvents.Length)
                      "createdAt", Json.field "occurredAt" first |> Option.defaultValue JNull
                      "domainPacks", JArr []
                      "eventCount", JInt(int64 allEvents.Length)
                      "eventsSha256", JStr(Json.sha256Hex eventsBytes)
                      "formatVersion", JStr ContractVersion.formatVersion
                      "investigationId", JStr investigationId
                      "schemaVersion", JStr ContractVersion.schemaVersion
                      "semanticVersion", JStr ContractVersion.semanticVersion
                      "snapshotSha256", JStr(Json.sha256OfText snapshotText)
                      "snapshotVersion", JInt 1L
                      "updatedAt", Json.field "occurredAt" last |> Option.defaultValue JNull ]
            let raw =
                { DirectoryName = investigationId
                  Entries =
                    [ manifestName, bytesOf (Json.canonicalText manifest + "\n")
                      eventsName, eventsBytes
                      snapshotName, bytesOf (snapshotText + "\n") ] }
            return! read engine raw
        }

    /// A successor file set is acceptable only if its events start with the previously accepted bytes (§9.3).
    let successor (previous: Verified) (candidate: Verified) : Result<Verified, ContractError> =
        let p, c = previous.EventsFile, candidate.EventsFile
        if c.Length >= p.Length && Array.sub c 0 p.Length = p then Ok candidate
        else fail Codes.historyRewritten "the newer events.ndjson does not start with the previously accepted history"

    /// The provider-neutral files of a verified set, keyed by name.
    let files (v: Verified) = [ manifestName, v.Manifest; eventsName, v.EventsFile; snapshotName, v.Snapshot ]
