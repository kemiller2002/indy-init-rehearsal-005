namespace IndyInitRootCauseInvestigator.Engine

open System

/// Human-readable names of states, without reflection (safe under WASM trimming).
[<RequireQualifiedAccess>]
module Names =
    let lifecycle =
        function
        | Draft -> "Draft"
        | Active -> "Active"
        | AwaitingEvidence -> "AwaitingEvidence"
        | AwaitingVerification -> "AwaitingVerification"
        | Resolved -> "Resolved"
        | Closed -> "Closed"
        | Archived -> "Archived"

    let hypothesis =
        function
        | Proposed -> "Proposed"
        | Plausible -> "Plausible"
        | Supported -> "Supported"
        | Weakened -> "Weakened"
        | Falsified -> "Falsified"
        | Superseded -> "Superseded"

    let hypothesisCommand =
        function
        | MarkPlausible -> "MarkPlausible"
        | MarkSupported -> "MarkSupported"
        | MarkWeakened -> "MarkWeakened"
        | Falsify -> "Falsify"
        | Supersede -> "Supersede"
        | Reopen -> "Reopen"

    let materiality =
        function
        | Blocking -> "Blocking"
        | Material -> "Material"
        | NonMaterial -> "NonMaterial"
        | Informational -> "Informational"

    let cause =
        function
        | CauseProposed -> "Proposed"
        | CauseConfirmed -> "Confirmed"
        | CauseReopened -> "Reopened"
        | CauseSuperseded -> "Superseded"

    let action =
        function
        | ActionProposed -> "Proposed"
        | ActionApproved -> "Approved"
        | ActionInProgress -> "InProgress"
        | ActionCompleted -> "Completed"
        | ActionInterpreted -> "Interpreted"
        | ActionRejected -> "Rejected"
        | ActionCancelled -> "Cancelled"

    let correction =
        function
        | CorrectionProposed -> "Proposed"
        | CorrectionApproved -> "Approved"
        | CorrectionInProgress -> "InProgress"
        | Implemented -> "Implemented"
        | AwaitingVerificationOfEffect -> "AwaitingVerificationOfEffect"
        | VerifiedEffective -> "VerifiedEffective"
        | VerificationFailed -> "VerificationFailed"
        | ReworkRequired -> "ReworkRequired"
        | CorrectionCancelled -> "Cancelled"

    let proposal =
        function
        | ProposalActive -> "Active"
        | ProposalAccepted -> "Accepted"
        | ProposalRejected -> "Rejected"

    let lifecycleCommand =
        function
        | ActivateInvestigationCommand -> "ActivateInvestigation"
        | MarkAwaitingEvidenceCommand -> "MarkAwaitingEvidence"
        | MarkAwaitingVerificationCommand -> "MarkAwaitingVerification"
        | ResolveInvestigationCommand -> "ResolveInvestigation"
        | CloseInvestigationCommand -> "CloseInvestigation"
        | ReopenInvestigationCommand -> "ReopenInvestigation"
        | VerificationFailedFollowOn -> "VerificationFailed"

/// The structural transition tables (STATE-TRANSITION-TABLES). The only copies: decide, capabilities and tests read them.
[<RequireQualifiedAccess>]
module Transitions =
    let hypothesis (state: HypothesisState) (command: HypothesisCommand) : HypothesisState option =
        match state, command with
        | Proposed, MarkPlausible -> Some Plausible
        | Plausible, MarkSupported -> Some Supported
        | (Plausible | Supported), MarkWeakened -> Some Weakened
        | (Proposed | Plausible | Supported | Weakened), Falsify -> Some Falsified
        | (Proposed | Plausible | Supported | Weakened), Supersede -> Some Superseded
        | Falsified, Reopen -> Some Plausible
        | Weakened, MarkPlausible -> Some Plausible
        | Weakened, MarkSupported -> Some Supported
        | _ -> None

    /// Commands whose basis must cite evidence (R2.3, R2.5).
    let requiresEvidence =
        function
        | MarkSupported
        | MarkWeakened
        | Falsify
        | Reopen -> true
        | MarkPlausible
        | Supersede -> false

    let unknown (state: UnknownState) (command: UnknownCommand) =
        match state, command with
        | Identified, StartInvestigatingUnknown -> Some Investigating
        | (Identified | Investigating), ResolveUnknown -> Some UnknownResolved
        | (Identified | Investigating), AcceptUnknownAsResidual -> Some AcceptedResidual
        | _ -> None

    let contradiction (state: ContradictionState) (command: ContradictionCommand) =
        match state, command with
        | Detected, TriageContradiction -> Some Triaged
        | (Detected | Triaged | ContradictionInvestigating), ResolveContradiction -> Some ContradictionResolved
        | (Detected | Triaged | ContradictionInvestigating), AcceptContradictionAsResidual -> Some ContradictionAcceptedResidual
        | _ -> None

    let assumption (state: AssumptionState) (command: AssumptionCommand) =
        match state, command with
        | ExplicitUnverified, ProvisionallyAcceptAssumption -> Some ProvisionallyAccepted
        | (ExplicitUnverified | ProvisionallyAccepted), VerifyAssumption -> Some AssumptionVerified
        | (ExplicitUnverified | ProvisionallyAccepted), RejectAssumption -> Some AssumptionRejected
        | _ -> None

    let action (state: ActionState) (command: ActionCommand) =
        match state, command with
        | ActionProposed, ApproveAction -> Some ActionApproved
        | ActionProposed, RejectAction -> Some ActionRejected
        | ActionApproved, StartAction -> Some ActionInProgress
        | (ActionApproved | ActionInProgress), CancelAction -> Some ActionCancelled
        | _ -> None

    let correction (state: CorrectiveActionState) (command: CorrectionCommand) =
        match state, command with
        | CorrectionProposed, ApproveCorrection -> Some CorrectionApproved
        | (CorrectionProposed | CorrectionApproved), CancelCorrection -> Some CorrectionCancelled
        | (CorrectionApproved | ReworkRequired), StartCorrection -> Some CorrectionInProgress
        | CorrectionInProgress, MarkImplemented -> Some Implemented
        | Implemented, StartVerificationOfEffect -> Some AwaitingVerificationOfEffect
        | VerificationFailed, RequireRework -> Some ReworkRequired
        | _ -> None

    /// Lifecycle commands and their legal sources. `None` when the command is not a lifecycle command.
    let lifecycleCommand (command: Command) : LifecycleCommand option =
        match command with
        | ActivateInvestigation -> Some ActivateInvestigationCommand
        | MarkAwaitingEvidence _ -> Some MarkAwaitingEvidenceCommand
        | MarkAwaitingVerification -> Some MarkAwaitingVerificationCommand
        | ResolveInvestigation -> Some ResolveInvestigationCommand
        | CloseInvestigation -> Some CloseInvestigationCommand
        | ReopenInvestigation _ -> Some ReopenInvestigationCommand
        | _ -> None

    let lifecycle (state: Lifecycle) (command: LifecycleCommand) : Lifecycle option =
        match state, command with
        | (Draft | AwaitingEvidence | AwaitingVerification | Archived), ActivateInvestigationCommand -> Some Active
        | Active, MarkAwaitingEvidenceCommand -> Some AwaitingEvidence
        | Active, MarkAwaitingVerificationCommand -> Some AwaitingVerification
        | (Active | AwaitingVerification), ResolveInvestigationCommand -> Some Resolved
        | Resolved, CloseInvestigationCommand -> Some Closed
        | (Resolved | Closed), ReopenInvestigationCommand -> Some Active
        | _ -> None

    /// Content may change only while the investigation is open (frozen once resolved, D-005).
    let isOpen =
        function
        | Draft
        | Active
        | AwaitingEvidence
        | AwaitingVerification -> true
        | Resolved
        | Closed
        | Archived -> false

/// The default policy: the cause confirmation gate and the resolution gate (INVESTIGATION-SEMANTICS §15,
/// POLICY-ENGINE §24-§25). Every blocker is returned, not only the first, so the UI can explain them all.
[<RequireQualifiedAccess>]
module Policy =
    /// The policy identity recorded on CausalAssertionConfirmed.
    let id = "core-default/1"

    let private err code rule message = { Code = code; Rule = rule; Message = message }

    let private values (m: Map<'K, 'V>) = m |> Map.toList |> List.map snd

    let private label (t: Text) =
        let s = t.Value
        if s.Length <= 60 then s else s.Substring(0, 57) + "..."

    let isOpenUnknown (u: Unknown) =
        match u.State with
        | Identified
        | Investigating -> true
        | UnknownResolved
        | AcceptedResidual
        | UnknownSuperseded -> false

    let isOpenContradiction (c: Contradiction) =
        match c.State with
        | Detected
        | Triaged
        | ContradictionInvestigating -> true
        | ContradictionResolved
        | ContradictionAcceptedResidual
        | ContradictionSuperseded -> false

    let isMaterial =
        function
        | Blocking
        | Material -> true
        | NonMaterial
        | Informational -> false

    let isActiveEvidence (inv: Investigation) (eid: EvidenceId) =
        inv.Evidence |> Map.tryFind eid |> Option.exists (fun e -> e.SupersededBy.IsNone)

    let blockingUnknowns (inv: Investigation) =
        values inv.Unknowns |> List.filter (fun u -> isOpenUnknown u && u.Materiality = Blocking)

    let unresolvedMaterialUnknowns (inv: Investigation) =
        values inv.Unknowns |> List.filter (fun u -> isOpenUnknown u && isMaterial u.Materiality)

    /// The evidential conditions of the cause gate, independent of the cause's own state, so the resolution gate can
    /// re-check a confirmed cause.
    let causeSupport (inv: Investigation) (cause: Cause) : DomainError list =
        let noRoot =
            if cause.Members |> List.exists (fun m -> m.Role = RootCause) then []
            else [ err "CAUSE.NO_ROOT" "R6.3" "Name at least one member as the root cause (contributing factors alone are not a root cause)." ]

        let members =
            cause.Members
            |> List.collect (fun m ->
                match inv.Hypotheses |> Map.tryFind m.Hypothesis with
                | None -> [ err "CAUSE.MEMBER_MISSING" "I-19" "A cause member names a hypothesis that does not exist." ]
                | Some h ->
                    let supported =
                        if h.State = Supported then []
                        else [ err "CAUSE.MEMBER_NOT_SUPPORTED" "§15.3" $"Hypothesis \"{label h.Statement}\" is {Names.hypothesis h.State}, not Supported." ]
                    let evidence =
                        if inv.Relationships |> Map.exists (fun _ r -> r.Hypothesis = h.Id && r.Kind = Supports && isActiveEvidence inv r.Evidence) then []
                        else [ err "CAUSE.NO_SUPPORTING_EVIDENCE" "§15.3" $"No active evidence supports \"{label h.Statement}\"." ]
                    let addressed (eid: EvidenceId) =
                        inv.Contradictions
                        |> Map.exists (fun _ c ->
                            List.contains eid c.Evidence
                            && (c.State = ContradictionResolved || c.State = ContradictionAcceptedResidual))
                    let contradicting =
                        values inv.Relationships
                        |> List.filter (fun r ->
                            r.Hypothesis = h.Id && r.Kind = Contradicts && isActiveEvidence inv r.Evidence && not (addressed r.Evidence))
                        |> List.map (fun _ ->
                            err "CAUSE.CONTRADICTING_EVIDENCE" "§15.4" $"Evidence contradicts \"{label h.Statement}\"; record and resolve (or accept) the contradiction first.")
                    supported @ evidence @ contradicting)

        let contradictions =
            values inv.Contradictions
            |> List.filter (fun c -> isOpenContradiction c && isMaterial c.Materiality)
            |> List.map (fun c -> err "CAUSE.OPEN_CONTRADICTION" "§15.4" $"Contradiction \"{label c.Statement}\" is unresolved.")

        let unknowns =
            unresolvedMaterialUnknowns inv
            |> List.map (fun u -> err "CAUSE.OPEN_UNKNOWN" "§15.5" $"Unknown \"{label u.Question}\" is unresolved ({Names.materiality u.Materiality}).")

        let assumptions =
            values inv.Assumptions
            |> List.filter (fun a -> a.State = ExplicitUnverified && isMaterial a.Materiality)
            |> List.map (fun a -> err "CAUSE.UNVERIFIED_ASSUMPTION" "§15.6" $"Assumption \"{label a.Proposition}\" is unverified.")

        let memberIds = cause.Members |> List.map (fun m -> m.Hypothesis) |> Set.ofList

        let alternatives =
            values inv.Hypotheses
            |> List.filter (fun h ->
                not (memberIds.Contains h.Id)
                && (match h.State with
                    | Proposed
                    | Plausible
                    | Supported -> true
                    | Weakened
                    | Falsified
                    | Superseded -> false))
            |> List.map (fun h ->
                err "CAUSE.ALTERNATIVE_NOT_ADDRESSED" "§15.7" $"Alternative hypothesis \"{label h.Statement}\" is still {Names.hypothesis h.State}; weaken, falsify, supersede or include it.")

        let mechanism =
            match cause.Mechanism with
            | Some _ -> []
            | None -> [ err "CAUSE.NO_MECHANISM" "§15.8" "The causal mechanism is not stated." ]

        noRoot @ members @ contradictions @ unknowns @ assumptions @ alternatives @ mechanism

    /// The cause confirmation gate. Empty means ConfirmCause would pass now.
    let causeBlockers (inv: Investigation) (cid: CauseId) : DomainError list =
        match inv.Causes |> Map.tryFind cid with
        | None -> [ err "CAUSE.NOT_FOUND" "I-19" "The causal assertion does not exist." ]
        | Some cause ->
            let state =
                match cause.State with
                | CauseProposed
                | CauseReopened -> []
                | s -> [ err "CAUSE.STATE" "STATE-TRANSITION-TABLES §10" $"A cause in state {Names.cause s} cannot be confirmed." ]
            let lifecycle =
                match inv.Lifecycle with
                | Active
                | AwaitingEvidence
                | AwaitingVerification -> []
                | s -> [ err "INVESTIGATION.NOT_OPEN" "D-005" $"The investigation is {Names.lifecycle s}." ]
            state @ lifecycle @ causeSupport inv cause

    let private criteriaOf (inv: Investigation) (caId: CorrectiveActionId) =
        values inv.Criteria |> List.filter (fun c -> c.CorrectiveAction = caId)

    let roundOf (inv: Investigation) (caId: CorrectiveActionId) =
        inv.CorrectiveActions |> Map.tryFind caId |> Option.map (fun ca -> ca.AwaitingSinceVersion) |> Option.defaultValue 0L

    /// Evidence admitted after the verification round began and still active.
    let isFreshEvidence (inv: Investigation) (round: int64) (eid: EvidenceId) =
        isActiveEvidence inv eid
        && (inv.Evidence |> Map.tryFind eid |> Option.exists (fun e -> e.AdmittedAtVersion > round))

    /// THE definition of "criterion met": a Satisfied verification in the current round citing only fresh evidence.
    let criterionSatisfied (inv: Investigation) (crit: Criterion) =
        let round = roundOf inv crit.CorrectiveAction
        inv.Verifications
        |> Map.exists (fun _ v ->
            v.Criterion = crit.Id
            && v.Outcome = Satisfied
            && v.RecordedAtVersion > round
            && not v.Evidence.IsEmpty
            && v.Evidence |> List.forall (isFreshEvidence inv round))

    let allCriteriaSatisfied (inv: Investigation) (caId: CorrectiveActionId) =
        let criteria = criteriaOf inv caId
        not criteria.IsEmpty && criteria |> List.forall (criterionSatisfied inv)

    /// The resolution gate. Empty means ResolveInvestigation would pass now.
    let resolutionBlockers (inv: Investigation) : DomainError list =
        let confirmed = values inv.Causes |> List.filter (fun c -> c.State = CauseConfirmed)
        let confirmedIds = confirmed |> List.map (fun c -> c.Id) |> Set.ofList
        let noCause =
            if confirmed.IsEmpty then [ err "RESOLVE.NO_CONFIRMED_CAUSE" "R6.1" "No root cause has been confirmed." ] else []
        let corrections =
            values inv.CorrectiveActions
            |> List.filter (fun ca -> ca.Kind = Correction && ca.Targets |> List.exists confirmedIds.Contains)
        let correction =
            if corrections.IsEmpty then
                [ err "RESOLVE.NO_CORRECTION" "R7.2" "No root-cause correction targets the confirmed cause (a mitigation alone does not resolve the cause)." ]
            elif corrections |> List.exists (fun ca -> ca.State = VerifiedEffective) then []
            else [ err "RESOLVE.CORRECTION_NOT_VERIFIED" "R7.4" "No correction has verification evidence that its success criteria were satisfied." ]
        let pending =
            corrections
            |> List.filter (fun ca -> ca.State <> CorrectionCancelled)
            |> List.collect (fun ca -> criteriaOf inv ca.Id)
            |> List.filter (fun c -> not (criterionSatisfied inv c))
            |> List.map (fun c -> err "RESOLVE.CRITERION_UNSATISFIED" "R7.3" $"Verification criterion \"{label c.ExpectedOutcome}\" has no satisfied verification.")
        let blocking =
            blockingUnknowns inv |> List.map (fun u -> err "RESOLVE.BLOCKING_UNKNOWN" "R5.2" $"Blocking unknown \"{label u.Question}\" is open.")
        let noConclusion =
            if inv.Conclusions.IsEmpty then [ err "RESOLVE.NO_CONCLUSION" "R6.4" "Record a conclusion (with residual uncertainty) before resolving." ] else []
        let stillHolds =
            confirmed
            |> List.collect (fun c ->
                causeSupport inv c
                |> List.map (fun f -> err "RESOLVE.CAUSE_NO_LONGER_HOLDS" f.Rule $"The confirmed cause no longer meets the gate: {f.Message} Reopen and reassess it."))
        let latest = values inv.Conclusions |> List.sortBy (fun c -> c.RecordedAtVersion) |> List.tryLast
        let residualsAccepted =
            (inv.Unknowns |> Map.exists (fun _ u -> u.State = AcceptedResidual))
            || (inv.Contradictions |> Map.exists (fun _ c -> c.State = ContradictionAcceptedResidual))
        let stale =
            match latest with
            | None -> []
            | Some c ->
                let staleCause =
                    if c.Strength = ConfirmedCause && (c.Causes.IsEmpty || c.Causes |> List.exists (fun k -> not (confirmedIds.Contains k))) then
                        [ err "RESOLVE.CONCLUSION_STALE" "R6.1" "The current conclusion names a cause that is no longer confirmed; record a new conclusion." ]
                    else []
                let residual =
                    if residualsAccepted && c.ResidualUncertainty.IsNone then
                        [ err "RESOLVE.RESIDUAL_NOT_STATED" "R6.4" "Uncertainty was accepted as residual; the conclusion must state it." ]
                    else []
                staleCause @ residual
        noCause @ stillHolds @ correction @ pending @ blocking @ noConclusion @ stale

/// The fold. Total: events are validated before they are applied; an update to a missing id changes nothing.
[<RequireQualifiedAccess>]
module Evolve =
    let private update key f (m: Map<'K, 'V>) =
        match Map.tryFind key m with
        | Some v -> Map.add key (f v) m
        | None -> m

    let private started (e: DomainEvent) title oid original =
        let p = { Actor = e.Actor; RecordedAt = e.At }
        { Id = e.Investigation
          Title = title
          Lifecycle = Draft
          Version = e.Version
          CreatedAt = e.At
          Observations = Map.ofList [ oid, { Id = oid; Original = original; ObservedAt = None; Provenance = p } ]
          Hypotheses = Map.empty
          Evidence = Map.empty
          Relationships = Map.empty
          Unknowns = Map.empty
          Assumptions = Map.empty
          Contradictions = Map.empty
          Predictions = Map.empty
          Actions = Map.empty
          Findings = Map.empty
          NegativeKnowledge = Map.empty
          Causes = Map.empty
          CorrectiveActions = Map.empty
          Criteria = Map.empty
          Verifications = Map.empty
          Conclusions = Map.empty
          Proposals = Map.empty }

    /// Apply one event. `None` state accepts only InvestigationStarted (anything else leaves it None).
    let apply (state: Investigation option) (e: DomainEvent) : Investigation option =
        let p = { Actor = e.Actor; RecordedAt = e.At }
        match state, e.Body with
        | None, InvestigationStarted(title, oid, original) -> Some(started e title oid original)
        | None, _ -> None
        | Some inv, body ->
            let inv = { inv with Version = e.Version }
            Some(
                match body with
                | InvestigationStarted _ -> inv
                | LifecycleChanged(_, target, _, _) -> { inv with Lifecycle = target }
                | ObservationRecorded(oid, t, at) ->
                    { inv with Observations = inv.Observations |> Map.add oid { Id = oid; Original = t; ObservedAt = at; Provenance = p } }
                | HypothesisRecorded(hid, s, m) ->
                    { inv with
                        Hypotheses =
                            inv.Hypotheses
                            |> Map.add hid { Id = hid; Statement = s; Mechanism = m; State = Proposed; History = []; Provenance = p } }
                | HypothesisTransitioned(hid, from, target, command, reason, evidence) ->
                    let change =
                        { From = from; To = target; Command = command; Reason = reason; Evidence = evidence; At = e.At; Version = e.Version }
                    { inv with Hypotheses = inv.Hypotheses |> update hid (fun h -> { h with State = target; History = h.History @ [ change ] }) }
                | EvidenceAdmitted(eid, c, cat, src, at) ->
                    { inv with
                        Evidence =
                            inv.Evidence
                            |> Map.add
                                eid
                                { Id = eid
                                  Content = c
                                  Category = cat
                                  Source = src
                                  CapturedAt = at
                                  Verification = Unverified
                                  SupersededBy = None
                                  AdmittedAtVersion = e.Version
                                  Provenance = p } }
                | EvidenceSuperseded(old, replacement, _) ->
                    { inv with Evidence = inv.Evidence |> update old (fun ev -> { ev with SupersededBy = Some replacement }) }
                | EvidenceVerificationAssessed(eid, s) -> { inv with Evidence = inv.Evidence |> update eid (fun ev -> { ev with Verification = s }) }
                | EvidenceRelated(rid, eid, hid, kind, r) ->
                    { inv with
                        Relationships =
                            inv.Relationships
                            |> Map.add rid { Id = rid; Evidence = eid; Hypothesis = hid; Kind = kind; Rationale = r; Provenance = p } }
                | UnknownIdentified(uid, q, m) ->
                    { inv with
                        Unknowns =
                            inv.Unknowns
                            |> Map.add uid { Id = uid; Question = q; Materiality = m; State = Identified; Basis = None; Provenance = p } }
                | UnknownTransitioned(uid, s, b) -> { inv with Unknowns = inv.Unknowns |> update uid (fun u -> { u with State = s; Basis = Some b }) }
                | AssumptionRecorded(aid, prop, m) ->
                    { inv with
                        Assumptions =
                            inv.Assumptions
                            |> Map.add aid { Id = aid; Proposition = prop; Materiality = m; State = ExplicitUnverified; Provenance = p } }
                | AssumptionTransitioned(aid, s) -> { inv with Assumptions = inv.Assumptions |> update aid (fun a -> { a with State = s }) }
                | ContradictionRecorded(cid, s, ev, m) ->
                    { inv with
                        Contradictions =
                            inv.Contradictions
                            |> Map.add cid { Id = cid; Statement = s; Evidence = ev; Materiality = m; State = Detected; Basis = None; Provenance = p } }
                | ContradictionTransitioned(cid, s, b) ->
                    { inv with Contradictions = inv.Contradictions |> update cid (fun c -> { c with State = s; Basis = Some b }) }
                | PredictionRecorded(pid, hid, s, st, t) ->
                    { inv with
                        Predictions =
                            inv.Predictions
                            |> Map.add pid { Id = pid; Hypothesis = hid; Statement = s; Strength = st; Timing = t; Provenance = p } }
                | ActionRecorded(aid, k, purpose, targets) ->
                    { inv with
                        Actions =
                            inv.Actions
                            |> Map.add aid { Id = aid; Kind = k; Purpose = purpose; Targets = targets; State = ActionProposed; Provenance = p } }
                | ActionTransitioned(aid, s) -> { inv with Actions = inv.Actions |> update aid (fun a -> { a with State = s }) }
                | FindingRecorded(fid, aid, r, i) ->
                    { inv with
                        Findings = inv.Findings |> Map.add fid { Id = fid; Action = aid; OriginalResult = r; Interpretation = i; Provenance = p } }
                | NegativeKnowledgeEstablished(nid, s, sc, b) ->
                    { inv with
                        NegativeKnowledge = inv.NegativeKnowledge |> Map.add nid { Id = nid; Statement = s; Scope = sc; Basis = b; Provenance = p } }
                | CausalAssertionRecorded(cid, o, members, m) ->
                    { inv with
                        Causes =
                            inv.Causes
                            |> Map.add cid { Id = cid; Outcome = o; Members = members; Mechanism = m; State = CauseProposed; Provenance = p } }
                | CausalAssertionConfirmed(cid, _) -> { inv with Causes = inv.Causes |> update cid (fun c -> { c with State = CauseConfirmed }) }
                | CausalAssertionReopened(cid, _) -> { inv with Causes = inv.Causes |> update cid (fun c -> { c with State = CauseReopened }) }
                | CorrectiveActionRecorded(caid, k, d, t) ->
                    { inv with
                        CorrectiveActions =
                            inv.CorrectiveActions
                            |> Map.add
                                caid
                                { Id = caid
                                  Kind = k
                                  Description = d
                                  Targets = t
                                  State = CorrectionProposed
                                  AwaitingSinceVersion = 0L
                                  Provenance = p } }
                | CorrectiveActionTransitioned(caid, s) ->
                    { inv with
                        CorrectiveActions =
                            inv.CorrectiveActions
                            |> update caid (fun ca ->
                                { ca with
                                    State = s
                                    AwaitingSinceVersion = (if s = AwaitingVerificationOfEffect then e.Version else ca.AwaitingSinceVersion) }) }
                | CriterionRecorded(crid, caid, ex, m) ->
                    { inv with
                        Criteria = inv.Criteria |> Map.add crid { Id = crid; CorrectiveAction = caid; ExpectedOutcome = ex; Method = m; Provenance = p } }
                | VerificationRecorded(vid, crid, o, ev) ->
                    { inv with
                        Verifications =
                            inv.Verifications
                            |> Map.add vid { Id = vid; Criterion = crid; Outcome = o; Evidence = ev; RecordedAtVersion = e.Version; Provenance = p } }
                | ConclusionRecorded(cid, s, st, causes, r) ->
                    { inv with
                        Conclusions =
                            inv.Conclusions
                            |> Map.add
                                cid
                                { Id = cid
                                  Statement = s
                                  Strength = st
                                  Causes = causes
                                  ResidualUncertainty = r
                                  RecordedAtVersion = e.Version
                                  Provenance = p } }
                | ProposalRecorded(pid, k, t, r) ->
                    { inv with
                        Proposals =
                            inv.Proposals
                            |> Map.add pid { Id = pid; Kind = k; Text = t; Rationale = r; State = ProposalActive; Decision = None; Provenance = p } }
                | ProposalWasAccepted(pid, _) -> { inv with Proposals = inv.Proposals |> update pid (fun pr -> { pr with State = ProposalAccepted }) }
                | ProposalWasRejected(pid, r) ->
                    { inv with Proposals = inv.Proposals |> update pid (fun pr -> { pr with State = ProposalRejected; Decision = Some r }) }
            )

    /// Rebuild state from history (deterministic).
    let replay (events: DomainEvent seq) : Investigation option = events |> Seq.fold apply None

/// The single decision path (Boundary A): schema, version, authority, structure, policy. Pure.
[<RequireQualifiedAccess>]
module Decide =
    type private R<'T> = Result<'T, DomainError list>

    let private fail code rule message : R<'T> =
        Error [ { Code = code; Rule = rule; Message = message } ]

    let private text field (value: string) : R<Text> =
        match Text.create value with
        | Some t -> Ok t
        | None -> fail "VALIDATION.EMPTY" "schema" $"{field} must not be empty."

    let private optText (value: string option) = value |> Option.bind Text.create

    let private found field (m: Map<'K, 'V>) key : R<'V> =
        match Map.tryFind key m with
        | Some v -> Ok v
        | None -> fail "NOT_FOUND" "schema" $"{field} does not exist."

    let private absent field (m: Map<'K, 'V>) key : R<unit> =
        if Map.containsKey key m then fail "DUPLICATE_ID" "schema" $"{field} id already exists." else Ok()

    let private allExist field (m: Map<'K, 'V>) keys : R<unit> =
        if keys |> List.forall (fun k -> Map.containsKey k m) then Ok()
        else fail "NOT_FOUND" "schema" $"A referenced {field} does not exist."

    let private structural name (target: 'S option) (from: string) : R<'S> =
        match target with
        | Some s -> Ok s
        | None -> fail "TRANSITION.ILLEGAL" "STATE-TRANSITION-TABLES" $"{name} is not legal from state {from}."

    /// Both results, accumulating every error.
    let private both (a: R<'A>) (b: R<'B>) : R<'A * 'B> =
        match a, b with
        | Ok x, Ok y -> Ok(x, y)
        | Error e1, Error e2 -> Error(e1 @ e2)
        | Error e, _
        | _, Error e -> Error e

    let private normalizedForRepetition (s: string) = s.Trim().ToLowerInvariant()

    /// R5.4: an agent may not re-propose an explanation already falsified or established as negative knowledge.
    let private repeatsNegative (inv: Investigation) (statement: string) =
        let s = normalizedForRepetition statement
        (inv.Hypotheses |> Map.exists (fun _ h -> h.State = Falsified && normalizedForRepetition h.Statement.Value = s))
        || (inv.NegativeKnowledge |> Map.exists (fun _ n -> normalizedForRepetition n.Statement.Value = s))

    let private isAbsoluteHttpUrl (u: string) =
        (u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
         || u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        && Uri.IsWellFormedUriString(u, UriKind.Absolute)

    let private hypothesisTransition (inv: Investigation) (hid, command, reason, evidence: EvidenceId list) : R<EventBody list> =
        found "Hypothesis" inv.Hypotheses hid
        |> Result.bind (fun h ->
            let target = structural (Names.hypothesisCommand command) (Transitions.hypothesis h.State command) (Names.hypothesis h.State)
            let why = text "Reason" reason
            let cited =
                if Transitions.requiresEvidence command && evidence.IsEmpty then
                    fail "HYPOTHESIS.EVIDENCE_REQUIRED" "R2.3" $"{Names.hypothesisCommand command} must cite the evidence that caused it."
                else
                    allExist "evidence" inv.Evidence evidence
            let fresh =
                match command with
                | Reopen ->
                    let falsifiedAt =
                        h.History |> List.tryFindBack (fun c -> c.To = Falsified) |> Option.map (fun c -> c.Version) |> Option.defaultValue 0L
                    let isNew =
                        evidence
                        |> List.exists (fun e -> inv.Evidence |> Map.tryFind e |> Option.exists (fun ev -> ev.AdmittedAtVersion > falsifiedAt))
                    if isNew then Ok()
                    else fail "HYPOTHESIS.REOPEN_NEEDS_NEW_EVIDENCE" "R2.5" "Reopening needs evidence admitted after the hypothesis was falsified."
                | _ -> Ok()
            both (both target why) (both cited fresh)
            |> Result.map (fun ((target, why), _) -> [ HypothesisTransitioned(hid, h.State, target, command, why, evidence) ]))

    /// Content commands against an existing, open investigation.
    let rec private content (inv: Investigation) (env: CommandEnvelope) : R<EventBody list> =
        match env.Command with
        | RecordObservation(oid, original, observedAt) ->
            both (absent "Observation" inv.Observations oid) (text "Observation" original)
            |> Result.map (fun (_, t) -> [ ObservationRecorded(oid, t, observedAt) ])
        | RecordHypothesis(hid, statement, mechanism) ->
            both (absent "Hypothesis" inv.Hypotheses hid) (text "Hypothesis statement" statement)
            |> Result.map (fun (_, t) -> [ HypothesisRecorded(hid, t, optText mechanism) ])
        | TransitionHypothesis(hid, command, reason, evidence) -> hypothesisTransition inv (hid, command, reason, evidence)
        | AdmitEvidence(eid, input, category, source, capturedAt) ->
            let contentR =
                match input with
                | TextInput t -> text "Evidence text" t |> Result.map TextContent
                | UrlInput u ->
                    if isAbsoluteHttpUrl u then Ok(ArtifactContent u)
                    else fail "EVIDENCE.URL" "R3.5" "An artifact reference must be an absolute http(s) URL."
            both (absent "Evidence" inv.Evidence eid) (both contentR (text "Evidence source" source))
            |> Result.map (fun (_, (c, s)) -> [ EvidenceAdmitted(eid, c, category, s, capturedAt) ])
        | SupersedeEvidence(old, replacement, reason) ->
            both (found "Evidence" inv.Evidence old) (both (found "Replacement evidence" inv.Evidence replacement) (text "Reason" reason))
            |> Result.bind (fun (o, (_, why)) ->
                if o.SupersededBy.IsSome then fail "EVIDENCE.ALREADY_SUPERSEDED" "STATE-TRANSITION-TABLES §4" "Evidence is already superseded."
                elif old = replacement then fail "EVIDENCE.SELF" "schema" "Evidence cannot supersede itself."
                else Ok [ EvidenceSuperseded(old, replacement, why) ])
        | AssessEvidence(eid, status) ->
            found "Evidence" inv.Evidence eid |> Result.map (fun _ -> [ EvidenceVerificationAssessed(eid, status) ])
        | RelateEvidence(rid, eid, hid, kind, rationale) ->
            both (absent "Relationship" inv.Relationships rid) (both (found "Evidence" inv.Evidence eid) (found "Hypothesis" inv.Hypotheses hid))
            |> Result.map (fun _ -> [ EvidenceRelated(rid, eid, hid, kind, optText rationale) ])
        | IdentifyUnknown(uid, question, materiality) ->
            both (absent "Unknown" inv.Unknowns uid) (text "Question" question)
            |> Result.map (fun (_, q) -> [ UnknownIdentified(uid, q, materiality) ])
        | TransitionUnknown(uid, command, basis) ->
            found "Unknown" inv.Unknowns uid
            |> Result.bind (fun u -> both (structural "Unknown transition" (Transitions.unknown u.State command) "current") (text "Basis" basis))
            |> Result.map (fun (s, b) -> [ UnknownTransitioned(uid, s, b) ])
        | RecordAssumption(aid, proposition, materiality) ->
            both (absent "Assumption" inv.Assumptions aid) (text "Assumption" proposition)
            |> Result.map (fun (_, p) -> [ AssumptionRecorded(aid, p, materiality) ])
        | TransitionAssumption(aid, command) ->
            found "Assumption" inv.Assumptions aid
            |> Result.bind (fun a -> structural "Assumption transition" (Transitions.assumption a.State command) "current")
            |> Result.map (fun s -> [ AssumptionTransitioned(aid, s) ])
        | RecordContradiction(cid, statement, evidence, materiality) ->
            both (absent "Contradiction" inv.Contradictions cid) (both (text "Contradiction" statement) (allExist "evidence" inv.Evidence evidence))
            |> Result.map (fun (_, (s, _)) -> [ ContradictionRecorded(cid, s, evidence, materiality) ])
        | TransitionContradiction(cid, command, basis) ->
            found "Contradiction" inv.Contradictions cid
            |> Result.bind (fun c ->
                both (structural "Contradiction transition" (Transitions.contradiction c.State command) "current") (text "Resolution basis" basis))
            |> Result.map (fun (s, b) -> [ ContradictionTransitioned(cid, s, b) ])
        | RecordPrediction(pid, hid, statement, strength, timing) ->
            both (absent "Prediction" inv.Predictions pid) (both (found "Hypothesis" inv.Hypotheses hid) (text "Prediction" statement))
            |> Result.map (fun (_, (_, s)) ->
                // A11: a prediction made after a relevant finding exists is retrospective, whatever was requested.
                let findingExists =
                    inv.Findings
                    |> Map.exists (fun _ f ->
                        inv.Actions |> Map.tryFind f.Action |> Option.exists (fun a -> a.Targets.IsEmpty || a.Targets |> List.contains hid))
                [ PredictionRecorded(pid, hid, s, strength, (if findingExists then Retrospective else timing)) ])
        | RecordAction(aid, kind, purpose, targets) ->
            both (absent "Action" inv.Actions aid) (both (text "Purpose" purpose) (allExist "hypothesis" inv.Hypotheses targets))
            |> Result.map (fun (_, (p, _)) -> [ ActionRecorded(aid, kind, p, targets) ])
        | TransitionAction(aid, command) ->
            found "Action" inv.Actions aid
            |> Result.bind (fun a -> structural "Action transition" (Transitions.action a.State command) (Names.action a.State))
            |> Result.map (fun s -> [ ActionTransitioned(aid, s) ])
        | RecordFinding(fid, aid, result, interpretation) ->
            both (absent "Finding" inv.Findings fid) (both (found "Action" inv.Actions aid) (text "Result" result))
            |> Result.bind (fun (_, (a, r)) ->
                let finding = FindingRecorded(fid, aid, r, optText interpretation)
                match a.State with
                | ActionInProgress -> Ok [ finding; ActionTransitioned(aid, ActionCompleted) ]
                | ActionApproved -> Ok [ ActionTransitioned(aid, ActionInProgress); finding; ActionTransitioned(aid, ActionCompleted) ]
                | ActionCompleted -> Ok [ finding ]
                | s -> fail "ACTION.NOT_EXECUTABLE" "STATE-TRANSITION-TABLES §5" $"A finding cannot be recorded for an action that is {Names.action s}.")
        | EstablishNegativeKnowledge(nid, statement, scope, basis) ->
            both
                (absent "Negative knowledge" inv.NegativeKnowledge nid)
                (both (text "Statement" statement) (both (text "Scope" scope) (allExist "evidence" inv.Evidence basis)))
            |> Result.bind (fun (_, (s, (sc, _))) ->
                if basis.IsEmpty then fail "NEGATIVE.BASIS_REQUIRED" "R5.3" "Negative knowledge must cite the evidence that established it."
                else Ok [ NegativeKnowledgeEstablished(nid, s, sc, basis) ])
        | RecordCause(cid, outcome, members, mechanism) ->
            both
                (absent "Causal assertion" inv.Causes cid)
                (both (text "Outcome" outcome) (allExist "hypothesis" inv.Hypotheses (members |> List.map (fun m -> m.Hypothesis))))
            |> Result.bind (fun (_, (o, _)) ->
                if members.IsEmpty then fail "CAUSE.NO_MEMBERS" "R6.3" "A causal assertion needs at least one member."
                else Ok [ CausalAssertionRecorded(cid, o, members, optText mechanism) ])
        | ConfirmCause cid ->
            match Policy.causeBlockers inv cid with
            | [] -> Ok [ CausalAssertionConfirmed(cid, Policy.id) ]
            | blockers -> Error blockers
        | ReopenCause(cid, reason) ->
            both (found "Causal assertion" inv.Causes cid) (text "Reason" reason)
            |> Result.bind (fun (c, r) ->
                if c.State = CauseConfirmed then Ok [ CausalAssertionReopened(cid, r) ]
                else fail "TRANSITION.ILLEGAL" "STATE-TRANSITION-TABLES §10" $"Only a confirmed cause can be reopened (it is {Names.cause c.State}).")
        | RecordCorrectiveAction(caid, kind, description, targets) ->
            both (absent "Corrective action" inv.CorrectiveActions caid) (both (text "Description" description) (allExist "cause" inv.Causes targets))
            |> Result.map (fun (_, (d, _)) -> [ CorrectiveActionRecorded(caid, kind, d, targets) ])
        | TransitionCorrection(caid, command) ->
            found "Corrective action" inv.CorrectiveActions caid
            |> Result.bind (fun ca ->
                structural "Corrective action transition" (Transitions.correction ca.State command) (Names.correction ca.State)
                |> Result.bind (fun target ->
                    let hasCriteria = inv.Criteria |> Map.exists (fun _ c -> c.CorrectiveAction = caid)
                    if target = AwaitingVerificationOfEffect && not hasCriteria then
                        fail "CORRECTION.NO_CRITERIA" "R7.3" "Define explicit success criteria before verifying."
                    else
                        Ok [ CorrectiveActionTransitioned(caid, target) ]))
        | RecordCriterion(crid, caid, expected, method) ->
            both (absent "Criterion" inv.Criteria crid) (both (found "Corrective action" inv.CorrectiveActions caid) (text "Expected outcome" expected))
            |> Result.map (fun (_, (_, e)) -> [ CriterionRecorded(crid, caid, e, optText method) ])
        | RecordVerification(vid, crid, outcome, evidence) ->
            both (absent "Verification" inv.Verifications vid) (both (found "Criterion" inv.Criteria crid) (allExist "evidence" inv.Evidence evidence))
            |> Result.bind (fun (_, (crit, _)) ->
                let ca = inv.CorrectiveActions[crit.CorrectiveAction]
                if ca.State <> AwaitingVerificationOfEffect then
                    fail
                        "VERIFICATION.NOT_AWAITING"
                        "R7.4"
                        $"The corrective action is {Names.correction ca.State}; mark it implemented and start verification first (implementation is not verification)."
                elif outcome = Satisfied && evidence.IsEmpty then
                    fail "VERIFICATION.EVIDENCE_REQUIRED" "R7.4" "A satisfied verification must cite evidence."
                elif outcome = Satisfied && not (evidence |> List.forall (Policy.isFreshEvidence inv ca.AwaitingSinceVersion)) then
                    fail
                        "VERIFICATION.EVIDENCE_PREDATES_ROUND"
                        "R7.4"
                        "Verification evidence must be gathered after verification started and still be current (not superseded)."
                else
                    let recorded = VerificationRecorded(vid, crid, outcome, evidence)
                    let after =
                        Evolve.apply
                            (Some inv)
                            { Version = inv.Version + 1L
                              Investigation = env.Investigation
                              CommandId = env.CommandId
                              Actor = env.Actor
                              At = env.RequestedAt
                              Body = recorded }
                        |> Option.get
                    let follow =
                        match outcome with
                        | Failed ->
                            [ CorrectiveActionTransitioned(ca.Id, VerificationFailed) ]
                            @ (if inv.Lifecycle = AwaitingVerification then
                                   [ LifecycleChanged(
                                         AwaitingVerification,
                                         Active,
                                         VerificationFailedFollowOn,
                                         Text.create "A verification failed; work reopens (A21)."
                                     ) ]
                               else
                                   [])
                        | Satisfied when Policy.allCriteriaSatisfied after ca.Id -> [ CorrectiveActionTransitioned(ca.Id, VerifiedEffective) ]
                        | Satisfied
                        | Inconclusive -> []
                    Ok(recorded :: follow))
        | RecordConclusion(cid, statement, strength, causes, residual) ->
            both (absent "Conclusion" inv.Conclusions cid) (both (text "Conclusion" statement) (allExist "cause" inv.Causes causes))
            |> Result.bind (fun (_, (s, _)) ->
                let confirmed = causes |> List.exists (fun c -> inv.Causes[c].State = CauseConfirmed)
                let residualMissing = residual |> Option.forall Text.isBlank
                let residualNeeded =
                    not (Policy.unresolvedMaterialUnknowns inv).IsEmpty
                    || inv.Unknowns |> Map.exists (fun _ u -> u.State = AcceptedResidual)
                if strength = ConfirmedCause && not confirmed then
                    fail "CONCLUSION.UNSUPPORTED" "R6.1" "A conclusion cannot claim a confirmed cause before the cause is confirmed."
                elif residualMissing && residualNeeded then
                    fail "CONCLUSION.RESIDUAL_REQUIRED" "R6.4" "State the residual uncertainty: unknowns remain open or accepted as residual."
                else
                    Ok [ ConclusionRecorded(cid, s, strength, causes, optText residual) ])
        | RecordProposal(pid, kind, proposalText, rationale) ->
            both (absent "Proposal" inv.Proposals pid) (text "Proposal" proposalText)
            |> Result.bind (fun (_, t) ->
                match env.Actor.Kind with
                | Agent _ when kind = ProposedHypothesis && repeatsNegative inv proposalText ->
                    fail "PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE" "R5.4" "This explanation was already falsified or eliminated; cite new evidence and reopen instead."
                | _ -> Ok [ ProposalRecorded(pid, kind, t, optText rationale) ])
        | AcceptProposal(pid, targetId) ->
            found "Proposal" inv.Proposals pid
            |> Result.bind (fun p ->
                if p.State <> ProposalActive then
                    fail "PROPOSAL.NOT_ACTIVE" "STATE-TRANSITION-TABLES §12" $"The proposal is {Names.proposal p.State}."
                else
                    match env.Actor.Kind with
                    | Agent _ -> fail "PROPOSAL.AGENT_CANNOT_ACCEPT" "D-004" "Only a human can accept an AI proposal."
                    | Human
                    | System ->
                        let target =
                            match p.Kind with
                            | ProposedHypothesis -> RecordHypothesis(Id targetId, p.Text.Value, None)
                            | ProposedUnknown -> IdentifyUnknown(Id targetId, p.Text.Value, Material)
                            | ProposedAction -> RecordAction(Id targetId, Test, p.Text.Value, [])
                        // A refused target command does not produce ProposalWasAccepted.
                        content inv { env with Command = target }
                        |> Result.map (fun events -> events @ [ ProposalWasAccepted(pid, targetId) ]))
        | RejectProposal(pid, reason) ->
            both (found "Proposal" inv.Proposals pid) (text "Reason" reason)
            |> Result.bind (fun (p, r) ->
                if p.State <> ProposalActive then
                    fail "PROPOSAL.NOT_ACTIVE" "STATE-TRANSITION-TABLES §12" $"The proposal is {Names.proposal p.State}."
                else
                    Ok [ ProposalWasRejected(pid, r) ])
        | StartInvestigation _ -> fail "INVESTIGATION.EXISTS" "schema" "The investigation already exists."
        | ActivateInvestigation
        | MarkAwaitingEvidence _
        | MarkAwaitingVerification
        | ResolveInvestigation
        | CloseInvestigation
        | ReopenInvestigation _ -> fail "INTERNAL" "decide" "Lifecycle commands are decided by the lifecycle table."

    let private lifecycle (inv: Investigation) (env: CommandEnvelope) (command: LifecycleCommand) : R<EventBody list> =
        structural (Names.lifecycleCommand command) (Transitions.lifecycle inv.Lifecycle command) (Names.lifecycle inv.Lifecycle)
        |> Result.bind (fun target ->
            let changed reason = LifecycleChanged(inv.Lifecycle, target, command, reason)
            match env.Command with
            | MarkAwaitingEvidence reason -> text "Reason" reason |> Result.map (fun r -> [ changed (Some r) ])
            | ReopenInvestigation basis -> text "Reopening basis" basis |> Result.map (fun b -> [ changed (Some b) ])
            | MarkAwaitingVerification ->
                if inv.CorrectiveActions |> Map.exists (fun _ ca -> ca.State = Implemented || ca.State = AwaitingVerificationOfEffect) then
                    Ok [ changed None ]
                else
                    fail "LIFECYCLE.NO_VERIFICATION_WORK" "STATE-TRANSITION-TABLES §1" "No implemented corrective action awaits verification."
            | ResolveInvestigation ->
                match Policy.resolutionBlockers inv with
                | [] -> Ok [ changed None ]
                | blockers -> Error blockers
            | _ -> Ok [ changed None ])

    /// Decide a command against the current state (None = no investigation yet). Pure; refusals are values.
    let decide (state: Investigation option) (env: CommandEnvelope) : Result<EventBody list, DomainError list> =
        match env.Actor.Kind, env.Command with
        // D-004 / R15.2: an AI agent proposes; it never writes authoritative state.
        | Agent _, cmd when
            (match cmd with
             | RecordProposal _ -> false
             | _ -> true)
            ->
            fail "AGENT.PROPOSAL_ONLY" "D-004" "An AI agent may only record proposals; a human must accept them."
        | _ ->
            match state, env.Command with
            | None, StartInvestigation(title, oid, original) ->
                let titleText = if Text.isBlank title then original else title
                both (text "Title" titleText) (text "Initial observation" original)
                |> Result.map (fun (t, o) -> [ InvestigationStarted(t, oid, o) ])
            | None, _ -> fail "INVESTIGATION.NOT_FOUND" "schema" "Start the investigation first."
            | Some inv, _ when env.Investigation <> inv.Id ->
                fail "ENVELOPE.MISMATCH" "schema" "The command names a different investigation than the one loaded."
            | Some inv, _ ->
                match env.ExpectedVersion with
                | Some v when v <> inv.Version ->
                    fail "VERSION.STALE" "STATE-TRANSITION-TABLES §16.3" $"The investigation changed (expected version {v}, current {inv.Version}); reload and retry."
                | _ ->
                    match Transitions.lifecycleCommand env.Command with
                    | Some command -> lifecycle inv env command
                    | None when not (Transitions.isOpen inv.Lifecycle) ->
                        fail "INVESTIGATION.NOT_OPEN" "D-005" $"The investigation is {Names.lifecycle inv.Lifecycle}; reopen it to change its content."
                    | None -> content inv env

    /// decide + stamp: events with consecutive versions, the command id, actor and time.
    let execute (state: Investigation option) (env: CommandEnvelope) : Result<DomainEvent list, DomainError list> =
        let version = state |> Option.map (fun i -> i.Version) |> Option.defaultValue 0L
        decide state env
        |> Result.map (
            List.mapi (fun i body ->
                { Version = version + int64 i + 1L
                  Investigation = env.Investigation
                  CommandId = env.CommandId
                  Actor = env.Actor
                  At = env.RequestedAt
                  Body = body })
        )

/// Admissibility of a loaded history (Boundary C), by re-deciding every command group through the single decision
/// path with its recorded actor and time: it must reproduce exactly the recorded events. So every rule of `decide`
/// (open-only content, id uniqueness, agent authority, cause and resolution gates, verification freshness,
/// prediction timing, repetition refusal) also binds stored and remote histories (contract §11.3).
[<RequireQualifiedAccess>]
module History =
    let private v (t: Text) = t.Value
    let private ov (t: Text option) = t |> Option.map v

    /// The command a group of events records, or None when no single command emits it.
    let commandOf (bodies: EventBody list) : Command option =
        let pick f = bodies |> List.tryPick f
        let finding =
            pick (function
                | FindingRecorded(f, a, r, i) -> Some(RecordFinding(f, a, v r, ov i))
                | _ -> None)
        let verification =
            pick (function
                | VerificationRecorded(vid, c, o, ev) -> Some(RecordVerification(vid, c, o, ev))
                | _ -> None)
        let accepted =
            pick (function
                | ProposalWasAccepted(p, t) -> Some(AcceptProposal(p, t))
                | _ -> None)
        match finding, verification, accepted, bodies with
        | Some c, _, _, _
        | _, Some c, _, _
        | _, _, Some c, _ -> Some c
        | _, _, _, [] -> None
        | _, _, _, first :: _ ->
            match first with
            | InvestigationStarted(t, oid, o) -> Some(StartInvestigation(v t, oid, v o))
            | LifecycleChanged(_, _, command, reason) ->
                match command, reason with
                | ActivateInvestigationCommand, _ -> Some ActivateInvestigation
                | MarkAwaitingEvidenceCommand, Some r -> Some(MarkAwaitingEvidence(v r))
                | MarkAwaitingVerificationCommand, _ -> Some MarkAwaitingVerification
                | ResolveInvestigationCommand, _ -> Some ResolveInvestigation
                | CloseInvestigationCommand, _ -> Some CloseInvestigation
                | ReopenInvestigationCommand, Some r -> Some(ReopenInvestigation(v r))
                | _ -> None
            | ObservationRecorded(o, t, at) -> Some(RecordObservation(o, v t, at))
            | HypothesisRecorded(h, s, m) -> Some(RecordHypothesis(h, v s, ov m))
            | HypothesisTransitioned(h, _, _, command, reason, evidence) -> Some(TransitionHypothesis(h, command, v reason, evidence))
            | EvidenceAdmitted(e, c, cat, src, at) ->
                let input =
                    match c with
                    | TextContent t -> TextInput t.Value
                    | ArtifactContent u -> UrlInput u
                Some(AdmitEvidence(e, input, cat, v src, at))
            | EvidenceSuperseded(o, r, reason) -> Some(SupersedeEvidence(o, r, v reason))
            | EvidenceVerificationAssessed(e, st) -> Some(AssessEvidence(e, st))
            | EvidenceRelated(r, e, h, k, why) -> Some(RelateEvidence(r, e, h, k, ov why))
            | UnknownIdentified(u, q, m) -> Some(IdentifyUnknown(u, v q, m))
            | UnknownTransitioned(u, st, basis) ->
                match st with
                | Investigating -> Some StartInvestigatingUnknown
                | UnknownResolved -> Some ResolveUnknown
                | AcceptedResidual -> Some AcceptUnknownAsResidual
                | Identified
                | UnknownSuperseded -> None
                |> Option.map (fun c -> TransitionUnknown(u, c, v basis))
            | AssumptionRecorded(a, p, m) -> Some(RecordAssumption(a, v p, m))
            | AssumptionTransitioned(a, st) ->
                match st with
                | ProvisionallyAccepted -> Some ProvisionallyAcceptAssumption
                | AssumptionVerified -> Some VerifyAssumption
                | AssumptionRejected -> Some RejectAssumption
                | ExplicitUnverified
                | Expired -> None
                |> Option.map (fun c -> TransitionAssumption(a, c))
            | ContradictionRecorded(c, s, ev, m) -> Some(RecordContradiction(c, v s, ev, m))
            | ContradictionTransitioned(c, st, basis) ->
                match st with
                | Triaged -> Some TriageContradiction
                | ContradictionResolved -> Some ResolveContradiction
                | ContradictionAcceptedResidual -> Some AcceptContradictionAsResidual
                | Detected
                | ContradictionInvestigating
                | ContradictionSuperseded -> None
                |> Option.map (fun cmd -> TransitionContradiction(c, cmd, v basis))
            | PredictionRecorded(p, h, s, st, timing) -> Some(RecordPrediction(p, h, v s, st, timing))
            | ActionRecorded(a, k, p, t) -> Some(RecordAction(a, k, v p, t))
            | ActionTransitioned(a, st) ->
                match st with
                | ActionApproved -> Some ApproveAction
                | ActionRejected -> Some RejectAction
                | ActionInProgress -> Some StartAction
                | ActionCancelled -> Some CancelAction
                | ActionProposed
                | ActionCompleted
                | ActionInterpreted -> None
                |> Option.map (fun c -> TransitionAction(a, c))
            | NegativeKnowledgeEstablished(n, s, sc, b) -> Some(EstablishNegativeKnowledge(n, v s, v sc, b))
            | CausalAssertionRecorded(c, o, m, mech) -> Some(RecordCause(c, v o, m, ov mech))
            | CausalAssertionConfirmed(c, _) -> Some(ConfirmCause c)
            | CausalAssertionReopened(c, r) -> Some(ReopenCause(c, v r))
            | CorrectiveActionRecorded(c, k, d, t) -> Some(RecordCorrectiveAction(c, k, v d, t))
            | CorrectiveActionTransitioned(c, st) ->
                match st with
                | CorrectionApproved -> Some ApproveCorrection
                | CorrectionInProgress -> Some StartCorrection
                | Implemented -> Some MarkImplemented
                | AwaitingVerificationOfEffect -> Some StartVerificationOfEffect
                | ReworkRequired -> Some RequireRework
                | CorrectionCancelled -> Some CancelCorrection
                | CorrectionProposed
                | VerifiedEffective
                | VerificationFailed -> None
                |> Option.map (fun cmd -> TransitionCorrection(c, cmd))
            | CriterionRecorded(c, ca, e, m) -> Some(RecordCriterion(c, ca, v e, ov m))
            | ConclusionRecorded(c, s, st, causes, r) -> Some(RecordConclusion(c, v s, st, causes, ov r))
            | ProposalRecorded(p, k, t, r) -> Some(RecordProposal(p, k, v t, ov r))
            | ProposalWasRejected(p, r) -> Some(RejectProposal(p, v r))
            | FindingRecorded _
            | VerificationRecorded _
            | ProposalWasAccepted _ -> None

    /// Consecutive events of one command.
    let groups (events: DomainEvent list) : DomainEvent list list =
        events
        |> List.fold
            (fun acc e ->
                match acc with
                | (head :: _ as g) :: rest when head.CommandId = e.CommandId -> (e :: g) :: rest
                | _ -> [ e ] :: acc)
            []
        |> List.map List.rev
        |> List.rev

    let private codeFor (bodies: EventBody list) =
        match bodies with
        | CausalAssertionConfirmed _ :: _ -> "HISTORY.UNGATED_CONFIRMATION"
        | LifecycleChanged(_, Resolved, _, _) :: _ -> "HISTORY.UNGATED_RESOLUTION"
        | _ -> "HISTORY.INADMISSIBLE_EVENT"

    let private error code rule message = { Code = code; Rule = rule; Message = message }

    /// Every violation in the history, in history order; empty means it is exactly what the decision path emits.
    let validate (events: DomainEvent list) : DomainError list =
        let step (state: Investigation option, problems: DomainError list) (group: DomainEvent list) =
            let first = List.head group
            let expected = (state |> Option.map (fun i -> i.Version) |> Option.defaultValue 0L) + 1L
            let sequence =
                group
                |> List.mapi (fun i e -> e, expected + int64 i)
                |> List.filter (fun (e, want) -> e.Version <> want)
                |> List.map (fun (e, want) -> error "HISTORY.SEQUENCE" "D-005" $"Event {e.Version} found where {want} was expected.")
            let bodies = group |> List.map (fun e -> e.Body)
            let consistent =
                group |> List.forall (fun e -> e.Actor = first.Actor && e.At = first.At && e.Investigation = first.Investigation)
            let verdict =
                match state, commandOf bodies with
                | None, _ when
                    (match bodies with
                     | InvestigationStarted _ :: _ -> false
                     | _ -> true)
                    ->
                    [ error "HISTORY.NO_START" "D-005" "The history does not begin with InvestigationStarted." ]
                | _, None -> [ error "HISTORY.INADMISSIBLE_EVENT" "DEC-04" $"Events from {first.Version} are not what any single command emits." ]
                | _, Some _ when not consistent ->
                    [ error "HISTORY.INADMISSIBLE_EVENT" "DEC-04" $"Events of command {first.CommandId} disagree on actor, time or investigation." ]
                | _, Some command ->
                    let env =
                        { CommandId = first.CommandId
                          Investigation = first.Investigation
                          ExpectedVersion = None
                          Actor = first.Actor
                          RequestedAt = first.At
                          Command = command }
                    match Decide.decide state env with
                    | Error refusals ->
                        refusals
                        |> List.map (fun r -> { r with Code = codeFor bodies; Message = $"Event {first.Version} would be refused now ({r.Code}): {r.Message}" })
                    | Ok decided when decided <> bodies ->
                        [ error "HISTORY.INADMISSIBLE_EVENT" "DEC-04" $"Events from {first.Version} differ from what the command emits." ]
                    | Ok _ -> []
            let next = group |> List.fold Evolve.apply state
            next, problems @ sequence @ verdict
        events |> groups |> List.fold step (None, []) |> snd

    /// The domain code of the first violation (contract §11.3), if any.
    let firstCode (events: DomainEvent list) =
        validate events |> List.tryHead |> Option.map (fun e -> e.Code)
