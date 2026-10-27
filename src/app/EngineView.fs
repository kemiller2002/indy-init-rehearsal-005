/// Boundary B and E consumer: the core kernel's InvestigationProjection (and session history) mapped to what the
/// shell renders. Pure; decides nothing. Capabilities, blockers and verification are exactly what the engine
/// projected (UI-004-O1/O6); words come from the presentation-language mapping (UI-024).
module Causa.App.EngineView

open System
open IndyInitRootCauseInvestigator.Engine
module A = Causa.App.Adapter

let private values (m: Map<'K, 'V>) = m |> Map.toList |> List.map snd
let private stamp (t: DateTimeOffset) = t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")

/// Contract (wire) spelling of the enums the presentation mapping knows.
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

let causeState =
    function
    | CauseProposed -> "identified"
    | CauseConfirmed -> "confirmed"
    | CauseReopened -> "reopened"
    | CauseSuperseded -> "superseded"

let category =
    function
    | DirectObservation -> "directObservation"
    | Measurement -> "measurement"
    | Log -> "log"
    | Document -> "document"
    | ExperimentResult -> "experimentResult"
    | Testimony -> "testimony"
    | ExternalReference -> "externalReference"

let role =
    function
    | CauseRole.RootCause -> A.RootCause
    | CauseRole.Trigger -> A.Trigger
    | CauseRole.ContributingFactor -> A.ContributingFactor
    | CauseRole.EnablingCondition -> A.EnablingCondition

let private lifecycleCommandName =
    function
    | ActivateInvestigationCommand -> "ActivateInvestigation", "Activate the investigation", None
    | MarkAwaitingEvidenceCommand -> "MarkAwaitingEvidence", "Mark as awaiting evidence", Some "What evidence is awaited"
    | MarkAwaitingVerificationCommand -> "MarkAwaitingVerification", "Mark as awaiting verification", None
    | ResolveInvestigationCommand -> "ResolveInvestigation", "Resolve the investigation", None
    | CloseInvestigationCommand -> "CloseInvestigation", "Close the investigation", None
    | ReopenInvestigationCommand -> "ReopenInvestigation", "Reopen the investigation", Some "Why it is reopened"
    | VerificationFailedFollowOn -> "VerificationFailedFollowOn", "Follow on a failed verification", None

/// The capabilities the shell can submit today, with their names, labels and the one input each needs. Others
/// (records that need several fields) are not offered until their forms exist, rather than offered and broken.
let describe (inv: Investigation) (action: CapabilityAction) : (string * string * string * string option) option =
    let text (t: Text) = t.Value

    match action with
    | LifecycleAction c ->
        let name, label, input = lifecycleCommandName c
        Some(name, name, label, input)
    | Create RecordObservationKind -> Some("RecordObservation", "RecordObservation", "Record an observation", Some "Observation")
    | Create RecordHypothesisKind -> Some("RecordHypothesis", "RecordHypothesis", "Record a hypothesis", Some "Hypothesis statement")
    | Create IdentifyUnknownKind -> Some("IdentifyUnknown", "IdentifyUnknown", "Record an open question", Some "Question")
    | ConfirmCauseAction c ->
        let outcome = inv.Causes |> Map.tryFind c |> Option.map (fun x -> text x.Outcome) |> Option.defaultValue ""
        Some("ConfirmCause:" + c.Text, "ConfirmCause", $"Confirm the cause of: {outcome}", None)
    | _ -> None

/// Boundary B -> the shell's investigation view.
let ofProjection (writable: bool) (history: DomainEvent list) (p: InvestigationProjection) : A.InvestigationProjection =
    let inv = p.State
    let rels = values inv.Relationships

    let count (h: HypothesisId) kind = A.Known(rels |> List.filter (fun r -> r.Hypothesis = h && r.Kind = kind) |> List.length)

    let firstObservation =
        values inv.Observations |> List.sortBy (fun o -> o.Provenance.RecordedAt) |> List.tryHead |> Option.map (fun o -> o.Original.Value)

    let obligationFor (errors: DomainError list) =
        errors |> List.tryPick (fun e -> p.Obligations |> List.tryFind (fun o -> o.Code = e.Code) |> Option.map (fun o -> o.EntityId))

    let capabilities =
        p.Capabilities
        |> List.choose (fun c ->
            describe inv c.Action
            |> Option.map (fun (id, name, label, input) ->
                ({ Id = id
                   Name = name
                   Input = input
                   Label = label
                   Legal = c.Allowed
                   Reason = (if c.Allowed then None else Some(c.Blockers |> List.map (fun e -> e.Message) |> String.concat " "))
                   BlockerId = (if c.Allowed then None else obligationFor c.Blockers) }: A.Capability)))

    { Id = p.InvestigationId.Text
      Title = p.Title
      Version = p.Version
      Lifecycle = lifecycle p.Lifecycle
      Writable = writable
      Description = firstObservation
      Hypotheses =
        values inv.Hypotheses
        |> List.sortBy (fun h -> h.Provenance.RecordedAt, h.Id.Text)
        |> List.map (fun h ->
            ({ Id = h.Id.Text
               Statement = h.Statement.Value
               State = hypothesisState h.State
               Supporting = count h.Id Supports
               Contradicting = count h.Id Contradicts }: A.HypothesisRow))
      Evidence =
        values inv.Evidence
        |> List.sortBy (fun e -> e.AdmittedAtVersion)
        |> List.map (fun e ->
            ({ Id = e.Id.Text
               Content = (match e.Content with TextContent t -> t.Value | ArtifactContent url -> url)
               Source = Some e.Source.Value
               Category = category e.Category
               RecordedAt = Some(stamp e.Provenance.RecordedAt) }: A.EvidenceRow))
      Blockers = p.Obligations |> List.map (fun o -> ({ Id = o.EntityId; Question = o.Message; Material = true }: A.BlockerRow))
      Causes =
        values inv.Causes
        |> List.map (fun c ->
            ({ Id = c.Id.Text
               Outcome = c.Outcome.Value
               State = causeState c.State
               Members = c.Members |> List.map (fun m -> m.Hypothesis.Text, role m.Role) }: A.CauseRow))
      Timeline =
        history
        |> List.map (fun e ->
            let name = Wire.eventType e.Body

            ({ Sequence = e.Version
               EventType = name
               OccurredAt = stamp e.At
               RecordedAt = None
               Actor = Some e.Actor.ActorId }: A.TimelineRow))
      Capabilities = capabilities }

/// The card for the investigation list: counts the engine state actually holds (never unknown-as-zero).
let card (writable: bool) (p: InvestigationProjection) (history: DomainEvent list) : A.CardProjection =
    let inv = p.State

    { Id = p.InvestigationId.Text
      Title = p.Title
      Lifecycle = lifecycle p.Lifecycle
      Writable = writable
      LastActivity = history |> List.tryLast |> Option.map (fun e -> stamp e.At)
      Hypotheses = A.Known inv.Hypotheses.Count
      Evidence = A.Known inv.Evidence.Count
      Contradictions = A.Known(values inv.Contradictions |> List.filter Policy.isOpenContradiction |> List.length) }
