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
    | AcceptProposalAction p -> Some("AcceptProposal:" + p.Text, "AcceptProposal", "Accept this proposal", None)
    | RejectProposalAction p -> Some("RejectProposal:" + p.Text, "RejectProposal", "Reject this proposal", Some "Why it is rejected")
    | ConfirmCauseAction c ->
        let outcome = inv.Causes |> Map.tryFind c |> Option.map (fun x -> text x.Outcome) |> Option.defaultValue ""
        Some("ConfirmCause:" + c.Text, "ConfirmCause", $"Confirm the cause of: {outcome}", None)
    | _ -> None

let actorText (a: Actor) =
    match a.Kind with
    | Human -> a.ActorId
    | System -> "System"
    | Agent(provider, model) -> $"AI agent ({provider}, {model})"

let categoryKey =
    function
    | LifecycleEvents -> "lifecycle"
    | ObservationEvents -> "observations"
    | HypothesisEvents -> "hypotheses"
    | EvidenceEvents -> "evidence"
    | UnknownEvents -> "unknowns"
    | AssumptionEvents -> "assumptions"
    | ContradictionEvents -> "contradictions"
    | PredictionEvents -> "predictions"
    | ActionEvents -> "actions"
    | FindingEvents -> "findings"
    | NegativeKnowledgeEvents -> "negative-knowledge"
    | CauseEvents -> "causes"
    | CorrectionEvents -> "corrective-actions"
    | VerificationEvents -> "verifications"
    | ConclusionEvents -> "conclusions"
    | ProposalEvents -> "proposals"

let categoryLabels =
    [ "lifecycle", "Status and start"; "observations", "Observations"; "hypotheses", "Hypotheses"; "evidence", "Evidence"
      "unknowns", "Open questions"; "assumptions", "Assumptions"; "contradictions", "Contradictions"; "predictions", "Predictions"
      "actions", "Actions"; "findings", "Findings"; "negative-knowledge", "Ruled out"; "causes", "Causes"
      "corrective-actions", "Corrective actions"; "verifications", "Verifications"; "conclusions", "Conclusions"; "proposals", "AI proposals" ]

let private kindWord =
    function
    | "observations" -> "observation"
    | "hypotheses" -> "hypothesis"
    | "evidence" -> "evidence"
    | "relationships" -> "evidence link"
    | "unknowns" -> "open question"
    | "assumptions" -> "assumption"
    | "contradictions" -> "contradiction"
    | "predictions" -> "prediction"
    | "actions" -> "action"
    | "findings" -> "finding"
    | "negativeKnowledge" -> "ruled-out statement"
    | "causes" -> "cause"
    | "correctiveActions" -> "corrective action"
    | "criteria" -> "criterion"
    | "verifications" -> "verification"
    | "conclusions" -> "conclusion"
    | "proposals" -> "proposal"
    | "investigation" -> "investigation"
    | other -> other

/// A record an event touched, as a link target: (section slug, id, words) from the current state's text.
let private affectedLink (inv: Investigation) (r: EntityRef) : (string * string * string) option =
    let find (m: Map<Id<'T>, 'V>) (text: 'V -> string) =
        match Guid.TryParse r.Id with
        | true, g -> m |> Map.tryFind (Id g) |> Option.map text
        | _ -> None
    let words =
        match r.Kind with
        | "hypotheses" -> find inv.Hypotheses (fun h -> h.Statement.Value)
        | "evidence" -> find inv.Evidence (fun e -> e.Source.Value)
        | "unknowns" -> find inv.Unknowns (fun u -> u.Question.Value)
        | "causes" -> find inv.Causes (fun c -> c.Outcome.Value)
        | "proposals" -> find inv.Proposals (fun p -> p.Text.Value)
        | "observations" -> find inv.Observations (fun o -> o.Original.Value)
        | _ -> None
    let section =
        match r.Kind with
        | "hypotheses" -> Some "hypotheses"
        | "evidence" | "relationships" -> Some "evidence"
        | "causes" -> Some "causal-analysis"
        | "correctiveActions" | "criteria" | "verifications" -> Some "corrective-actions"
        | "proposals" -> Some "proposals"
        | "unknowns" | "observations" | "investigation" -> Some "overview"
        | _ -> None
    match section with
    | Some sec -> Some(sec, r.Id, kindWord r.Kind + (words |> Option.map (fun w -> ": " + w) |> Option.defaultValue ""))
    | None -> None

let private shortJson (j: Json) =
    match j with
    | JString s -> s
    | JNull -> "none"
    | other -> let t = Json.canonical other in if t.Length > 60 then t.Substring(0, 57) + "…" else t

/// One semantic change in words (UI-003-O2: before and after, never invented).
let changeText (c: SemanticChange) =
    match c with
    | TitleChanged(b, a) -> $"Title: {b} → {a}"
    | LifecycleMoved(None, a) -> $"Status set to {Causa.App.Presentation.label (lifecycle a)}"
    | LifecycleMoved(Some b, a) -> $"Status: {Causa.App.Presentation.label (lifecycle b)} → {Causa.App.Presentation.label (lifecycle a)}"
    | EntityAdded r -> $"Added {kindWord r.Kind} {r.Id.Substring(0, min 8 r.Id.Length)}"
    | StateMoved(r, b, a) -> $"{kindWord r.Kind} {r.Id.Substring(0, min 8 r.Id.Length)}: {Causa.App.Presentation.label b} → {Causa.App.Presentation.label a}"
    | FieldChanged(r, field, b, a) -> $"{kindWord r.Kind} {r.Id.Substring(0, min 8 r.Id.Length)}: {field} {shortJson b} → {shortJson a}"

/// The investigation at `version` (Timeline.view), with what that version changed and what changed since.
let history (events: DomainEvent list) (version: int64) : A.HistoryView option =
    Timeline.view events version
    |> Option.map (fun v ->
        let s = v.State
        { AtVersion = v.AtVersion
          LatestVersion = v.LatestVersion
          IsHistorical = v.IsHistorical
          Title = s.Title.Value
          Lifecycle = lifecycle s.Lifecycle
          Counts =
            [ "Hypotheses", s.Hypotheses.Count; "Evidence items", s.Evidence.Count; "Open questions", s.Unknowns.Count
              "Causes", s.Causes.Count; "Proposals", s.Proposals.Count ]
          ChangesHere = Timeline.changesAt events v.AtVersion |> List.map changeText
          ChangesSince = (if v.IsHistorical then Timeline.between events v.AtVersion v.LatestVersion |> List.map changeText else []) })

let private strength =
    function
    | PlausibleExplanation -> "a plausible explanation"
    | LeadingExplanation -> "the leading explanation"
    | SupportedCause -> "a supported cause"
    | ConfirmedCause -> "a confirmed cause"
    | AcceptedWithResidualUncertainty -> "accepted with residual uncertainty"

/// Boundary E in words: the report states only what the typed records hold.
let reportView (inv: Investigation) : A.ReportView =
    let r = Projection.report inv
    let roleWord (m: CauseRole) = Causa.App.Presentation.roleLabel (role m)

    { Conclusion =
        r.CurrentConclusion
        |> Option.map (fun c ->
            let residual = c.ResidualUncertainty |> Option.map (fun u -> $" Residual uncertainty: {u.Value}") |> Option.defaultValue ""
            $"{c.Statement.Value} (recorded as {strength c.Strength}).{residual}")
      Causes =
        r.Causes
        |> List.map (fun c ->
            let members = c.Members |> List.map (fun (h, m) -> $"{roleWord m}: {h.Statement.Value} ({Causa.App.Presentation.label (hypothesisState h.State)})") |> String.concat "; "
            c.Cause.Outcome.Value, c.Confirmed, (if members = "" then "No contributing hypotheses recorded." else members))
      OpenQuestions = r.OpenUnknowns |> List.map (fun u -> u.Question.Value)
      AcceptedResiduals = r.AcceptedResiduals
      OpenContradictions = r.OpenContradictions |> List.map (fun c -> c.Statement.Value)
      UnverifiedAssumptions = r.UnverifiedAssumptions |> List.map (fun a -> a.Proposition.Value)
      Corrections =
        r.Corrections
        |> List.map (fun c ->
            let met = c.Criteria |> List.filter snd |> List.length
            c.Action.Description.Value,
            (if c.VerifiedEffective then "Verified effective" else $"Not verified effective ({met} of {c.Criteria.Length} criteria satisfied)")) }

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
        Timeline.entries history
        |> List.map (fun e ->
            ({ Sequence = e.Version
               EventType = e.EventType
               OccurredAt = stamp e.OccurredAt
               RecordedAt = None
               Actor = Some(actorText e.Actor)
               Category = categoryKey e.Category
               Affected = e.Affected |> List.choose (affectedLink inv)
               ClockBeforePrevious = e.ClockBeforePrevious }: A.TimelineRow))
      Proposals =
        values inv.Proposals
        |> List.sortBy (fun p -> p.Provenance.RecordedAt, p.Id.Text)
        |> List.map (fun p ->
            ({ Id = p.Id.Text
               Kind = (match p.Kind with ProposedHypothesis -> "Hypothesis" | ProposedUnknown -> "Open question" | ProposedAction -> "Action")
               Text = p.Text.Value
               Rationale = p.Rationale |> Option.map (fun r -> r.Value)
               State = (match p.State with ProposalActive -> "Awaiting your decision" | ProposalAccepted -> "Accepted" | ProposalRejected -> "Rejected")
               By =
                 match p.Provenance.Actor.Kind with
                 | Agent(provider, model) -> $"Proposed by an AI agent ({provider}, {model})"
                 | _ -> "Proposed by " + p.Provenance.Actor.ActorId }: A.ProposalRow))
      Findings =
        Some
            { ConfirmedCauses = p.Verification.ConfirmedCauses.Length
              VerifiedCorrections = p.Verification.VerifiedEffectiveCorrections.Length
              Resolved = p.Verification.Resolved }
      Integrity =
        p.Integrity
        |> Option.map (fun i ->
            let checkedText = if i.AdmissibilityChecked then "checked" else "not checked"
            $"events SHA-256 {i.EventsSha256.Substring(0, min 12 i.EventsSha256.Length)}…, state SHA-256 {i.StateSha256.Substring(0, min 12 i.StateSha256.Length)}…, admissibility {checkedText}")
      Report = Some(reportView inv)
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
