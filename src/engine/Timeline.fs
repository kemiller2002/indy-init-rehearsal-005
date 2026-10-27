namespace IndyInitRootCauseInvestigator.Engine

open System

/// A reference to one domain entity (kind as in the state projection, id as canonical text).
type EntityRef = { Kind: string; Id: string }

type EventCategory =
    | LifecycleEvents
    | ObservationEvents
    | HypothesisEvents
    | EvidenceEvents
    | UnknownEvents
    | AssumptionEvents
    | ContradictionEvents
    | PredictionEvents
    | ActionEvents
    | FindingEvents
    | NegativeKnowledgeEvents
    | CauseEvents
    | CorrectionEvents
    | VerificationEvents
    | ConclusionEvents
    | ProposalEvents

/// One row of the investigation timeline. Ordered by `Version` (the canonical sequence), never by clock.
type TimelineEntry =
    { Version: int64
      EventType: string
      Category: EventCategory
      Actor: Actor
      /// When the producing command was decided (recorded, not validated: contract §5.6).
      OccurredAt: DateTimeOffset
      CommandId: Guid
      Affected: EntityRef list
      /// True when this event's clock is earlier than an event before it in sequence (UI-003-O3).
      ClockBeforePrevious: bool }

/// One semantic change between two versions, derived from the authoritative projection only.
type SemanticChange =
    | TitleChanged of before: string * after: string
    | LifecycleMoved of before: Lifecycle option * after: Lifecycle
    | EntityAdded of EntityRef
    | StateMoved of EntityRef * before: string * after: string
    | FieldChanged of EntityRef * field: string * before: Json * after: Json

/// The state at one point of history (time travel), labelled against the latest version (UI-013-O3).
type HistoricalView =
    { AtVersion: int64
      LatestVersion: int64
      /// True when this is not the latest state: the UI must label it historical, never mix it with current data.
      IsHistorical: bool
      State: Investigation }

/// History, time travel and semantic diff (T28). Pure functions over the canonical event sequence.
[<RequireQualifiedAccess>]
module Timeline =
    let private ref kind (id: Id<'T>) = { Kind = kind; Id = id.Text }

    let category (body: EventBody) =
        match body with
        | InvestigationStarted _
        | LifecycleChanged _ -> LifecycleEvents
        | ObservationRecorded _ -> ObservationEvents
        | HypothesisRecorded _
        | HypothesisTransitioned _ -> HypothesisEvents
        | EvidenceAdmitted _
        | EvidenceSuperseded _
        | EvidenceVerificationAssessed _
        | EvidenceRelated _ -> EvidenceEvents
        | UnknownIdentified _
        | UnknownTransitioned _ -> UnknownEvents
        | AssumptionRecorded _
        | AssumptionTransitioned _ -> AssumptionEvents
        | ContradictionRecorded _
        | ContradictionTransitioned _ -> ContradictionEvents
        | PredictionRecorded _ -> PredictionEvents
        | ActionRecorded _
        | ActionTransitioned _ -> ActionEvents
        | FindingRecorded _ -> FindingEvents
        | NegativeKnowledgeEstablished _ -> NegativeKnowledgeEvents
        | CausalAssertionRecorded _
        | CausalAssertionConfirmed _
        | CausalAssertionReopened _ -> CauseEvents
        | CorrectiveActionRecorded _
        | CorrectiveActionTransitioned _
        | CriterionRecorded _ -> CorrectionEvents
        | VerificationRecorded _ -> VerificationEvents
        | ConclusionRecorded _ -> ConclusionEvents
        | ProposalRecorded _
        | ProposalWasAccepted _
        | ProposalWasRejected _ -> ProposalEvents

    /// The entities an event creates or changes, and those it cites (for navigation, UI-003-O4).
    let affected (investigation: InvestigationId) (body: EventBody) : EntityRef list =
        let evs = List.map (ref "evidence")
        match body with
        | InvestigationStarted(_, o, _) -> [ ref "investigation" investigation; ref "observations" o ]
        | LifecycleChanged _ -> [ ref "investigation" investigation ]
        | ObservationRecorded(o, _, _) -> [ ref "observations" o ]
        | HypothesisRecorded(h, _, _) -> [ ref "hypotheses" h ]
        | HypothesisTransitioned(h, _, _, _, _, ev) -> ref "hypotheses" h :: evs ev
        | EvidenceAdmitted(e, _, _, _, _) -> [ ref "evidence" e ]
        | EvidenceSuperseded(o, r, _) -> [ ref "evidence" o; ref "evidence" r ]
        | EvidenceVerificationAssessed(e, _) -> [ ref "evidence" e ]
        | EvidenceRelated(r, e, h, _, _) -> [ ref "relationships" r; ref "evidence" e; ref "hypotheses" h ]
        | UnknownIdentified(u, _, _)
        | UnknownTransitioned(u, _, _) -> [ ref "unknowns" u ]
        | AssumptionRecorded(a, _, _)
        | AssumptionTransitioned(a, _) -> [ ref "assumptions" a ]
        | ContradictionRecorded(c, _, ev, _) -> ref "contradictions" c :: evs ev
        | ContradictionTransitioned(c, _, _) -> [ ref "contradictions" c ]
        | PredictionRecorded(p, h, _, _, _) -> [ ref "predictions" p; ref "hypotheses" h ]
        | ActionRecorded(a, _, _, targets) -> ref "actions" a :: (targets |> List.map (ref "hypotheses"))
        | ActionTransitioned(a, _) -> [ ref "actions" a ]
        | FindingRecorded(f, a, _, _) -> [ ref "findings" f; ref "actions" a ]
        | NegativeKnowledgeEstablished(n, _, _, ev) -> ref "negativeKnowledge" n :: evs ev
        | CausalAssertionRecorded(c, _, members, _) -> ref "causes" c :: (members |> List.map (fun m -> ref "hypotheses" m.Hypothesis))
        | CausalAssertionConfirmed(c, _)
        | CausalAssertionReopened(c, _) -> [ ref "causes" c ]
        | CorrectiveActionRecorded(c, _, _, targets) -> ref "correctiveActions" c :: (targets |> List.map (ref "causes"))
        | CorrectiveActionTransitioned(c, _) -> [ ref "correctiveActions" c ]
        | CriterionRecorded(c, ca, _, _) -> [ ref "criteria" c; ref "correctiveActions" ca ]
        | VerificationRecorded(v, c, _, ev) -> ref "verifications" v :: ref "criteria" c :: evs ev
        | ConclusionRecorded(c, _, _, causes, _) -> ref "conclusions" c :: (causes |> List.map (ref "causes"))
        | ProposalRecorded(p, _, _, _)
        | ProposalWasRejected(p, _) -> [ ref "proposals" p ]
        | ProposalWasAccepted(p, target) -> [ ref "proposals" p; { Kind = "target"; Id = target.ToString("D") } ]

    /// The timeline, in canonical sequence order (ADRP-007): never sorted by wall clock.
    let entries (events: DomainEvent list) : TimelineEntry list =
        let ordered = events |> List.sortBy (fun e -> e.Version)
        ordered
        |> List.mapFold
            (fun (latest: DateTimeOffset option) e ->
                { Version = e.Version
                  EventType = Wire.eventType e.Body
                  Category = category e.Body
                  Actor = e.Actor
                  OccurredAt = e.At
                  CommandId = e.CommandId
                  Affected = affected e.Investigation e.Body
                  ClockBeforePrevious = latest |> Option.exists (fun l -> e.At < l) },
                Some(match latest with Some l when l > e.At -> l | _ -> e.At))
            None
        |> fst

    /// The state after applying every event with version <= `version` (time travel).
    let stateAt (events: DomainEvent list) (version: int64) : Investigation option =
        events |> List.filter (fun e -> e.Version <= version) |> List.sortBy (fun e -> e.Version) |> Evolve.replay

    let view (events: DomainEvent list) (version: int64) : HistoricalView option =
        let latest = events |> List.fold (fun m e -> max m e.Version) 0L
        stateAt events version
        |> Option.map (fun s ->
            { AtVersion = s.Version
              LatestVersion = latest
              IsHistorical = s.Version < latest
              State = s })

    let private entityKinds =
        [ "observations"; "hypotheses"; "evidence"; "relationships"; "unknowns"; "assumptions"; "contradictions"; "predictions"
          "actions"; "findings"; "negativeKnowledge"; "causes"; "correctiveActions"; "criteria"; "verifications"; "conclusions"; "proposals" ]

    let private byId (state: Json option) (kind: string) : (string * Json) list =
        match state |> Option.bind (Json.field kind) with
        | Some(JArray items) ->
            items
            |> List.choose (fun item ->
                match Json.field "id" item with
                | Some(JString id) -> Some(id, item)
                | _ -> None)
        | _ -> []

    let private members (j: Json) =
        match j with
        | JObject ms -> ms
        | _ -> []

    /// The semantic changes from `before` (None = nothing yet) to `after`, from the projection only (no inference).
    /// Entities are never removed by an admissible history, so there is no removal case.
    let diff (before: Investigation option) (after: Investigation) : SemanticChange list =
        let b = before |> Option.map Wire.state
        let a = Wire.state after
        let header =
            [ match before with
              | Some s when s.Title <> after.Title -> yield TitleChanged(s.Title.Value, after.Title.Value)
              | _ -> ()
              match before with
              | Some s when s.Lifecycle = after.Lifecycle -> ()
              | _ -> yield LifecycleMoved(before |> Option.map (fun s -> s.Lifecycle), after.Lifecycle) ]
        let entities =
            entityKinds
            |> List.collect (fun kind ->
                let old = byId b kind |> Map.ofList
                byId (Some a) kind
                |> List.collect (fun (id, now) ->
                    let r = { Kind = kind; Id = id }
                    match old |> Map.tryFind id with
                    | None -> [ EntityAdded r ]
                    | Some was ->
                        members now
                        |> List.choose (fun (field, value) ->
                            let previous = Json.field field was |> Option.defaultValue JNull
                            if field = "provenance" || Json.equal previous value then None
                            else
                                match field, previous, value with
                                | "state", JString p, JString n -> Some(StateMoved(r, p, n))
                                | _ -> Some(FieldChanged(r, field, previous, value)))))
        header @ entities

    /// The semantic change made by the command group containing `version` (before/after detail, UI-003-O2).
    let changesAt (events: DomainEvent list) (version: int64) : SemanticChange list =
        match events |> List.tryFind (fun e -> e.Version = version) with
        | None -> []
        | Some e ->
            let group = events |> List.filter (fun x -> x.CommandId = e.CommandId) |> List.map (fun x -> x.Version)
            let first = List.min group
            let last = List.max group
            match stateAt events last with
            | Some after -> diff (stateAt events (first - 1L)) after
            | None -> []

    /// The semantic diff between two versions (S15).
    let between (events: DomainEvent list) (fromVersion: int64) (toVersion: int64) : SemanticChange list =
        match stateAt events toVersion with
        | Some after -> diff (stateAt events fromVersion) after
        | None -> []
