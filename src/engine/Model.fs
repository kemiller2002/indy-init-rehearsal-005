namespace IndyInitRootCauseInvestigator.Engine

open System

/// A typed identifier: a Guid tagged with the entity kind it names. Ids are chosen by the caller and travel inside
/// commands, so `decide` stays pure and deterministic (FSHARP-TYPE-SHAPES §2).
[<Struct>]
type Id<'Tag> =
    | Id of Guid

    member this.Value = let (Id g) = this in g
    /// Lowercase canonical text (contract §2.8).
    member this.Text = this.Value.ToString("D")

[<RequireQualifiedAccess>]
module Id =
    let ofGuid<'Tag> (g: Guid) : Id<'Tag> = Id g
    let text (id: Id<'Tag>) = id.Text

type InvestigationTag = interface end
type ObservationTag = interface end
type HypothesisTag = interface end
type EvidenceTag = interface end
type RelationshipTag = interface end
type UnknownTag = interface end
type AssumptionTag = interface end
type ContradictionTag = interface end
type PredictionTag = interface end
type ActionTag = interface end
type FindingTag = interface end
type NegativeKnowledgeTag = interface end
type CauseTag = interface end
type CorrectiveActionTag = interface end
type CriterionTag = interface end
type VerificationTag = interface end
type ConclusionTag = interface end
type ProposalTag = interface end

type InvestigationId = Id<InvestigationTag>
type ObservationId = Id<ObservationTag>
type HypothesisId = Id<HypothesisTag>
type EvidenceId = Id<EvidenceTag>
type RelationshipId = Id<RelationshipTag>
type UnknownId = Id<UnknownTag>
type AssumptionId = Id<AssumptionTag>
type ContradictionId = Id<ContradictionTag>
type PredictionId = Id<PredictionTag>
type ActionId = Id<ActionTag>
type FindingId = Id<FindingTag>
type NegativeKnowledgeId = Id<NegativeKnowledgeTag>
type CauseId = Id<CauseTag>
type CorrectiveActionId = Id<CorrectiveActionTag>
type CriterionId = Id<CriterionTag>
type VerificationId = Id<VerificationTag>
type ConclusionId = Id<ConclusionTag>
type ProposalId = Id<ProposalTag>

/// Text with at least one non-whitespace character, kept verbatim (D-002, contract §2.6): never trimmed or normalized.
type Text =
    private
    | Text of string

    member this.Value = let (Text s) = this in s

[<RequireQualifiedAccess>]
module Text =
    /// The engine whitespace set is exactly `Char.IsWhiteSpace` (contract §2.6), per UTF-16 code unit.
    let isBlank (s: string) = s |> Seq.forall Char.IsWhiteSpace
    let create (s: string) : Text option = if isBlank s then None else Some(Text s)
    let value (t: Text) = t.Value

type ActorKind =
    | Human
    | System
    /// An AI agent: it may only record proposals (D-004).
    | Agent of provider: string * model: string

type Actor = { ActorId: string; Kind: ActorKind }

type Provenance = { Actor: Actor; RecordedAt: DateTimeOffset }

// ---- persisted states (STATE-TRANSITION-TABLES; contract §6) ----

type Lifecycle =
    | Draft
    | Active
    | AwaitingEvidence
    | AwaitingVerification
    | Resolved
    | Closed
    | Archived

type HypothesisState =
    | Proposed
    | Plausible
    | Supported
    | Weakened
    | Falsified
    | Superseded

type UnknownState =
    | Identified
    | Investigating
    | UnknownResolved
    | AcceptedResidual
    | UnknownSuperseded

type Materiality =
    | Blocking
    | Material
    | NonMaterial
    | Informational

type AssumptionState =
    | ExplicitUnverified
    | ProvisionallyAccepted
    | AssumptionVerified
    | AssumptionRejected
    | Expired

type ContradictionState =
    | Detected
    | Triaged
    | ContradictionInvestigating
    | ContradictionResolved
    | ContradictionAcceptedResidual
    | ContradictionSuperseded

type ActionState =
    | ActionProposed
    | ActionApproved
    | ActionInProgress
    | ActionCompleted
    | ActionInterpreted
    | ActionRejected
    | ActionCancelled

type ActionKind =
    | Test
    | Inspection
    | Query
    | Comparison
    | Experiment

type CauseState =
    | CauseProposed
    | CauseConfirmed
    | CauseReopened
    | CauseSuperseded

type CauseRole =
    | RootCause
    | ContributingFactor
    | Trigger
    | EnablingCondition

type CorrectiveActionState =
    | CorrectionProposed
    | CorrectionApproved
    | CorrectionInProgress
    | Implemented
    | AwaitingVerificationOfEffect
    | VerifiedEffective
    | VerificationFailed
    | ReworkRequired
    | CorrectionCancelled

/// R7.2: mitigation is distinguishable from root-cause correction.
type CorrectiveActionKind =
    | Mitigation
    | Workaround
    | Correction
    | Prevention

type EvidenceCategory =
    | DirectObservation
    | Measurement
    | Log
    | Document
    | ExperimentResult
    | Testimony
    | ExternalReference

type VerificationStatus =
    | Unverified
    | PartiallyVerified
    | Verified
    | Invalidated

type RelationshipKind =
    | Supports
    | Contradicts
    | Discriminates
    | Insufficient

type PredictionStrength =
    | Necessary
    | Expected
    | DiscriminatingPrediction
    | NonDiscriminating

type PredictionTiming =
    | Prospective
    | Retrospective

type VerificationOutcome =
    | Satisfied
    | Failed
    | Inconclusive

type ConclusionStrength =
    | PlausibleExplanation
    | LeadingExplanation
    | SupportedCause
    | ConfirmedCause
    | AcceptedWithResidualUncertainty

type ProposalState =
    | ProposalActive
    | ProposalAccepted
    | ProposalRejected

type ProposalKind =
    | ProposedHypothesis
    | ProposedUnknown
    | ProposedAction

type EvidenceContent =
    | TextContent of Text
    /// Artifacts are referenced by absolute http(s) URL, never embedded (R3.5).
    | ArtifactContent of url: string

// ---- commands of the transition tables (no generic state setter exists) ----

type HypothesisCommand =
    | MarkPlausible
    | MarkSupported
    | MarkWeakened
    | Falsify
    | Supersede
    | Reopen

type UnknownCommand =
    | StartInvestigatingUnknown
    | ResolveUnknown
    | AcceptUnknownAsResidual

type ContradictionCommand =
    | TriageContradiction
    | ResolveContradiction
    | AcceptContradictionAsResidual

type AssumptionCommand =
    | ProvisionallyAcceptAssumption
    | VerifyAssumption
    | RejectAssumption

type ActionCommand =
    | ApproveAction
    | RejectAction
    | StartAction
    | CancelAction

type CorrectionCommand =
    | ApproveCorrection
    | StartCorrection
    | MarkImplemented
    | StartVerificationOfEffect
    | RequireRework
    | CancelCorrection

/// The command that caused a lifecycle change (kept on the event so resolve/reopen/verification failure stay distinct).
type LifecycleCommand =
    | ActivateInvestigationCommand
    | MarkAwaitingEvidenceCommand
    | MarkAwaitingVerificationCommand
    | ResolveInvestigationCommand
    | CloseInvestigationCommand
    | ReopenInvestigationCommand
    /// Follow-on of a failed verification while awaiting verification (A21).
    | VerificationFailedFollowOn

// ---- entities ----

type HypothesisChange =
    { From: HypothesisState
      To: HypothesisState
      Command: HypothesisCommand
      Reason: Text
      Evidence: EvidenceId list
      At: DateTimeOffset
      Version: int64 }

type Observation =
    { Id: ObservationId
      Original: Text
      ObservedAt: DateTimeOffset option
      Provenance: Provenance }

type Hypothesis =
    { Id: HypothesisId
      Statement: Text
      Mechanism: Text option
      State: HypothesisState
      History: HypothesisChange list
      Provenance: Provenance }

type Evidence =
    { Id: EvidenceId
      Content: EvidenceContent
      Category: EvidenceCategory
      Source: Text
      CapturedAt: DateTimeOffset option
      Verification: VerificationStatus
      SupersededBy: EvidenceId option
      AdmittedAtVersion: int64
      Provenance: Provenance }

type Relationship =
    { Id: RelationshipId
      Evidence: EvidenceId
      Hypothesis: HypothesisId
      Kind: RelationshipKind
      Rationale: Text option
      Provenance: Provenance }

type Unknown =
    { Id: UnknownId
      Question: Text
      Materiality: Materiality
      State: UnknownState
      Basis: Text option
      Provenance: Provenance }

type Assumption =
    { Id: AssumptionId
      Proposition: Text
      Materiality: Materiality
      State: AssumptionState
      Provenance: Provenance }

type Contradiction =
    { Id: ContradictionId
      Statement: Text
      Evidence: EvidenceId list
      Materiality: Materiality
      State: ContradictionState
      Basis: Text option
      Provenance: Provenance }

type Prediction =
    { Id: PredictionId
      Hypothesis: HypothesisId
      Statement: Text
      Strength: PredictionStrength
      Timing: PredictionTiming
      Provenance: Provenance }

type InvestigationAction =
    { Id: ActionId
      Kind: ActionKind
      Purpose: Text
      Targets: HypothesisId list
      State: ActionState
      Provenance: Provenance }

type Finding =
    { Id: FindingId
      Action: ActionId
      OriginalResult: Text
      Interpretation: Text option
      Provenance: Provenance }

type NegativeKnowledge =
    { Id: NegativeKnowledgeId
      Statement: Text
      Scope: Text
      Basis: EvidenceId list
      Provenance: Provenance }

type CausalMember = { Hypothesis: HypothesisId; Role: CauseRole }

type Cause =
    { Id: CauseId
      Outcome: Text
      Members: CausalMember list
      Mechanism: Text option
      State: CauseState
      Provenance: Provenance }

type CorrectiveAction =
    { Id: CorrectiveActionId
      Kind: CorrectiveActionKind
      Description: Text
      Targets: CauseId list
      State: CorrectiveActionState
      /// Version at which the current verification round began (0 = never).
      AwaitingSinceVersion: int64
      Provenance: Provenance }

type Criterion =
    { Id: CriterionId
      CorrectiveAction: CorrectiveActionId
      ExpectedOutcome: Text
      Method: Text option
      Provenance: Provenance }

type Verification =
    { Id: VerificationId
      Criterion: CriterionId
      Outcome: VerificationOutcome
      Evidence: EvidenceId list
      RecordedAtVersion: int64
      Provenance: Provenance }

type Conclusion =
    { Id: ConclusionId
      Statement: Text
      Strength: ConclusionStrength
      Causes: CauseId list
      ResidualUncertainty: Text option
      RecordedAtVersion: int64
      Provenance: Provenance }

type Proposal =
    { Id: ProposalId
      Kind: ProposalKind
      /// Never evidence or authoritative state (R15.2).
      Text: Text
      Rationale: Text option
      State: ProposalState
      Decision: Text option
      Provenance: Provenance }

/// The fold of one investigation's history. History is the authority; this is derived (contract §1.2).
type Investigation =
    { Id: InvestigationId
      Title: Text
      Lifecycle: Lifecycle
      Version: int64
      CreatedAt: DateTimeOffset
      Observations: Map<ObservationId, Observation>
      Hypotheses: Map<HypothesisId, Hypothesis>
      Evidence: Map<EvidenceId, Evidence>
      Relationships: Map<RelationshipId, Relationship>
      Unknowns: Map<UnknownId, Unknown>
      Assumptions: Map<AssumptionId, Assumption>
      Contradictions: Map<ContradictionId, Contradiction>
      Predictions: Map<PredictionId, Prediction>
      Actions: Map<ActionId, InvestigationAction>
      Findings: Map<FindingId, Finding>
      NegativeKnowledge: Map<NegativeKnowledgeId, NegativeKnowledge>
      Causes: Map<CauseId, Cause>
      CorrectiveActions: Map<CorrectiveActionId, CorrectiveAction>
      Criteria: Map<CriterionId, Criterion>
      Verifications: Map<VerificationId, Verification>
      Conclusions: Map<ConclusionId, Conclusion>
      Proposals: Map<ProposalId, Proposal> }

// ---- Boundary A: commands ----

type EvidenceInput =
    | TextInput of string
    | UrlInput of string

/// Every command the core accepts. Every id is chosen by the caller (COMMAND-EVENT-CATALOG).
type Command =
    | StartInvestigation of title: string * observation: ObservationId * original: string
    | ActivateInvestigation
    | MarkAwaitingEvidence of reason: string
    | MarkAwaitingVerification
    | ResolveInvestigation
    | CloseInvestigation
    | ReopenInvestigation of basis: string
    | RecordObservation of ObservationId * original: string * observedAt: DateTimeOffset option
    | RecordHypothesis of HypothesisId * statement: string * mechanism: string option
    | TransitionHypothesis of HypothesisId * HypothesisCommand * reason: string * evidence: EvidenceId list
    | AdmitEvidence of EvidenceId * EvidenceInput * EvidenceCategory * source: string * capturedAt: DateTimeOffset option
    | SupersedeEvidence of old: EvidenceId * replacement: EvidenceId * reason: string
    | AssessEvidence of EvidenceId * VerificationStatus
    | RelateEvidence of RelationshipId * EvidenceId * HypothesisId * RelationshipKind * rationale: string option
    | IdentifyUnknown of UnknownId * question: string * Materiality
    | TransitionUnknown of UnknownId * UnknownCommand * basis: string
    | RecordAssumption of AssumptionId * proposition: string * Materiality
    | TransitionAssumption of AssumptionId * AssumptionCommand
    | RecordContradiction of ContradictionId * statement: string * evidence: EvidenceId list * Materiality
    | TransitionContradiction of ContradictionId * ContradictionCommand * basis: string
    | RecordPrediction of PredictionId * HypothesisId * statement: string * PredictionStrength * PredictionTiming
    | RecordAction of ActionId * ActionKind * purpose: string * targets: HypothesisId list
    | TransitionAction of ActionId * ActionCommand
    | RecordFinding of FindingId * ActionId * originalResult: string * interpretation: string option
    | EstablishNegativeKnowledge of NegativeKnowledgeId * statement: string * scope: string * basis: EvidenceId list
    | RecordCause of CauseId * outcome: string * CausalMember list * mechanism: string option
    | ConfirmCause of CauseId
    | ReopenCause of CauseId * reason: string
    | RecordCorrectiveAction of CorrectiveActionId * CorrectiveActionKind * description: string * targets: CauseId list
    | TransitionCorrection of CorrectiveActionId * CorrectionCommand
    | RecordCriterion of CriterionId * CorrectiveActionId * expected: string * method: string option
    | RecordVerification of VerificationId * CriterionId * VerificationOutcome * evidence: EvidenceId list
    | RecordConclusion of ConclusionId * statement: string * ConclusionStrength * causes: CauseId list * residual: string option
    | RecordProposal of ProposalId * ProposalKind * text: string * rationale: string option
    | AcceptProposal of ProposalId * targetId: Guid
    | RejectProposal of ProposalId * reason: string

/// A command with the context it is decided in. `ExpectedVersion` is the version the caller observed (stale -> refused).
type CommandEnvelope =
    { CommandId: Guid
      Investigation: InvestigationId
      ExpectedVersion: int64 option
      Actor: Actor
      /// Millisecond-truncated UTC (contract §2.9).
      RequestedAt: DateTimeOffset
      Command: Command }

// ---- Boundary B: events (exactly the 31 registered types, contract §7) ----

type EventBody =
    | InvestigationStarted of title: Text * observation: ObservationId * original: Text
    | LifecycleChanged of from: Lifecycle * target: Lifecycle * command: LifecycleCommand * reason: Text option
    | ObservationRecorded of ObservationId * Text * DateTimeOffset option
    | HypothesisRecorded of HypothesisId * Text * Text option
    | HypothesisTransitioned of HypothesisId * from: HypothesisState * target: HypothesisState * HypothesisCommand * reason: Text * evidence: EvidenceId list
    | EvidenceAdmitted of EvidenceId * EvidenceContent * EvidenceCategory * source: Text * DateTimeOffset option
    | EvidenceSuperseded of old: EvidenceId * replacement: EvidenceId * reason: Text
    | EvidenceVerificationAssessed of EvidenceId * VerificationStatus
    | EvidenceRelated of RelationshipId * EvidenceId * HypothesisId * RelationshipKind * Text option
    | UnknownIdentified of UnknownId * Text * Materiality
    | UnknownTransitioned of UnknownId * UnknownState * Text
    | AssumptionRecorded of AssumptionId * Text * Materiality
    | AssumptionTransitioned of AssumptionId * AssumptionState
    | ContradictionRecorded of ContradictionId * Text * EvidenceId list * Materiality
    | ContradictionTransitioned of ContradictionId * ContradictionState * Text
    | PredictionRecorded of PredictionId * HypothesisId * Text * PredictionStrength * PredictionTiming
    | ActionRecorded of ActionId * ActionKind * Text * HypothesisId list
    | ActionTransitioned of ActionId * ActionState
    | FindingRecorded of FindingId * ActionId * Text * Text option
    | NegativeKnowledgeEstablished of NegativeKnowledgeId * Text * scope: Text * EvidenceId list
    | CausalAssertionRecorded of CauseId * Text * CausalMember list * Text option
    | CausalAssertionConfirmed of CauseId * policy: string
    | CausalAssertionReopened of CauseId * Text
    | CorrectiveActionRecorded of CorrectiveActionId * CorrectiveActionKind * Text * CauseId list
    | CorrectiveActionTransitioned of CorrectiveActionId * CorrectiveActionState
    | CriterionRecorded of CriterionId * CorrectiveActionId * Text * Text option
    | VerificationRecorded of VerificationId * CriterionId * VerificationOutcome * EvidenceId list
    | ConclusionRecorded of ConclusionId * Text * ConclusionStrength * CauseId list * Text option
    | ProposalRecorded of ProposalId * ProposalKind * Text * Text option
    | ProposalWasAccepted of ProposalId * targetId: Guid
    | ProposalWasRejected of ProposalId * Text

/// One persisted event. Sequence = aggregateVersion; CommandId = correlationId (contract §5).
type DomainEvent =
    { Version: int64
      Investigation: InvestigationId
      CommandId: Guid
      Actor: Actor
      At: DateTimeOffset
      Body: EventBody }

/// A typed, explainable refusal (R8.2): a stable code, the rule that produced it, and a safe message.
type DomainError =
    { Code: string
      Rule: string
      Message: string }
