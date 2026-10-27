namespace IndyInitRootCauseInvestigator.Engine

open System
open System.Globalization

/// The engine <-> Causa v1.0.0 wire mapping (contract §5–§8, FIELD-MAPPING.md). Every enumeration maps both ways
/// exhaustively; decoding is total over schema-valid input and returns an error otherwise (never throws).
[<RequireQualifiedAccess>]
module Wire =
    let formatVersion = "1.0.0"
    let semanticVersion = "1.0.0"

    // ---- enumerations ----

    let lifecycle =
        function
        | Draft -> "draft"
        | Active -> "active"
        | AwaitingEvidence -> "awaitingEvidence"
        | AwaitingVerification -> "awaitingVerification"
        | Resolved -> "resolved"
        | Closed -> "closed"
        | Archived -> "archived"

    let hypothesisState =
        function
        | Proposed -> "proposed"
        | Plausible -> "plausible"
        | Supported -> "supported"
        | Weakened -> "weakened"
        | Falsified -> "falsified"
        | Superseded -> "superseded"

    let unknownState =
        function
        | Identified -> "identified"
        | Investigating -> "investigating"
        | UnknownResolved -> "resolved"
        | AcceptedResidual -> "acceptedResidual"
        | UnknownSuperseded -> "superseded"

    let materiality =
        function
        | Blocking -> "blocking"
        | Material -> "material"
        | NonMaterial -> "nonMaterial"
        | Informational -> "informational"

    let assumptionState =
        function
        | ExplicitUnverified -> "explicitUnverified"
        | ProvisionallyAccepted -> "provisionallyAccepted"
        | AssumptionVerified -> "verified"
        | AssumptionRejected -> "rejected"
        | Expired -> "expired"

    let contradictionState =
        function
        | Detected -> "detected"
        | Triaged -> "triaged"
        | ContradictionInvestigating -> "investigating"
        | ContradictionResolved -> "resolved"
        | ContradictionAcceptedResidual -> "acceptedResidual"
        | ContradictionSuperseded -> "superseded"

    let actionState =
        function
        | ActionProposed -> "proposed"
        | ActionApproved -> "approved"
        | ActionInProgress -> "inProgress"
        | ActionCompleted -> "completed"
        | ActionInterpreted -> "interpreted"
        | ActionRejected -> "rejected"
        | ActionCancelled -> "cancelled"

    let actionKind =
        function
        | Test -> "test"
        | Inspection -> "inspection"
        | Query -> "query"
        | ActionKind.Comparison -> "comparison"
        | Experiment -> "experiment"

    let causeState =
        function
        | CauseProposed -> "proposed"
        | CauseConfirmed -> "confirmed"
        | CauseReopened -> "reopened"
        | CauseSuperseded -> "superseded"

    let causeRole =
        function
        | RootCause -> "rootCause"
        | ContributingFactor -> "contributingFactor"
        | Trigger -> "trigger"
        | EnablingCondition -> "enablingCondition"

    let correctiveActionState =
        function
        | CorrectionProposed -> "proposed"
        | CorrectionApproved -> "approved"
        | CorrectionInProgress -> "inProgress"
        | Implemented -> "implemented"
        | AwaitingVerificationOfEffect -> "awaitingVerificationOfEffect"
        | VerifiedEffective -> "verifiedEffective"
        | VerificationFailed -> "verificationFailed"
        | ReworkRequired -> "reworkRequired"
        | CorrectionCancelled -> "cancelled"

    let correctiveActionKind =
        function
        | Mitigation -> "mitigation"
        | Workaround -> "workaround"
        | Correction -> "correction"
        | Prevention -> "prevention"

    let evidenceCategory =
        function
        | DirectObservation -> "directObservation"
        | Measurement -> "measurement"
        | Log -> "log"
        | Document -> "document"
        | ExperimentResult -> "experimentResult"
        | Testimony -> "testimony"
        | ExternalReference -> "externalReference"

    let verificationStatus =
        function
        | Unverified -> "unverified"
        | PartiallyVerified -> "partiallyVerified"
        | Verified -> "verified"
        | Invalidated -> "invalidated"

    let relationshipKind =
        function
        | Supports -> "supports"
        | Contradicts -> "contradicts"
        | Discriminates -> "discriminates"
        | Insufficient -> "insufficient"

    let predictionStrength =
        function
        | Necessary -> "necessary"
        | Expected -> "expected"
        | DiscriminatingPrediction -> "discriminating"
        | NonDiscriminating -> "nonDiscriminating"

    let predictionTiming =
        function
        | Prospective -> "prospective"
        | Retrospective -> "retrospective"

    let verificationOutcome =
        function
        | Satisfied -> "satisfied"
        | Failed -> "failed"
        | Inconclusive -> "inconclusive"

    let conclusionStrength =
        function
        | PlausibleExplanation -> "plausibleExplanation"
        | LeadingExplanation -> "leadingExplanation"
        | SupportedCause -> "supportedCause"
        | ConfirmedCause -> "confirmedCause"
        | AcceptedWithResidualUncertainty -> "acceptedWithResidualUncertainty"

    let proposalKind =
        function
        | ProposedHypothesis -> "hypothesis"
        | ProposedUnknown -> "unknown"
        | ProposedAction -> "action"

    let proposalState =
        function
        | ProposalActive -> "active"
        | ProposalAccepted -> "accepted"
        | ProposalRejected -> "rejected"

    let hypothesisCommand =
        function
        | MarkPlausible -> "markPlausible"
        | MarkSupported -> "markSupported"
        | MarkWeakened -> "markWeakened"
        | Falsify -> "falsify"
        | Supersede -> "supersede"
        | Reopen -> "reopen"

    let lifecycleCommand =
        function
        | ActivateInvestigationCommand -> "activateInvestigation"
        | MarkAwaitingEvidenceCommand -> "markAwaitingEvidence"
        | MarkAwaitingVerificationCommand -> "markAwaitingVerification"
        | ResolveInvestigationCommand -> "resolveInvestigation"
        | CloseInvestigationCommand -> "closeInvestigation"
        | ReopenInvestigationCommand -> "reopenInvestigation"
        | VerificationFailedFollowOn -> "verificationFailed"

    /// The inverse of an exhaustive enumeration mapping, built from the mapping itself so the two cannot drift.
    let private inverse (encode: 'T -> string) (all: 'T list) =
        let table = all |> List.map (fun v -> encode v, v) |> Map.ofList
        fun (s: string) -> Map.tryFind s table

    let parseLifecycle = inverse lifecycle [ Draft; Active; AwaitingEvidence; AwaitingVerification; Resolved; Closed; Archived ]
    let parseHypothesisState = inverse hypothesisState [ Proposed; Plausible; Supported; Weakened; Falsified; Superseded ]
    let parseUnknownState = inverse unknownState [ Identified; Investigating; UnknownResolved; AcceptedResidual; UnknownSuperseded ]
    let parseMateriality = inverse materiality [ Blocking; Material; NonMaterial; Informational ]
    let parseAssumptionState = inverse assumptionState [ ExplicitUnverified; ProvisionallyAccepted; AssumptionVerified; AssumptionRejected; Expired ]
    let parseContradictionState =
        inverse contradictionState [ Detected; Triaged; ContradictionInvestigating; ContradictionResolved; ContradictionAcceptedResidual; ContradictionSuperseded ]
    let parseActionState =
        inverse actionState [ ActionProposed; ActionApproved; ActionInProgress; ActionCompleted; ActionInterpreted; ActionRejected; ActionCancelled ]
    let parseActionKind = inverse actionKind [ Test; Inspection; Query; ActionKind.Comparison; Experiment ]
    let parseCauseState = inverse causeState [ CauseProposed; CauseConfirmed; CauseReopened; CauseSuperseded ]
    let parseCauseRole = inverse causeRole [ RootCause; ContributingFactor; Trigger; EnablingCondition ]
    let parseCorrectiveActionState =
        inverse
            correctiveActionState
            [ CorrectionProposed
              CorrectionApproved
              CorrectionInProgress
              Implemented
              AwaitingVerificationOfEffect
              VerifiedEffective
              VerificationFailed
              ReworkRequired
              CorrectionCancelled ]
    let parseCorrectiveActionKind = inverse correctiveActionKind [ Mitigation; Workaround; Correction; Prevention ]
    let parseEvidenceCategory =
        inverse evidenceCategory [ DirectObservation; Measurement; Log; Document; ExperimentResult; Testimony; ExternalReference ]
    let parseVerificationStatus = inverse verificationStatus [ Unverified; PartiallyVerified; Verified; Invalidated ]
    let parseRelationshipKind = inverse relationshipKind [ Supports; Contradicts; Discriminates; Insufficient ]
    let parsePredictionStrength = inverse predictionStrength [ Necessary; Expected; DiscriminatingPrediction; NonDiscriminating ]
    let parsePredictionTiming = inverse predictionTiming [ Prospective; Retrospective ]
    let parseVerificationOutcome = inverse verificationOutcome [ Satisfied; Failed; Inconclusive ]
    let parseConclusionStrength =
        inverse conclusionStrength [ PlausibleExplanation; LeadingExplanation; SupportedCause; ConfirmedCause; AcceptedWithResidualUncertainty ]
    let parseProposalKind = inverse proposalKind [ ProposedHypothesis; ProposedUnknown; ProposedAction ]
    let parseProposalState = inverse proposalState [ ProposalActive; ProposalAccepted; ProposalRejected ]
    let parseHypothesisCommand = inverse hypothesisCommand [ MarkPlausible; MarkSupported; MarkWeakened; Falsify; Supersede; Reopen ]
    let parseLifecycleCommand =
        inverse
            lifecycleCommand
            [ ActivateInvestigationCommand
              MarkAwaitingEvidenceCommand
              MarkAwaitingVerificationCommand
              ResolveInvestigationCommand
              CloseInvestigationCommand
              ReopenInvestigationCommand
              VerificationFailedFollowOn ]

    /// The 31 registered event types, in registry order (contract §7).
    let eventTypes =
        [ "InvestigationStarted"; "LifecycleChanged"; "ObservationRecorded"; "HypothesisRecorded"; "HypothesisTransitioned"
          "EvidenceAdmitted"; "EvidenceSuperseded"; "EvidenceVerificationAssessed"; "EvidenceRelated"; "UnknownIdentified"
          "UnknownTransitioned"; "AssumptionRecorded"; "AssumptionTransitioned"; "ContradictionRecorded"; "ContradictionTransitioned"
          "PredictionRecorded"; "ActionRecorded"; "ActionTransitioned"; "FindingRecorded"; "NegativeKnowledgeEstablished"
          "CausalAssertionRecorded"; "CausalAssertionConfirmed"; "CausalAssertionReopened"; "CorrectiveActionRecorded"
          "CorrectiveActionTransitioned"; "CriterionRecorded"; "VerificationRecorded"; "ConclusionRecorded"; "ProposalRecorded"
          "ProposalWasAccepted"; "ProposalWasRejected" ]

    // ---- scalars ----

    let timestamp (t: DateTimeOffset) = t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)

    let parseTimestamp (s: string) : DateTimeOffset option =
        match
            DateTimeOffset.TryParseExact(
                s,
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal ||| DateTimeStyles.AdjustToUniversal
            )
        with
        | true, t -> Some t
        | _ -> None

    /// Whether an instant is representable on the wire: millisecond precision (contract §2.9).
    let isMillisecond (t: DateTimeOffset) = t.UtcTicks % TimeSpan.TicksPerMillisecond = 0L

    let uuid (g: Guid) = g.ToString("D")
    let private id (i: Id<'T>) = JString i.Text
    let private ids (xs: Id<'T> list) = JArray(xs |> List.map id)
    let private txt (t: Text) = JString t.Value
    let private otxt (t: Text option) = Json.opt txt t
    let private ts (t: DateTimeOffset) = JString(timestamp t)
    let private ots (t: DateTimeOffset option) = Json.opt ts t

    let actor (a: Actor) =
        match a.Kind with
        | Human -> JObject [ "actorId", JString a.ActorId; "kind", JString "human" ]
        | System -> JObject [ "actorId", JString a.ActorId; "kind", JString "system" ]
        | Agent(provider, model) ->
            JObject [ "actorId", JString a.ActorId; "kind", JString "agent"; "provider", JString provider; "model", JString model ]

    let private provenance (p: Provenance) = JObject [ "actor", actor p.Actor; "recordedAt", ts p.RecordedAt ]

    let private causalMember (m: CausalMember) =
        JObject [ "hypothesisId", id m.Hypothesis; "role", JString(causeRole m.Role) ]

    // ---- events ----

    let eventType (body: EventBody) =
        match body with
        | InvestigationStarted _ -> "InvestigationStarted"
        | LifecycleChanged _ -> "LifecycleChanged"
        | ObservationRecorded _ -> "ObservationRecorded"
        | HypothesisRecorded _ -> "HypothesisRecorded"
        | HypothesisTransitioned _ -> "HypothesisTransitioned"
        | EvidenceAdmitted _ -> "EvidenceAdmitted"
        | EvidenceSuperseded _ -> "EvidenceSuperseded"
        | EvidenceVerificationAssessed _ -> "EvidenceVerificationAssessed"
        | EvidenceRelated _ -> "EvidenceRelated"
        | UnknownIdentified _ -> "UnknownIdentified"
        | UnknownTransitioned _ -> "UnknownTransitioned"
        | AssumptionRecorded _ -> "AssumptionRecorded"
        | AssumptionTransitioned _ -> "AssumptionTransitioned"
        | ContradictionRecorded _ -> "ContradictionRecorded"
        | ContradictionTransitioned _ -> "ContradictionTransitioned"
        | PredictionRecorded _ -> "PredictionRecorded"
        | ActionRecorded _ -> "ActionRecorded"
        | ActionTransitioned _ -> "ActionTransitioned"
        | FindingRecorded _ -> "FindingRecorded"
        | NegativeKnowledgeEstablished _ -> "NegativeKnowledgeEstablished"
        | CausalAssertionRecorded _ -> "CausalAssertionRecorded"
        | CausalAssertionConfirmed _ -> "CausalAssertionConfirmed"
        | CausalAssertionReopened _ -> "CausalAssertionReopened"
        | CorrectiveActionRecorded _ -> "CorrectiveActionRecorded"
        | CorrectiveActionTransitioned _ -> "CorrectiveActionTransitioned"
        | CriterionRecorded _ -> "CriterionRecorded"
        | VerificationRecorded _ -> "VerificationRecorded"
        | ConclusionRecorded _ -> "ConclusionRecorded"
        | ProposalRecorded _ -> "ProposalRecorded"
        | ProposalWasAccepted _ -> "ProposalWasAccepted"
        | ProposalWasRejected _ -> "ProposalWasRejected"

    let payload (body: EventBody) : Json =
        match body with
        | InvestigationStarted(title, oid, original) -> JObject [ "title", txt title; "observationId", id oid; "original", txt original ]
        | LifecycleChanged(from, target, command, reason) ->
            JObject
                [ "from", JString(lifecycle from)
                  "to", JString(lifecycle target)
                  "command", JString(lifecycleCommand command)
                  "reason", otxt reason ]
        | ObservationRecorded(oid, t, at) -> JObject [ "observationId", id oid; "original", txt t; "observedAt", ots at ]
        | HypothesisRecorded(hid, s, m) -> JObject [ "hypothesisId", id hid; "statement", txt s; "mechanism", otxt m ]
        | HypothesisTransitioned(hid, from, target, command, reason, evidence) ->
            JObject
                [ "hypothesisId", id hid
                  "from", JString(hypothesisState from)
                  "to", JString(hypothesisState target)
                  "command", JString(hypothesisCommand command)
                  "reason", txt reason
                  "evidenceIds", ids evidence ]
        | EvidenceAdmitted(eid, content, category, source, at) ->
            let kind, value =
                match content with
                | TextContent t -> "text", t.Value
                | ArtifactContent u -> "artifact", u
            JObject
                [ "evidenceId", id eid
                  "contentKind", JString kind
                  "content", JString value
                  "category", JString(evidenceCategory category)
                  "source", txt source
                  "capturedAt", ots at ]
        | EvidenceSuperseded(old, replacement, reason) ->
            JObject [ "evidenceId", id old; "replacementEvidenceId", id replacement; "reason", txt reason ]
        | EvidenceVerificationAssessed(eid, s) -> JObject [ "evidenceId", id eid; "verification", JString(verificationStatus s) ]
        | EvidenceRelated(rid, eid, hid, kind, rationale) ->
            JObject
                [ "relationshipId", id rid
                  "evidenceId", id eid
                  "hypothesisId", id hid
                  "kind", JString(relationshipKind kind)
                  "rationale", otxt rationale ]
        | UnknownIdentified(uid, q, m) -> JObject [ "unknownId", id uid; "question", txt q; "materiality", JString(materiality m) ]
        | UnknownTransitioned(uid, s, b) -> JObject [ "unknownId", id uid; "state", JString(unknownState s); "basis", txt b ]
        | AssumptionRecorded(aid, p, m) -> JObject [ "assumptionId", id aid; "proposition", txt p; "materiality", JString(materiality m) ]
        | AssumptionTransitioned(aid, s) -> JObject [ "assumptionId", id aid; "state", JString(assumptionState s) ]
        | ContradictionRecorded(cid, s, ev, m) ->
            JObject [ "contradictionId", id cid; "statement", txt s; "evidenceIds", ids ev; "materiality", JString(materiality m) ]
        | ContradictionTransitioned(cid, s, b) -> JObject [ "contradictionId", id cid; "state", JString(contradictionState s); "basis", txt b ]
        | PredictionRecorded(pid, hid, s, st, t) ->
            JObject
                [ "predictionId", id pid
                  "hypothesisId", id hid
                  "statement", txt s
                  "strength", JString(predictionStrength st)
                  "timing", JString(predictionTiming t) ]
        | ActionRecorded(aid, k, p, t) ->
            JObject [ "actionId", id aid; "kind", JString(actionKind k); "purpose", txt p; "targetHypothesisIds", ids t ]
        | ActionTransitioned(aid, s) -> JObject [ "actionId", id aid; "state", JString(actionState s) ]
        | FindingRecorded(fid, aid, r, i) -> JObject [ "findingId", id fid; "actionId", id aid; "originalResult", txt r; "interpretation", otxt i ]
        | NegativeKnowledgeEstablished(nid, s, sc, b) ->
            JObject [ "negativeKnowledgeId", id nid; "statement", txt s; "scope", txt sc; "basisEvidenceIds", ids b ]
        | CausalAssertionRecorded(cid, o, members, m) ->
            JObject [ "causeId", id cid; "outcome", txt o; "members", JArray(members |> List.map causalMember); "mechanism", otxt m ]
        | CausalAssertionConfirmed(cid, policy) -> JObject [ "causeId", id cid; "policy", JString policy ]
        | CausalAssertionReopened(cid, r) -> JObject [ "causeId", id cid; "reason", txt r ]
        | CorrectiveActionRecorded(caid, k, d, t) ->
            JObject
                [ "correctiveActionId", id caid
                  "kind", JString(correctiveActionKind k)
                  "description", txt d
                  "targetCauseIds", ids t ]
        | CorrectiveActionTransitioned(caid, s) -> JObject [ "correctiveActionId", id caid; "state", JString(correctiveActionState s) ]
        | CriterionRecorded(crid, caid, e, m) ->
            JObject [ "criterionId", id crid; "correctiveActionId", id caid; "expectedOutcome", txt e; "method", otxt m ]
        | VerificationRecorded(vid, crid, o, ev) ->
            JObject [ "verificationId", id vid; "criterionId", id crid; "outcome", JString(verificationOutcome o); "evidenceIds", ids ev ]
        | ConclusionRecorded(cid, s, st, causes, r) ->
            JObject
                [ "conclusionId", id cid
                  "statement", txt s
                  "strength", JString(conclusionStrength st)
                  "causeIds", ids causes
                  "residualUncertainty", otxt r ]
        | ProposalRecorded(pid, k, t, r) -> JObject [ "proposalId", id pid; "kind", JString(proposalKind k); "text", txt t; "rationale", otxt r ]
        | ProposalWasAccepted(pid, target) -> JObject [ "proposalId", id pid; "targetId", JString(uuid target) ]
        | ProposalWasRejected(pid, r) -> JObject [ "proposalId", id pid; "reason", txt r ]

    /// The persisted envelope of one event (contract §5), with its derived eventId.
    let envelope (e: DomainEvent) : Json =
        JObject
            [ "formatVersion", JString formatVersion
              "eventId", JString(uuid (Hashing.eventId e.Investigation.Value e.Version e.CommandId))
              "investigationId", id e.Investigation
              "aggregateVersion", JInt e.Version
              "eventType", JString(eventType e.Body)
              "occurredAt", ts e.At
              "actor", actor e.Actor
              "semanticVersion", JString semanticVersion
              "correlationId", JString(uuid e.CommandId)
              "payload", payload e.Body ]

    /// Why a domain value has no v1 wire form (CAUSA.UNREPRESENTABLE, contract §2.7, §2.9, §7.2).
    let unrepresentable (e: DomainEvent) : string option =
        let times =
            [ yield e.At
              match e.Body with
              | ObservationRecorded(_, _, Some t)
              | EvidenceAdmitted(_, _, _, _, Some t) -> yield t
              | _ -> () ]
        let duplicates (xs: Id<'T> list) = (List.distinct xs).Length <> xs.Length
        let duplicateIds =
            match e.Body with
            | HypothesisTransitioned(_, _, _, _, _, ev)
            | ContradictionRecorded(_, _, ev, _)
            | VerificationRecorded(_, _, _, ev)
            | NegativeKnowledgeEstablished(_, _, _, ev) -> duplicates ev
            | ActionRecorded(_, _, _, t) -> duplicates t
            | CorrectiveActionRecorded(_, _, _, t)
            | ConclusionRecorded(_, _, _, t, _) -> duplicates t
            | CausalAssertionRecorded(_, _, members, _) -> (List.distinct members).Length <> members.Length
            | _ -> false
        let rec hasFeff (j: Json) =
            match j with
            | JString s -> s.IndexOf('﻿') >= 0
            | JArray xs -> xs |> List.exists hasFeff
            | JObject ms -> ms |> List.exists (fun (k, v) -> k.IndexOf('﻿') >= 0 || hasFeff v)
            | _ -> false
        if times |> List.exists (fun t -> not (isMillisecond t)) then Some "an instant has sub-millisecond precision"
        elif duplicateIds then Some "an identifier list repeats an entry"
        elif hasFeff (envelope e) then Some "text contains U+FEFF"
        else None

    // ---- decoding (schema-valid input) ----

    type private D<'T> = Result<'T, string>

    let private get (name: string) (o: Json) : D<Json> =
        match Json.field name o with
        | Some v -> Ok v
        | None -> Error $"missing field {name}"

    let private str (name: string) (o: Json) : D<string> =
        match get name o with
        | Ok(JString s) -> Ok s
        | Ok _ -> Error $"{name} is not a string"
        | Error e -> Error e

    let private ostr (name: string) (o: Json) : D<string option> =
        match get name o with
        | Ok JNull -> Ok None
        | Ok(JString s) -> Ok(Some s)
        | Ok _ -> Error $"{name} is not a string or null"
        | Error e -> Error e

    let private guid (s: string) : D<Guid> =
        match Guid.TryParseExact(s, "D") with
        | true, g when g <> Guid.Empty && g.ToString("D") = s -> Ok g
        | _ -> Error $"invalid uuid {s}"

    let private idOf (name: string) (o: Json) : D<Id<'T>> = str name o |> Result.bind guid |> Result.map Id

    let private idsOf (name: string) (o: Json) : D<Id<'T> list> =
        match get name o with
        | Ok(JArray items) ->
            items
            |> List.fold
                (fun acc item ->
                    match acc, item with
                    | Ok xs, JString s -> guid s |> Result.map (fun g -> Id g :: xs)
                    | Ok _, _ -> Error $"{name} has a non-string item"
                    | Error e, _ -> Error e)
                (Ok [])
            |> Result.map List.rev
        | Ok _ -> Error $"{name} is not an array"
        | Error e -> Error e

    let private textOf (name: string) (o: Json) : D<Text> =
        str name o |> Result.bind (fun s -> Text.create s |> Option.map Ok |> Option.defaultValue (Error $"{name} is blank"))

    let private otextOf (name: string) (o: Json) : D<Text option> =
        ostr name o
        |> Result.bind (fun s ->
            match s with
            | None -> Ok None
            | Some v -> Text.create v |> Option.map (Some >> Ok) |> Option.defaultValue (Error $"{name} is blank"))

    let private tsOf (name: string) (o: Json) : D<DateTimeOffset> =
        str name o |> Result.bind (fun s -> parseTimestamp s |> Option.map Ok |> Option.defaultValue (Error $"{name} is not a timestamp"))

    let private otsOf (name: string) (o: Json) : D<DateTimeOffset option> =
        ostr name o
        |> Result.bind (fun s ->
            match s with
            | None -> Ok None
            | Some v -> parseTimestamp v |> Option.map (Some >> Ok) |> Option.defaultValue (Error $"{name} is not a timestamp"))

    let private enumOf (parse: string -> 'T option) (name: string) (o: Json) : D<'T> =
        str name o |> Result.bind (fun s -> parse s |> Option.map Ok |> Option.defaultValue (Error $"{name} has unknown value {s}"))

    let parseActor (o: Json) : D<Actor> =
        result {
            let! actorId = str "actorId" o
            let! kind = str "kind" o
            match kind with
            | "human" -> return { ActorId = actorId; Kind = Human }
            | "system" -> return { ActorId = actorId; Kind = System }
            | "agent" ->
                let! provider = str "provider" o
                let! model = str "model" o
                return { ActorId = actorId; Kind = Agent(provider, model) }
            | other -> return! Error $"unknown actor kind {other}"
        }

    let private membersOf (o: Json) : D<CausalMember list> =
        match get "members" o with
        | Ok(JArray items) ->
            items
            |> List.fold
                (fun acc item ->
                    acc
                    |> Result.bind (fun xs ->
                        result {
                            let! h = idOf "hypothesisId" item
                            let! role = enumOf parseCauseRole "role" item
                            return { Hypothesis = h; Role = role } :: xs
                        }))
                (Ok [])
            |> Result.map List.rev
        | Ok _ -> Error "members is not an array"
        | Error e -> Error e

    let parsePayload (eventType: string) (p: Json) : D<EventBody> =
        match eventType with
        | "InvestigationStarted" ->
            result {
                let! t = textOf "title" p
                let! o = idOf "observationId" p
                let! original = textOf "original" p
                return InvestigationStarted(t, o, original)
            }
        | "LifecycleChanged" ->
            result {
                let! from = enumOf parseLifecycle "from" p
                let! target = enumOf parseLifecycle "to" p
                let! command = enumOf parseLifecycleCommand "command" p
                let! reason = otextOf "reason" p
                return LifecycleChanged(from, target, command, reason)
            }
        | "ObservationRecorded" ->
            result {
                let! o = idOf "observationId" p
                let! t = textOf "original" p
                let! at = otsOf "observedAt" p
                return ObservationRecorded(o, t, at)
            }
        | "HypothesisRecorded" ->
            result {
                let! h = idOf "hypothesisId" p
                let! s = textOf "statement" p
                let! m = otextOf "mechanism" p
                return HypothesisRecorded(h, s, m)
            }
        | "HypothesisTransitioned" ->
            result {
                let! h = idOf "hypothesisId" p
                let! from = enumOf parseHypothesisState "from" p
                let! target = enumOf parseHypothesisState "to" p
                let! command = enumOf parseHypothesisCommand "command" p
                let! reason = textOf "reason" p
                let! ev = idsOf "evidenceIds" p
                return HypothesisTransitioned(h, from, target, command, reason, ev)
            }
        | "EvidenceAdmitted" ->
            result {
                let! e = idOf "evidenceId" p
                let! kind = str "contentKind" p
                let! content =
                    match kind with
                    | "text" -> textOf "content" p |> Result.map TextContent
                    | "artifact" -> str "content" p |> Result.map ArtifactContent
                    | other -> Error $"unknown contentKind {other}"
                let! category = enumOf parseEvidenceCategory "category" p
                let! source = textOf "source" p
                let! at = otsOf "capturedAt" p
                return EvidenceAdmitted(e, content, category, source, at)
            }
        | "EvidenceSuperseded" ->
            result {
                let! o = idOf "evidenceId" p
                let! r = idOf "replacementEvidenceId" p
                let! reason = textOf "reason" p
                return EvidenceSuperseded(o, r, reason)
            }
        | "EvidenceVerificationAssessed" ->
            result {
                let! e = idOf "evidenceId" p
                let! s = enumOf parseVerificationStatus "verification" p
                return EvidenceVerificationAssessed(e, s)
            }
        | "EvidenceRelated" ->
            result {
                let! r = idOf "relationshipId" p
                let! e = idOf "evidenceId" p
                let! h = idOf "hypothesisId" p
                let! k = enumOf parseRelationshipKind "kind" p
                let! why = otextOf "rationale" p
                return EvidenceRelated(r, e, h, k, why)
            }
        | "UnknownIdentified" ->
            result {
                let! u = idOf "unknownId" p
                let! q = textOf "question" p
                let! m = enumOf parseMateriality "materiality" p
                return UnknownIdentified(u, q, m)
            }
        | "UnknownTransitioned" ->
            result {
                let! u = idOf "unknownId" p
                let! s = enumOf parseUnknownState "state" p
                let! b = textOf "basis" p
                return UnknownTransitioned(u, s, b)
            }
        | "AssumptionRecorded" ->
            result {
                let! a = idOf "assumptionId" p
                let! prop = textOf "proposition" p
                let! m = enumOf parseMateriality "materiality" p
                return AssumptionRecorded(a, prop, m)
            }
        | "AssumptionTransitioned" ->
            result {
                let! a = idOf "assumptionId" p
                let! s = enumOf parseAssumptionState "state" p
                return AssumptionTransitioned(a, s)
            }
        | "ContradictionRecorded" ->
            result {
                let! c = idOf "contradictionId" p
                let! s = textOf "statement" p
                let! ev = idsOf "evidenceIds" p
                let! m = enumOf parseMateriality "materiality" p
                return ContradictionRecorded(c, s, ev, m)
            }
        | "ContradictionTransitioned" ->
            result {
                let! c = idOf "contradictionId" p
                let! s = enumOf parseContradictionState "state" p
                let! b = textOf "basis" p
                return ContradictionTransitioned(c, s, b)
            }
        | "PredictionRecorded" ->
            result {
                let! pr = idOf "predictionId" p
                let! h = idOf "hypothesisId" p
                let! s = textOf "statement" p
                let! st = enumOf parsePredictionStrength "strength" p
                let! t = enumOf parsePredictionTiming "timing" p
                return PredictionRecorded(pr, h, s, st, t)
            }
        | "ActionRecorded" ->
            result {
                let! a = idOf "actionId" p
                let! k = enumOf parseActionKind "kind" p
                let! purpose = textOf "purpose" p
                let! t = idsOf "targetHypothesisIds" p
                return ActionRecorded(a, k, purpose, t)
            }
        | "ActionTransitioned" ->
            result {
                let! a = idOf "actionId" p
                let! s = enumOf parseActionState "state" p
                return ActionTransitioned(a, s)
            }
        | "FindingRecorded" ->
            result {
                let! f = idOf "findingId" p
                let! a = idOf "actionId" p
                let! r = textOf "originalResult" p
                let! i = otextOf "interpretation" p
                return FindingRecorded(f, a, r, i)
            }
        | "NegativeKnowledgeEstablished" ->
            result {
                let! n = idOf "negativeKnowledgeId" p
                let! s = textOf "statement" p
                let! sc = textOf "scope" p
                let! b = idsOf "basisEvidenceIds" p
                return NegativeKnowledgeEstablished(n, s, sc, b)
            }
        | "CausalAssertionRecorded" ->
            result {
                let! c = idOf "causeId" p
                let! o = textOf "outcome" p
                let! members = membersOf p
                let! m = otextOf "mechanism" p
                return CausalAssertionRecorded(c, o, members, m)
            }
        | "CausalAssertionConfirmed" ->
            result {
                let! c = idOf "causeId" p
                let! policy = str "policy" p
                return CausalAssertionConfirmed(c, policy)
            }
        | "CausalAssertionReopened" ->
            result {
                let! c = idOf "causeId" p
                let! r = textOf "reason" p
                return CausalAssertionReopened(c, r)
            }
        | "CorrectiveActionRecorded" ->
            result {
                let! c = idOf "correctiveActionId" p
                let! k = enumOf parseCorrectiveActionKind "kind" p
                let! d = textOf "description" p
                let! t = idsOf "targetCauseIds" p
                return CorrectiveActionRecorded(c, k, d, t)
            }
        | "CorrectiveActionTransitioned" ->
            result {
                let! c = idOf "correctiveActionId" p
                let! s = enumOf parseCorrectiveActionState "state" p
                return CorrectiveActionTransitioned(c, s)
            }
        | "CriterionRecorded" ->
            result {
                let! c = idOf "criterionId" p
                let! ca = idOf "correctiveActionId" p
                let! e = textOf "expectedOutcome" p
                let! m = otextOf "method" p
                return CriterionRecorded(c, ca, e, m)
            }
        | "VerificationRecorded" ->
            result {
                let! v = idOf "verificationId" p
                let! c = idOf "criterionId" p
                let! o = enumOf parseVerificationOutcome "outcome" p
                let! ev = idsOf "evidenceIds" p
                return VerificationRecorded(v, c, o, ev)
            }
        | "ConclusionRecorded" ->
            result {
                let! c = idOf "conclusionId" p
                let! s = textOf "statement" p
                let! st = enumOf parseConclusionStrength "strength" p
                let! causes = idsOf "causeIds" p
                let! r = otextOf "residualUncertainty" p
                return ConclusionRecorded(c, s, st, causes, r)
            }
        | "ProposalRecorded" ->
            result {
                let! pr = idOf "proposalId" p
                let! k = enumOf parseProposalKind "kind" p
                let! t = textOf "text" p
                let! r = otextOf "rationale" p
                return ProposalRecorded(pr, k, t, r)
            }
        | "ProposalWasAccepted" ->
            result {
                let! pr = idOf "proposalId" p
                let! target = str "targetId" p |> Result.bind guid
                return ProposalWasAccepted(pr, target)
            }
        | "ProposalWasRejected" ->
            result {
                let! pr = idOf "proposalId" p
                let! r = textOf "reason" p
                return ProposalWasRejected(pr, r)
            }
        | other -> Error $"unregistered event type {other}"

    /// Decode one schema-valid envelope into a domain event.
    let parseEvent (o: Json) : D<DomainEvent> =
        result {
            let! inv = idOf "investigationId" o
            let! version =
                match Json.field "aggregateVersion" o with
                | Some(JInt v) -> Ok v
                | _ -> Error "aggregateVersion is not an integer"
            let! eventType = str "eventType" o
            let! at = tsOf "occurredAt" o
            let! actorJson = get "actor" o
            let! actor = parseActor actorJson
            let! correlation = str "correlationId" o |> Result.bind guid
            let! payloadJson = get "payload" o
            let! body = parsePayload eventType payloadJson
            return
                { Version = version
                  Investigation = inv
                  CommandId = correlation
                  Actor = actor
                  At = at
                  Body = body }
        }

    // ---- projection (contract §8) ----

    let private sortById (items: ('K * Json) list) (key: 'K -> string) =
        items |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(key a, key b)) |> List.map snd

    let private entities (m: Map<Id<'T>, 'V>) (f: 'V -> Json) =
        JArray(sortById (m |> Map.toList |> List.map (fun (k, v) -> k, f v)) (fun (k: Id<'T>) -> k.Text))

    /// The `state` object (contract §8.2); also the shape of golden/<scenario>.state.json.
    let state (inv: Investigation) : Json =
        JObject
            [ "investigationId", id inv.Id
              "title", txt inv.Title
              "lifecycle", JString(lifecycle inv.Lifecycle)
              "version", JInt inv.Version
              "createdAt", ts inv.CreatedAt
              "observations",
              entities inv.Observations (fun o ->
                  JObject [ "id", id o.Id; "original", txt o.Original; "observedAt", ots o.ObservedAt; "provenance", provenance o.Provenance ])
              "hypotheses",
              entities inv.Hypotheses (fun h ->
                  JObject
                      [ "id", id h.Id
                        "statement", txt h.Statement
                        "mechanism", otxt h.Mechanism
                        "state", JString(hypothesisState h.State)
                        "history",
                        JArray(
                            h.History
                            |> List.map (fun c ->
                                JObject
                                    [ "from", JString(hypothesisState c.From)
                                      "to", JString(hypothesisState c.To)
                                      "command", JString(hypothesisCommand c.Command)
                                      "reason", txt c.Reason
                                      "evidenceIds", ids c.Evidence
                                      "at", ts c.At
                                      "aggregateVersion", JInt c.Version ])
                        )
                        "provenance", provenance h.Provenance ])
              "evidence",
              entities inv.Evidence (fun e ->
                  let kind, content =
                      match e.Content with
                      | TextContent t -> "text", t.Value
                      | ArtifactContent u -> "artifact", u
                  JObject
                      [ "id", id e.Id
                        "contentKind", JString kind
                        "content", JString content
                        "category", JString(evidenceCategory e.Category)
                        "source", txt e.Source
                        "capturedAt", ots e.CapturedAt
                        "verification", JString(verificationStatus e.Verification)
                        "supersededBy", Json.opt id e.SupersededBy
                        "admittedAtVersion", JInt e.AdmittedAtVersion
                        "provenance", provenance e.Provenance ])
              "relationships",
              entities inv.Relationships (fun r ->
                  JObject
                      [ "id", id r.Id
                        "evidenceId", id r.Evidence
                        "hypothesisId", id r.Hypothesis
                        "kind", JString(relationshipKind r.Kind)
                        "rationale", otxt r.Rationale
                        "provenance", provenance r.Provenance ])
              "unknowns",
              entities inv.Unknowns (fun u ->
                  JObject
                      [ "id", id u.Id
                        "question", txt u.Question
                        "materiality", JString(materiality u.Materiality)
                        "state", JString(unknownState u.State)
                        "basis", otxt u.Basis
                        "provenance", provenance u.Provenance ])
              "assumptions",
              entities inv.Assumptions (fun a ->
                  JObject
                      [ "id", id a.Id
                        "proposition", txt a.Proposition
                        "materiality", JString(materiality a.Materiality)
                        "state", JString(assumptionState a.State)
                        "provenance", provenance a.Provenance ])
              "contradictions",
              entities inv.Contradictions (fun c ->
                  JObject
                      [ "id", id c.Id
                        "statement", txt c.Statement
                        "evidenceIds", ids c.Evidence
                        "materiality", JString(materiality c.Materiality)
                        "state", JString(contradictionState c.State)
                        "basis", otxt c.Basis
                        "provenance", provenance c.Provenance ])
              "predictions",
              entities inv.Predictions (fun p ->
                  JObject
                      [ "id", id p.Id
                        "hypothesisId", id p.Hypothesis
                        "statement", txt p.Statement
                        "strength", JString(predictionStrength p.Strength)
                        "timing", JString(predictionTiming p.Timing)
                        "provenance", provenance p.Provenance ])
              "actions",
              entities inv.Actions (fun a ->
                  JObject
                      [ "id", id a.Id
                        "kind", JString(actionKind a.Kind)
                        "purpose", txt a.Purpose
                        "targetHypothesisIds", ids a.Targets
                        "state", JString(actionState a.State)
                        "provenance", provenance a.Provenance ])
              "findings",
              entities inv.Findings (fun f ->
                  JObject
                      [ "id", id f.Id
                        "actionId", id f.Action
                        "originalResult", txt f.OriginalResult
                        "interpretation", otxt f.Interpretation
                        "provenance", provenance f.Provenance ])
              "negativeKnowledge",
              entities inv.NegativeKnowledge (fun n ->
                  JObject
                      [ "id", id n.Id
                        "statement", txt n.Statement
                        "scope", txt n.Scope
                        "basisEvidenceIds", ids n.Basis
                        "provenance", provenance n.Provenance ])
              "causes",
              entities inv.Causes (fun c ->
                  JObject
                      [ "id", id c.Id
                        "outcome", txt c.Outcome
                        "members", JArray(c.Members |> List.map causalMember)
                        "mechanism", otxt c.Mechanism
                        "state", JString(causeState c.State)
                        "provenance", provenance c.Provenance ])
              "correctiveActions",
              entities inv.CorrectiveActions (fun c ->
                  JObject
                      [ "id", id c.Id
                        "kind", JString(correctiveActionKind c.Kind)
                        "description", txt c.Description
                        "targetCauseIds", ids c.Targets
                        "state", JString(correctiveActionState c.State)
                        "awaitingSinceVersion", JInt c.AwaitingSinceVersion
                        "provenance", provenance c.Provenance ])
              "criteria",
              entities inv.Criteria (fun c ->
                  JObject
                      [ "id", id c.Id
                        "correctiveActionId", id c.CorrectiveAction
                        "expectedOutcome", txt c.ExpectedOutcome
                        "method", otxt c.Method
                        "provenance", provenance c.Provenance ])
              "verifications",
              entities inv.Verifications (fun v ->
                  JObject
                      [ "id", id v.Id
                        "criterionId", id v.Criterion
                        "outcome", JString(verificationOutcome v.Outcome)
                        "evidenceIds", ids v.Evidence
                        "recordedAtVersion", JInt v.RecordedAtVersion
                        "provenance", provenance v.Provenance ])
              "conclusions",
              entities inv.Conclusions (fun c ->
                  JObject
                      [ "id", id c.Id
                        "statement", txt c.Statement
                        "strength", JString(conclusionStrength c.Strength)
                        "causeIds", ids c.Causes
                        "residualUncertainty", otxt c.ResidualUncertainty
                        "recordedAtVersion", JInt c.RecordedAtVersion
                        "provenance", provenance c.Provenance ])
              "proposals",
              entities inv.Proposals (fun p ->
                  JObject
                      [ "id", id p.Id
                        "kind", JString(proposalKind p.Kind)
                        "text", txt p.Text
                        "rationale", otxt p.Rationale
                        "state", JString(proposalState p.State)
                        "decision", otxt p.Decision
                        "provenance", provenance p.Provenance ]) ]

    /// The snapshot envelope (contract §8.1). generatedAt = occurredAt of the last event.
    let snapshot (inv: Investigation) (lastAt: DateTimeOffset) : Json =
        JObject
            [ "formatVersion", JString formatVersion
              "investigationId", id inv.Id
              "aggregateVersion", JInt inv.Version
              "projectionVersion", JInt 1L
              "semanticVersion", JString semanticVersion
              "generatedAt", ts lastAt
              "state", state inv ]

    /// The investigation manifest (contract §9.1). No provider metadata.
    let manifest (inv: Investigation) (eventCount: int64) (eventsSha256: string) (snapshotSha256: string) (createdAt: DateTimeOffset) (updatedAt: DateTimeOffset) =
        JObject
            [ "formatVersion", JString formatVersion
              "schemaVersion", JString "1.0.0"
              "semanticVersion", JString semanticVersion
              "investigationId", id inv.Id
              "aggregateVersion", JInt eventCount
              "eventCount", JInt eventCount
              "eventsSha256", JString eventsSha256
              "snapshotVersion", JInt 1L
              "snapshotSha256", JString snapshotSha256
              "domainPacks", JArray []
              "createdAt", ts createdAt
              "updatedAt", ts updatedAt ]
