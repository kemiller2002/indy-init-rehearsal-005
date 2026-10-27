namespace IndyInitRootCauseInvestigator.Engine

open System

/// Creation commands, as capabilities (the ids and inputs are collected by the UI form).
type RecordKind =
    | RecordObservationKind
    | RecordHypothesisKind
    | AdmitEvidenceKind
    | RelateEvidenceKind
    | IdentifyUnknownKind
    | RecordAssumptionKind
    | RecordContradictionKind
    | RecordPredictionKind
    | RecordActionKind
    | EstablishNegativeKnowledgeKind
    | RecordCauseKind
    | RecordCorrectiveActionKind
    | RecordConclusionKind
    | RecordProposalKind

/// Every action the UI may offer, typed by its target. The UI renders exactly these (UI-004-O1).
type CapabilityAction =
    | LifecycleAction of LifecycleCommand
    | Create of RecordKind
    | HypothesisAction of HypothesisId * HypothesisCommand
    | AssessEvidenceAction of EvidenceId
    | SupersedeEvidenceAction of EvidenceId
    | UnknownAction of UnknownId * UnknownCommand
    | AssumptionAction of AssumptionId * AssumptionCommand
    | ContradictionAction of ContradictionId * ContradictionCommand
    | InvestigationActionAction of ActionId * ActionCommand
    | RecordFindingAction of ActionId
    | ConfirmCauseAction of CauseId
    | ReopenCauseAction of CauseId
    | CorrectionAction of CorrectiveActionId * CorrectionCommand
    | RecordCriterionAction of CorrectiveActionId
    | RecordVerificationAction of CriterionId
    | AcceptProposalAction of ProposalId
    | RejectProposalAction of ProposalId

/// Whether an action is available now, and if not, every reason the engine gives (UI-004-O2).
/// `Requires` names domain inputs the person must still supply (e.g. evidence that postdates a falsification);
/// they are requirements of the input, not blockers of the action.
type Capability =
    { Action: CapabilityAction
      Allowed: bool
      Blockers: DomainError list
      Requires: DomainError list }

/// An open obligation that blocks a gate (blocking/material unknowns, contradictions, assumptions, criteria).
type Obligation =
    { Code: string
      Message: string
      EntityId: string }

/// What the domain itself asserts about verification (UI-023): disjoint from technical data integrity.
type DomainVerification =
    { ConfirmedCauses: CauseId list
      VerifiedEffectiveCorrections: CorrectiveActionId list
      /// True only when the investigation is Resolved or Closed (the resolution gate passed).
      Resolved: bool }

/// Technical integrity of how the data was obtained (Administration only, UI-023-O2). Never a domain claim.
type DataIntegrity =
    { EventsSha256: string
      StateSha256: string
      AdmissibilityChecked: bool }

/// Boundary B: one immutable, versioned projection every screen consumes (UI-013-O1).
type InvestigationProjection =
    { InvestigationId: InvestigationId
      Version: int64
      Lifecycle: Lifecycle
      Title: string
      State: Investigation
      CauseGates: (CauseId * DomainError list) list
      ResolutionBlockers: DomainError list
      Obligations: Obligation list
      Capabilities: Capability list
      Verification: DomainVerification
      Integrity: DataIntegrity option }

/// Boundary E: the read-only report projection (Folio). It cannot elevate an unverified hypothesis to a cause:
/// causes carry their own domain state and only CauseConfirmed counts as confirmed.
type ReportCause =
    { Cause: Cause
      Confirmed: bool
      Members: (Hypothesis * CauseRole) list }

type ReportCorrection =
    { Action: CorrectiveAction
      Criteria: (Criterion * bool) list
      VerifiedEffective: bool }

type ReportProjection =
    { InvestigationId: InvestigationId
      Version: int64
      Title: string
      Lifecycle: Lifecycle
      CurrentConclusion: Conclusion option
      Causes: ReportCause list
      Hypotheses: Hypothesis list
      Evidence: Evidence list
      OpenUnknowns: Unknown list
      AcceptedResiduals: string list
      OpenContradictions: Contradiction list
      UnverifiedAssumptions: Assumption list
      Corrections: ReportCorrection list
      NegativeKnowledge: NegativeKnowledge list }

[<RequireQualifiedAccess>]
module Projection =
    let private values (m: Map<'K, 'V>) = m |> Map.toList |> List.map snd

    /// Codes that describe what the input must contain rather than whether the action is available.
    let inputCodes =
        set
            [ "VALIDATION.EMPTY"
              "NOT_FOUND"
              "HYPOTHESIS.EVIDENCE_REQUIRED"
              "HYPOTHESIS.REOPEN_NEEDS_NEW_EVIDENCE"
              "NEGATIVE.BASIS_REQUIRED"
              "CAUSE.NO_MEMBERS"
              "VERIFICATION.EVIDENCE_REQUIRED"
              "VERIFICATION.EVIDENCE_PREDATES_ROUND"
              "EVIDENCE.URL"
              "EVIDENCE.SELF"
              "CONCLUSION.RESIDUAL_REQUIRED"
              "CONCLUSION.UNSUPPORTED"
              "PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE" ]

    /// A fresh id no investigation uses, for probing creation commands.
    let private probeGuid = Guid "7f3c2a1e-0000-4000-8000-00000000c0de"
    let private probeText = "(probe)"

    let private probeCommand (inv: Investigation) (action: CapabilityAction) : Command =
        let anyEvidence = values inv.Evidence |> List.tryHead |> Option.map (fun e -> e.Id)
        match action with
        | LifecycleAction ActivateInvestigationCommand -> ActivateInvestigation
        | LifecycleAction MarkAwaitingEvidenceCommand -> MarkAwaitingEvidence probeText
        | LifecycleAction MarkAwaitingVerificationCommand -> MarkAwaitingVerification
        | LifecycleAction ResolveInvestigationCommand -> ResolveInvestigation
        | LifecycleAction CloseInvestigationCommand -> CloseInvestigation
        | LifecycleAction ReopenInvestigationCommand -> ReopenInvestigation probeText
        | LifecycleAction VerificationFailedFollowOn -> ActivateInvestigation
        | Create kind ->
            match kind with
            | RecordObservationKind -> RecordObservation(Id probeGuid, probeText, None)
            | RecordHypothesisKind -> RecordHypothesis(Id probeGuid, probeText, None)
            | AdmitEvidenceKind -> AdmitEvidence(Id probeGuid, TextInput probeText, DirectObservation, probeText, None)
            | RelateEvidenceKind ->
                let h = values inv.Hypotheses |> List.tryHead |> Option.map (fun h -> h.Id) |> Option.defaultValue (Id probeGuid)
                RelateEvidence(Id probeGuid, defaultArg anyEvidence (Id probeGuid), h, Supports, None)
            | IdentifyUnknownKind -> IdentifyUnknown(Id probeGuid, probeText, Material)
            | RecordAssumptionKind -> RecordAssumption(Id probeGuid, probeText, Material)
            | RecordContradictionKind -> RecordContradiction(Id probeGuid, probeText, [], Material)
            | RecordPredictionKind ->
                let h = values inv.Hypotheses |> List.tryHead |> Option.map (fun h -> h.Id) |> Option.defaultValue (Id probeGuid)
                RecordPrediction(Id probeGuid, h, probeText, Expected, Prospective)
            | RecordActionKind -> RecordAction(Id probeGuid, Test, probeText, [])
            | EstablishNegativeKnowledgeKind -> EstablishNegativeKnowledge(Id probeGuid, probeText, probeText, anyEvidence |> Option.toList)
            | RecordCauseKind ->
                let h = values inv.Hypotheses |> List.tryHead |> Option.map (fun h -> h.Id) |> Option.defaultValue (Id probeGuid)
                RecordCause(Id probeGuid, probeText, [ { Hypothesis = h; Role = RootCause } ], None)
            | RecordCorrectiveActionKind -> RecordCorrectiveAction(Id probeGuid, Correction, probeText, [])
            | RecordConclusionKind -> RecordConclusion(Id probeGuid, probeText, PlausibleExplanation, [], Some probeText)
            | RecordProposalKind -> RecordProposal(Id probeGuid, ProposedHypothesis, probeText, None)
        | HypothesisAction(h, c) ->
            TransitionHypothesis(h, c, probeText, (if Transitions.requiresEvidence c then anyEvidence |> Option.toList else []))
        | AssessEvidenceAction e -> AssessEvidence(e, Verified)
        | SupersedeEvidenceAction e ->
            let other = values inv.Evidence |> List.tryFind (fun x -> x.Id <> e) |> Option.map (fun x -> x.Id) |> Option.defaultValue (Id probeGuid)
            SupersedeEvidence(e, other, probeText)
        | UnknownAction(u, c) -> TransitionUnknown(u, c, probeText)
        | AssumptionAction(a, c) -> TransitionAssumption(a, c)
        | ContradictionAction(c, cmd) -> TransitionContradiction(c, cmd, probeText)
        | InvestigationActionAction(a, c) -> TransitionAction(a, c)
        | RecordFindingAction a -> RecordFinding(Id probeGuid, a, probeText, None)
        | ConfirmCauseAction c -> ConfirmCause c
        | ReopenCauseAction c -> ReopenCause(c, probeText)
        | CorrectionAction(c, cmd) -> TransitionCorrection(c, cmd)
        | RecordCriterionAction c -> RecordCriterion(Id probeGuid, c, probeText, None)
        | RecordVerificationAction c -> RecordVerification(Id probeGuid, c, Inconclusive, [])
        | AcceptProposalAction p -> AcceptProposal(p, probeGuid)
        | RejectProposalAction p -> RejectProposal(p, probeText)

    /// Every action that could apply to this investigation, by entity and transition table.
    let actions (inv: Investigation) : CapabilityAction list =
        [ yield!
              [ ActivateInvestigationCommand
                MarkAwaitingEvidenceCommand
                MarkAwaitingVerificationCommand
                ResolveInvestigationCommand
                CloseInvestigationCommand
                ReopenInvestigationCommand ]
              |> List.map LifecycleAction
          yield!
              [ RecordObservationKind
                RecordHypothesisKind
                AdmitEvidenceKind
                RelateEvidenceKind
                IdentifyUnknownKind
                RecordAssumptionKind
                RecordContradictionKind
                RecordPredictionKind
                RecordActionKind
                EstablishNegativeKnowledgeKind
                RecordCauseKind
                RecordCorrectiveActionKind
                RecordConclusionKind
                RecordProposalKind ]
              |> List.map Create
          for h in values inv.Hypotheses do
              for c in [ MarkPlausible; MarkSupported; MarkWeakened; Falsify; Supersede; Reopen ] do
                  yield HypothesisAction(h.Id, c)
          for e in values inv.Evidence do
              yield AssessEvidenceAction e.Id
              yield SupersedeEvidenceAction e.Id
          for u in values inv.Unknowns do
              for c in [ StartInvestigatingUnknown; ResolveUnknown; AcceptUnknownAsResidual ] do
                  yield UnknownAction(u.Id, c)
          for a in values inv.Assumptions do
              for c in [ ProvisionallyAcceptAssumption; VerifyAssumption; RejectAssumption ] do
                  yield AssumptionAction(a.Id, c)
          for c in values inv.Contradictions do
              for cmd in [ TriageContradiction; ResolveContradiction; AcceptContradictionAsResidual ] do
                  yield ContradictionAction(c.Id, cmd)
          for a in values inv.Actions do
              for c in [ ApproveAction; RejectAction; StartAction; CancelAction ] do
                  yield InvestigationActionAction(a.Id, c)
              yield RecordFindingAction a.Id
          for c in values inv.Causes do
              yield ConfirmCauseAction c.Id
              yield ReopenCauseAction c.Id
          for ca in values inv.CorrectiveActions do
              for c in [ ApproveCorrection; StartCorrection; MarkImplemented; StartVerificationOfEffect; RequireRework; CancelCorrection ] do
                  yield CorrectionAction(ca.Id, c)
              yield RecordCriterionAction ca.Id
          for cr in values inv.Criteria do
              yield RecordVerificationAction cr.Id
          for p in values inv.Proposals do
              yield AcceptProposalAction p.Id
              yield RejectProposalAction p.Id ]

    /// Capabilities for `actor` at `at`, each probed through the single decision path (no parallel policy, UI-004-O6).
    let capabilities (actor: Actor) (at: DateTimeOffset) (inv: Investigation) : Capability list =
        actions inv
        |> List.map (fun action ->
            let env =
                { CommandId = Guid.Empty
                  Investigation = inv.Id
                  ExpectedVersion = Some inv.Version
                  Actor = actor
                  RequestedAt = at
                  Command = probeCommand inv action }
            match Decide.decide (Some inv) env with
            | Ok _ -> { Action = action; Allowed = true; Blockers = []; Requires = [] }
            | Error errors ->
                let requires, blockers = errors |> List.partition (fun e -> inputCodes.Contains e.Code)
                { Action = action
                  Allowed = blockers.IsEmpty
                  Blockers = blockers
                  Requires = requires })

    let obligations (inv: Investigation) : Obligation list =
        [ for u in values inv.Unknowns do
              if Policy.isOpenUnknown u && Policy.isMaterial u.Materiality then
                  yield { Code = "OPEN_UNKNOWN"; Message = u.Question.Value; EntityId = u.Id.Text }
          for c in values inv.Contradictions do
              if Policy.isOpenContradiction c && Policy.isMaterial c.Materiality then
                  yield { Code = "OPEN_CONTRADICTION"; Message = c.Statement.Value; EntityId = c.Id.Text }
          for a in values inv.Assumptions do
              if a.State = ExplicitUnverified && Policy.isMaterial a.Materiality then
                  yield { Code = "UNVERIFIED_ASSUMPTION"; Message = a.Proposition.Value; EntityId = a.Id.Text }
          for cr in values inv.Criteria do
              let ca = inv.CorrectiveActions |> Map.tryFind cr.CorrectiveAction
              if ca |> Option.exists (fun ca -> ca.State <> CorrectionCancelled) && not (Policy.criterionSatisfied inv cr) then
                  yield { Code = "UNSATISFIED_CRITERION"; Message = cr.ExpectedOutcome.Value; EntityId = cr.Id.Text } ]

    let verification (inv: Investigation) : DomainVerification =
        { ConfirmedCauses = values inv.Causes |> List.filter (fun c -> c.State = CauseConfirmed) |> List.map (fun c -> c.Id)
          VerifiedEffectiveCorrections =
            values inv.CorrectiveActions |> List.filter (fun c -> c.State = VerifiedEffective) |> List.map (fun c -> c.Id)
          Resolved = (inv.Lifecycle = Resolved || inv.Lifecycle = Closed) }

    /// Boundary B: the projection of one investigation for one viewer.
    let ofInvestigation (actor: Actor) (at: DateTimeOffset) (integrity: DataIntegrity option) (inv: Investigation) : InvestigationProjection =
        { InvestigationId = inv.Id
          Version = inv.Version
          Lifecycle = inv.Lifecycle
          Title = inv.Title.Value
          State = inv
          CauseGates = values inv.Causes |> List.map (fun c -> c.Id, Policy.causeBlockers inv c.Id)
          ResolutionBlockers = Policy.resolutionBlockers inv
          Obligations = obligations inv
          Capabilities = capabilities actor at inv
          Verification = verification inv
          Integrity = integrity }

    let integrityOf (loaded: LoadedInvestigation) =
        { EventsSha256 = loaded.EventsSha256
          StateSha256 = loaded.StateSha256
          AdmissibilityChecked = true }

    /// Boundary E: the report projection.
    let report (inv: Investigation) : ReportProjection =
        let current = values inv.Conclusions |> List.sortBy (fun c -> c.RecordedAtVersion) |> List.tryLast
        { InvestigationId = inv.Id
          Version = inv.Version
          Title = inv.Title.Value
          Lifecycle = inv.Lifecycle
          CurrentConclusion = current
          Causes =
            values inv.Causes
            |> List.map (fun c ->
                { Cause = c
                  Confirmed = (c.State = CauseConfirmed)
                  Members = c.Members |> List.choose (fun m -> inv.Hypotheses |> Map.tryFind m.Hypothesis |> Option.map (fun h -> h, m.Role)) })
          Hypotheses = values inv.Hypotheses
          Evidence = values inv.Evidence
          OpenUnknowns = values inv.Unknowns |> List.filter Policy.isOpenUnknown
          AcceptedResiduals =
            [ for u in values inv.Unknowns do
                  if u.State = AcceptedResidual then yield u.Question.Value
              for c in values inv.Contradictions do
                  if c.State = ContradictionAcceptedResidual then yield c.Statement.Value ]
          OpenContradictions = values inv.Contradictions |> List.filter Policy.isOpenContradiction
          UnverifiedAssumptions = values inv.Assumptions |> List.filter (fun a -> a.State = ExplicitUnverified)
          Corrections =
            values inv.CorrectiveActions
            |> List.map (fun ca ->
                { Action = ca
                  Criteria =
                    values inv.Criteria
                    |> List.filter (fun c -> c.CorrectiveAction = ca.Id)
                    |> List.map (fun c -> c, Policy.criterionSatisfied inv c)
                  VerifiedEffective = (ca.State = VerifiedEffective) })
          NegativeKnowledge = values inv.NegativeKnowledge }
