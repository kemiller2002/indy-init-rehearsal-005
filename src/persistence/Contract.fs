namespace Causa.Persistence

open System
open System.Globalization
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions

/// A contract refusal: the primary code of the first failing stage (causa-data CONTRACT §11) and a safe detail.
/// Details never contain credentials (they are built only from repository content and fixed text).
type ContractError =
    { Code: string
      Detail: string
      /// For CAUSA.HISTORY_INADMISSIBLE, the engine's domain code (§11.3).
      DomainCode: string option }

[<RequireQualifiedAccess>]
module Codes =
    let pathInvalid = "CAUSA.PATH_INVALID"
    let encoding = "CAUSA.ENCODING"
    let jsonMalformed = "CAUSA.JSON_MALFORMED"
    let nonCanonical = "CAUSA.NON_CANONICAL"
    let unsupportedVersion = "CAUSA.UNSUPPORTED_VERSION"
    let unknownEventType = "CAUSA.UNKNOWN_EVENT_TYPE"
    let schemaViolation = "CAUSA.SCHEMA_VIOLATION"
    let manifestMismatch = "CAUSA.MANIFEST_MISMATCH"
    let eventsHashMismatch = "CAUSA.EVENTS_HASH_MISMATCH"
    let investigationMismatch = "CAUSA.INVESTIGATION_MISMATCH"
    let eventIdDuplicate = "CAUSA.EVENT_ID_DUPLICATE"
    let sequenceInvalid = "CAUSA.SEQUENCE_INVALID"
    let eventIdMismatch = "CAUSA.EVENT_ID_MISMATCH"
    let correlationReused = "CAUSA.CORRELATION_REUSED"
    let historyInadmissible = "CAUSA.HISTORY_INADMISSIBLE"
    let snapshotHashMismatch = "CAUSA.SNAPSHOT_HASH_MISMATCH"
    let snapshotMismatch = "CAUSA.SNAPSHOT_MISMATCH"
    let versionStale = "CAUSA.VERSION_STALE"
    let historyRewritten = "CAUSA.HISTORY_REWRITTEN"
    let unrepresentable = "CAUSA.UNREPRESENTABLE"
    /// Catalog level (historical catalog manifest vs the investigation read from the repository).
    let catalogDrift = "CAUSA.FIXTURE_DRIFT"

[<RequireQualifiedAccess>]
module ContractError =
    let make code detail = { Code = code; Detail = detail; DomainCode = None }
    let fail code detail : Result<'T, ContractError> = Error(make code detail)

/// Contract identity (CONTRACT §1, header).
[<RequireQualifiedAccess>]
module ContractVersion =
    let formatVersion = "1.0.0"
    let schemaVersion = "1.0.0"
    let semanticVersion = "1.0.0"

[<RequireQualifiedAccess>]
module Uuid =
    let private pattern =
        Regex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\z", RegexOptions.CultureInvariant)

    let nil = "00000000-0000-0000-0000-000000000000"

    /// Lowercase canonical, non-nil (CONTRACT §2.8). `\z`, not `$` (§2.13).
    let isValid (s: string) = pattern.IsMatch s && s <> nil

    let private toBytes (uuid: string) =
        Convert.FromHexString(uuid.Replace("-", "", StringComparison.Ordinal))

    let private ofBytes (b: byte[]) =
        let h = Convert.ToHexStringLower b
        String.Join("-", [| h.Substring(0, 8); h.Substring(8, 4); h.Substring(12, 4); h.Substring(16, 4); h.Substring(20, 12) |])

    /// RFC 4122 name-based UUID, SHA-1 (version 5). Network byte order throughout.
    let v5 (ns: string) (name: string) =
        let data = Array.append (toBytes ns) (Encoding.UTF8.GetBytes name)
        let hash = SHA1.HashData data
        let b = Array.sub hash 0 16
        b[6] <- (b[6] &&& 0x0Fuy) ||| 0x50uy
        b[8] <- (b[8] &&& 0x3Fuy) ||| 0x80uy
        ofBytes b

    /// N = UUIDv5(NAMESPACE_URL, "https://github.com/kemiller2002/causa-data/contract/v1/event-id") (§5.2).
    let eventIdNamespace = "241fefd4-c810-5a3a-9cfa-4bbd968ae1cb"

    /// eventId = UUIDv5(N, investigationId + "/" + aggregateVersion + "/" + correlationId) (§5.2).
    let eventId (investigationId: string) (aggregateVersion: int64) (correlationId: string) =
        v5 eventIdNamespace (investigationId + "/" + aggregateVersion.ToString(CultureInfo.InvariantCulture) + "/" + correlationId)

[<RequireQualifiedAccess>]
module Timestamp =
    let private pattern =
        Regex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z\z", RegexOptions.CultureInvariant)

    /// RFC 3339 UTC, exactly three fractional digits, a real instant in 0001–9999 (§2.9).
    let isValid (s: string) =
        pattern.IsMatch s
        && fst (
            DateTime.TryParseExact(
                s,
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal ||| DateTimeStyles.AssumeUniversal
            )
        )

    /// Millisecond-truncated UTC text; the command boundary must supply this form (§2.9).
    let ofDateTimeOffset (d: DateTimeOffset) =
        d.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)

[<RequireQualifiedAccess>]
module Text =
    let private engineWhitespace (c: char) =
        (c >= '\u0009' && c <= '\u000D')
        || c = ' ' || c = '\u0085' || c = ' ' || c = ' '
        || (c >= ' ' && c <= ' ')
        || c = ' ' || c = ' ' || c = ' ' || c = ' ' || c = '　'

    /// Non-blank under the engine whitespace set, per UTF-16 code unit (§2.6).
    let isNonBlank (s: string) = s |> Seq.exists (engineWhitespace >> not)

/// The closed enumerations (§6).
[<RequireQualifiedAccess>]
module Enums =
    let lifecycle = [ "draft"; "active"; "awaitingEvidence"; "awaitingVerification"; "resolved"; "closed"; "archived" ]
    let hypothesisState = [ "proposed"; "plausible"; "supported"; "weakened"; "falsified"; "superseded" ]
    let materiality = [ "blocking"; "material"; "nonMaterial"; "informational" ]
    let actionKind = [ "test"; "inspection"; "query"; "comparison"; "experiment" ]
    let causeRole = [ "rootCause"; "contributingFactor"; "trigger"; "enablingCondition" ]
    let correctiveActionKind = [ "mitigation"; "workaround"; "correction"; "prevention" ]
    let evidenceCategory = [ "directObservation"; "measurement"; "log"; "document"; "experimentResult"; "testimony"; "externalReference" ]
    let verificationStatus = [ "unverified"; "partiallyVerified"; "verified"; "invalidated" ]
    let relationshipKind = [ "supports"; "contradicts"; "discriminates"; "insufficient" ]
    let predictionStrength = [ "necessary"; "expected"; "discriminating"; "nonDiscriminating" ]
    let predictionTiming = [ "prospective"; "retrospective" ]
    let verificationOutcome = [ "satisfied"; "failed"; "inconclusive" ]
    let conclusionStrength = [ "plausibleExplanation"; "leadingExplanation"; "supportedCause"; "confirmedCause"; "acceptedWithResidualUncertainty" ]
    let proposalKind = [ "hypothesis"; "unknown"; "action" ]
    let hypothesisCommand = [ "markPlausible"; "markSupported"; "markWeakened"; "falsify"; "supersede"; "reopen" ]
    let lifecycleCommand = [ "activateInvestigation"; "markAwaitingEvidence"; "markAwaitingVerification"; "resolveInvestigation"; "closeInvestigation"; "reopenInvestigation"; "verificationFailed" ]
    let contentKind = [ "text"; "artifact" ]
    let lifecycleTarget = [ "active"; "awaitingEvidence"; "awaitingVerification"; "resolved"; "closed" ]
    let hypothesisTarget = [ "plausible"; "supported"; "weakened"; "falsified"; "superseded" ]
    let unknownTarget = [ "investigating"; "resolved"; "acceptedResidual" ]
    let assumptionTarget = [ "provisionallyAccepted"; "verified"; "rejected" ]
    let contradictionTarget = [ "triaged"; "resolved"; "acceptedResidual" ]
    let actionTarget = [ "approved"; "rejected"; "inProgress"; "completed"; "cancelled" ]
    let correctiveActionTarget = [ "approved"; "cancelled"; "inProgress"; "implemented"; "awaitingVerificationOfEffect"; "verificationFailed"; "verifiedEffective"; "reworkRequired" ]

/// Wire field kinds of the event payloads (§7).
type FieldKind =
    | FUuid
    | FText
    | FEnum of string list
    | FTimestamp
    | FUuidList of minItems: int
    | FMembers

type PayloadField = { Name: string; Kind: FieldKind; Nullable: bool }

/// The 31 registered payload shapes (§7). Closed: every key required, no others allowed.
[<RequireQualifiedAccess>]
module Registry =
    let private f name kind = { Name = name; Kind = kind; Nullable = false }
    let private n name kind = { Name = name; Kind = kind; Nullable = true }

    let payloads: (string * PayloadField list) list =
        [ "InvestigationStarted", [ f "title" FText; f "observationId" FUuid; f "original" FText ]
          "LifecycleChanged", [ f "from" (FEnum Enums.lifecycle); f "to" (FEnum Enums.lifecycleTarget); f "command" (FEnum Enums.lifecycleCommand); n "reason" FText ]
          "ObservationRecorded", [ f "observationId" FUuid; f "original" FText; n "observedAt" FTimestamp ]
          "HypothesisRecorded", [ f "hypothesisId" FUuid; f "statement" FText; n "mechanism" FText ]
          "HypothesisTransitioned",
          [ f "hypothesisId" FUuid; f "from" (FEnum Enums.hypothesisState); f "to" (FEnum Enums.hypothesisTarget)
            f "command" (FEnum Enums.hypothesisCommand); f "reason" FText; f "evidenceIds" (FUuidList 0) ]
          "EvidenceAdmitted",
          [ f "evidenceId" FUuid; f "contentKind" (FEnum Enums.contentKind); f "content" FText
            f "category" (FEnum Enums.evidenceCategory); f "source" FText; n "capturedAt" FTimestamp ]
          "EvidenceSuperseded", [ f "evidenceId" FUuid; f "replacementEvidenceId" FUuid; f "reason" FText ]
          "EvidenceVerificationAssessed", [ f "evidenceId" FUuid; f "verification" (FEnum Enums.verificationStatus) ]
          "EvidenceRelated", [ f "relationshipId" FUuid; f "evidenceId" FUuid; f "hypothesisId" FUuid; f "kind" (FEnum Enums.relationshipKind); n "rationale" FText ]
          "UnknownIdentified", [ f "unknownId" FUuid; f "question" FText; f "materiality" (FEnum Enums.materiality) ]
          "UnknownTransitioned", [ f "unknownId" FUuid; f "state" (FEnum Enums.unknownTarget); f "basis" FText ]
          "AssumptionRecorded", [ f "assumptionId" FUuid; f "proposition" FText; f "materiality" (FEnum Enums.materiality) ]
          "AssumptionTransitioned", [ f "assumptionId" FUuid; f "state" (FEnum Enums.assumptionTarget) ]
          "ContradictionRecorded", [ f "contradictionId" FUuid; f "statement" FText; f "evidenceIds" (FUuidList 0); f "materiality" (FEnum Enums.materiality) ]
          "ContradictionTransitioned", [ f "contradictionId" FUuid; f "state" (FEnum Enums.contradictionTarget); f "basis" FText ]
          "PredictionRecorded",
          [ f "predictionId" FUuid; f "hypothesisId" FUuid; f "statement" FText
            f "strength" (FEnum Enums.predictionStrength); f "timing" (FEnum Enums.predictionTiming) ]
          "ActionRecorded", [ f "actionId" FUuid; f "kind" (FEnum Enums.actionKind); f "purpose" FText; f "targetHypothesisIds" (FUuidList 0) ]
          "ActionTransitioned", [ f "actionId" FUuid; f "state" (FEnum Enums.actionTarget) ]
          "FindingRecorded", [ f "findingId" FUuid; f "actionId" FUuid; f "originalResult" FText; n "interpretation" FText ]
          "NegativeKnowledgeEstablished", [ f "negativeKnowledgeId" FUuid; f "statement" FText; f "scope" FText; f "basisEvidenceIds" (FUuidList 1) ]
          "CausalAssertionRecorded", [ f "causeId" FUuid; f "outcome" FText; f "members" FMembers; n "mechanism" FText ]
          "CausalAssertionConfirmed", [ f "causeId" FUuid; f "policy" FText ]
          "CausalAssertionReopened", [ f "causeId" FUuid; f "reason" FText ]
          "CorrectiveActionRecorded",
          [ f "correctiveActionId" FUuid; f "kind" (FEnum Enums.correctiveActionKind); f "description" FText; f "targetCauseIds" (FUuidList 0) ]
          "CorrectiveActionTransitioned", [ f "correctiveActionId" FUuid; f "state" (FEnum Enums.correctiveActionTarget) ]
          "CriterionRecorded", [ f "criterionId" FUuid; f "correctiveActionId" FUuid; f "expectedOutcome" FText; n "method" FText ]
          "VerificationRecorded", [ f "verificationId" FUuid; f "criterionId" FUuid; f "outcome" (FEnum Enums.verificationOutcome); f "evidenceIds" (FUuidList 0) ]
          "ConclusionRecorded",
          [ f "conclusionId" FUuid; f "statement" FText; f "strength" (FEnum Enums.conclusionStrength); f "causeIds" (FUuidList 0); n "residualUncertainty" FText ]
          "ProposalRecorded", [ f "proposalId" FUuid; f "kind" (FEnum Enums.proposalKind); f "text" FText; n "rationale" FText ]
          "ProposalWasAccepted", [ f "proposalId" FUuid; f "targetId" FUuid ]
          "ProposalWasRejected", [ f "proposalId" FUuid; f "reason" FText ] ]

    let private table = Map.ofList payloads

    let isRegistered (eventType: string) = table.ContainsKey eventType

    let fields (eventType: string) = Map.tryFind eventType table

/// Structural (schema-level, stage 7) checks. They return a reason; the caller assigns the code.
[<RequireQualifiedAccess>]
module Schema =
    let private keysExactly (expected: string list) (o: (string * JValue) list) =
        let actual = o |> List.map fst |> Set.ofList
        let wanted = Set.ofList expected
        if actual = wanted then None
        else
            let missing = Set.difference wanted actual |> Set.toList
            let extra = Set.difference actual wanted |> Set.toList
            Some(sprintf "keys differ (missing: %s; unexpected: %s)" (String.Join(",", missing)) (String.Join(",", extra)))

    let private textOk v =
        match v with
        | JStr s -> Text.isNonBlank s
        | _ -> false

    let private uuidOk v =
        match v with
        | JStr s -> Uuid.isValid s
        | _ -> false

    let private distinctUuids (items: JValue list) =
        items |> List.forall uuidOk && (items |> List.distinct |> List.length) = items.Length

    let private memberOk v =
        match v with
        | JObj o ->
            keysExactly [ "hypothesisId"; "role" ] o |> Option.isNone
            && (Json.field "hypothesisId" o |> Option.exists uuidOk)
            && (match Json.field "role" o with
                | Some(JStr r) -> List.contains r Enums.causeRole
                | _ -> false)
        | _ -> false

    let private valueOk (kind: FieldKind) (v: JValue) =
        match kind, v with
        | FUuid, _ -> uuidOk v
        | FText, _ -> textOk v
        | FEnum values, JStr s -> List.contains s values
        | FTimestamp, JStr s -> Timestamp.isValid s
        | FUuidList minItems, JArr items -> items.Length >= minItems && distinctUuids items
        | FMembers, JArr items -> items.Length >= 1 && List.forall memberOk items && (List.distinct items).Length = items.Length
        | _ -> false

    let actor (eventType: string) (v: JValue) : string option =
        match v with
        | JObj o ->
            match Json.field "kind" o with
            | Some(JStr("human" | "system")) ->
                match keysExactly [ "actorId"; "kind" ] o with
                | Some r -> Some("actor " + r)
                | None -> if Json.field "actorId" o |> Option.exists textOk then None else Some "actor.actorId must be non-blank text"
            | Some(JStr "agent") ->
                match keysExactly [ "actorId"; "kind"; "provider"; "model" ] o with
                | Some r -> Some("actor " + r)
                | None ->
                    if not ([ "actorId"; "provider"; "model" ] |> List.forall (fun k -> Json.field k o |> Option.exists textOk)) then
                        Some "agent actor fields must be non-blank text"
                    elif eventType <> "ProposalRecorded" then
                        Some "an agent actor may appear only on ProposalRecorded (D-004)"
                    else None
            | _ -> Some "actor.kind must be human, system or agent"
        | _ -> Some "actor must be an object"

    /// Payload invariants that the schemas carry as if/then rules (§7).
    let private invariants (eventType: string) (o: (string * JValue) list) : string option =
        let s k = Json.str k o
        let isNull k = Json.field k o = Some JNull
        let nonEmpty k =
            match Json.field k o with
            | Some(JArr(_ :: _)) -> true
            | _ -> false
        match eventType with
        | "HypothesisTransitioned" ->
            let expectedTo =
                match s "command" with
                | Some "markPlausible" | Some "reopen" -> Some "plausible"
                | Some "markSupported" -> Some "supported"
                | Some "markWeakened" -> Some "weakened"
                | Some "falsify" -> Some "falsified"
                | Some "supersede" -> Some "superseded"
                | _ -> None
            if expectedTo.IsSome && s "to" <> expectedTo then Some "HypothesisTransitioned: 'to' does not match 'command'"
            elif (match s "command" with
                  | Some("markSupported" | "markWeakened" | "falsify" | "reopen") -> not (nonEmpty "evidenceIds")
                  | _ -> false) then
                Some "HypothesisTransitioned: this command must cite evidence"
            else None
        | "LifecycleChanged" ->
            let rule =
                match s "command" with
                | Some "activateInvestigation" -> Some("active", false)
                | Some "markAwaitingEvidence" -> Some("awaitingEvidence", true)
                | Some "markAwaitingVerification" -> Some("awaitingVerification", false)
                | Some "resolveInvestigation" -> Some("resolved", false)
                | Some "closeInvestigation" -> Some("closed", false)
                | Some "reopenInvestigation" -> Some("active", true)
                | Some "verificationFailed" -> Some("active", true)
                | _ -> None
            match rule with
            | Some(target, reasonRequired) ->
                if s "to" <> Some target then Some "LifecycleChanged: 'to' does not match 'command'"
                elif reasonRequired && isNull "reason" then Some "LifecycleChanged: this command requires a reason"
                elif not reasonRequired && not (isNull "reason") then Some "LifecycleChanged: this command carries no reason"
                elif s "command" = Some "verificationFailed" && s "from" <> Some "awaitingVerification" then
                    Some "LifecycleChanged: verificationFailed must come from awaitingVerification"
                else None
            | None -> None
        | "EvidenceAdmitted" ->
            match s "contentKind", s "content" with
            | Some "artifact", Some c when not (c.StartsWith("http://", StringComparison.Ordinal) || c.StartsWith("https://", StringComparison.Ordinal)) ->
                Some "EvidenceAdmitted: artifact content must be an absolute http(s) URL"
            | _ -> None
        | "VerificationRecorded" when s "outcome" = Some "satisfied" && not (nonEmpty "evidenceIds") ->
            Some "VerificationRecorded: a satisfied outcome must cite evidence"
        | _ -> None

    let payload (eventType: string) (v: JValue) : string option =
        match Registry.fields eventType, v with
        | None, _ -> Some("unregistered event type " + eventType)
        | Some fields, JObj o ->
            match keysExactly (fields |> List.map (fun f -> f.Name)) o with
            | Some r -> Some(eventType + " payload " + r)
            | None ->
                fields
                |> List.tryPick (fun fd ->
                    match Json.field fd.Name o with
                    | Some JNull when fd.Nullable -> None
                    | Some value when valueOk fd.Kind value -> None
                    | _ -> Some(sprintf "%s.%s is not a valid value" eventType fd.Name))
                |> Option.orElse (invariants eventType o)
        | Some _, _ -> Some(eventType + " payload must be an object")

    let envelopeKeys =
        [ "formatVersion"; "eventId"; "investigationId"; "aggregateVersion"; "eventType"; "occurredAt"; "actor"; "semanticVersion"; "correlationId"; "payload" ]

    let event (v: JValue) : string option =
        match v with
        | JObj o ->
            match keysExactly envelopeKeys o with
            | Some r -> Some("event " + r)
            | None ->
                let eventType = Json.str "eventType" o |> Option.defaultValue ""
                if Json.str "formatVersion" o <> Some ContractVersion.formatVersion then Some "event.formatVersion must be \"1.0.0\""
                elif Json.str "semanticVersion" o <> Some ContractVersion.semanticVersion then Some "event.semanticVersion must be \"1.0.0\""
                elif not (Json.field "eventId" o |> Option.exists uuidOk) then Some "event.eventId must be a canonical UUID"
                elif not (Json.field "investigationId" o |> Option.exists uuidOk) then Some "event.investigationId must be a canonical UUID"
                elif not (Json.field "correlationId" o |> Option.exists uuidOk) then Some "event.correlationId must be a canonical UUID"
                elif not (match Json.int "aggregateVersion" o with Some n -> n >= 1L | None -> false) then Some "event.aggregateVersion must be a positive integer"
                elif not (Json.str "occurredAt" o |> Option.exists Timestamp.isValid) then Some "event.occurredAt must be a millisecond UTC timestamp"
                elif not (Registry.isRegistered eventType) then Some "event.eventType must be a registered string"
                else
                    match Json.field "actor" o with
                    | Some a ->
                        actor eventType a
                        |> Option.orElse (Json.field "payload" o |> Option.bind (payload eventType))
                    | None -> Some "event.actor is missing"
        | _ -> Some "event must be an object"

    let manifestKeys =
        [ "aggregateVersion"; "createdAt"; "domainPacks"; "eventCount"; "eventsSha256"; "formatVersion"; "investigationId"
          "schemaVersion"; "semanticVersion"; "snapshotSha256"; "snapshotVersion"; "updatedAt" ]

    let private sha256Pattern = Regex(@"^[0-9a-f]{64}\z", RegexOptions.CultureInvariant)
    let isSha256 (s: string) = sha256Pattern.IsMatch s

    let manifest (v: JValue) : string option =
        match v with
        | JObj o ->
            match keysExactly manifestKeys o with
            | Some r -> Some("manifest " + r)
            | None ->
                if not (Json.field "investigationId" o |> Option.exists uuidOk) then Some "manifest.investigationId must be a canonical UUID"
                elif not ([ "aggregateVersion"; "eventCount" ] |> List.forall (fun k -> match Json.int k o with Some n -> n >= 1L | None -> false)) then
                    Some "manifest counts must be positive integers"
                elif not ([ "eventsSha256"; "snapshotSha256" ] |> List.forall (fun k -> Json.str k o |> Option.exists isSha256)) then
                    Some "manifest digests must be lowercase SHA-256 hex"
                elif not ([ "createdAt"; "updatedAt" ] |> List.forall (fun k -> Json.str k o |> Option.exists Timestamp.isValid)) then
                    Some "manifest times must be millisecond UTC timestamps"
                elif Json.int "snapshotVersion" o <> Some 1L then Some "manifest.snapshotVersion must be 1"
                elif Json.field "domainPacks" o <> Some(JArr []) then Some "manifest.domainPacks must be [] in v1"
                else None
        | _ -> Some "manifest must be an object"

    let snapshotKeys =
        [ "aggregateVersion"; "formatVersion"; "generatedAt"; "investigationId"; "projectionVersion"; "semanticVersion"; "state" ]

    let snapshot (v: JValue) : string option =
        match v with
        | JObj o ->
            match keysExactly snapshotKeys o with
            | Some r -> Some("snapshot " + r)
            | None ->
                if not (Json.field "investigationId" o |> Option.exists uuidOk) then Some "snapshot.investigationId must be a canonical UUID"
                elif not (match Json.int "aggregateVersion" o with Some n -> n >= 1L | None -> false) then Some "snapshot.aggregateVersion must be positive"
                elif Json.int "projectionVersion" o <> Some 1L then Some "snapshot.projectionVersion must be 1"
                elif not (Json.str "generatedAt" o |> Option.exists Timestamp.isValid) then Some "snapshot.generatedAt must be a millisecond UTC timestamp"
                else
                    match Json.field "state" o with
                    | Some(JObj _) -> None
                    | _ -> Some "snapshot.state must be an object"
        | _ -> Some "snapshot must be an object"
