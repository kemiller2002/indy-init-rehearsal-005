---
id: RQ-CAUSA-2026-0373
title: "STATE-TRANSITION-TABLES§preamble State Transition Tables"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§preamble"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:04.814Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0373

Derived from baseline requirement `STATE-TRANSITION-TABLES§preamble` (State Transition Tables). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STATE-TRANSITION-TABLES§preamble-O52 | Verified |
| STATE-TRANSITION-TABLES§preamble-O58 | \| ExplicitUnverified/ProvisionallyAccepted \| VerifyAssumption \| Verified \| |
| STATE-TRANSITION-TABLES§preamble-O60 | \| ExplicitUnverified/ProvisionallyAccepted/Verified \| ExpireAssumption \| Expired \| |
| STATE-TRANSITION-TABLES§preamble-O61 | If later evidence invalidates a previously verified assumption, use explicit reassessment/supersession rather than rewriting the old verification. |
| STATE-TRANSITION-TABLES§preamble-O128 | Accepted |
| STATE-TRANSITION-TABLES§preamble-O133 | Derived obligations may exist before Accepted. |
| STATE-TRANSITION-TABLES§preamble-O135 | \| derived candidate \| AcceptObligation \| Accepted \| |
| STATE-TRANSITION-TABLES§preamble-O136 | \| Accepted \| StartObligation \| InProgress \| |
| STATE-TRANSITION-TABLES§preamble-O137 | \| Accepted/InProgress \| ResolveObligation \| Resolved \| |
| STATE-TRANSITION-TABLES§preamble-O138 | \| Accepted/InProgress \| WaiveObligation \| Waived if policy permits \| |
| STATE-TRANSITION-TABLES§preamble-O139 | \| Accepted/InProgress \| SupersedeObligation \| Superseded \| |
| STATE-TRANSITION-TABLES§preamble-O178 | Accepted |
| STATE-TRANSITION-TABLES§preamble-O185 | \| Active/Revised/Deferred \| AcceptProposal \| Accepted only if target command succeeds \| |
| STATE-TRANSITION-TABLES§preamble-O211 | verify required semantic dependencies; |
| STATE-TRANSITION-TABLES§preamble-O225 | Table-driven tests should cover: |

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§preamble-O1 | These tables define initial legal lifecycle transitions. |
| STATE-TRANSITION-TABLES§preamble-O2 | Policy may add prerequisites or block an otherwise structurally legal transition. |
| STATE-TRANSITION-TABLES§preamble-O3 | Domain packs may strengthen rules. |
| STATE-TRANSITION-TABLES§preamble-O4 | No domain pack may create transitions that violate core invariants. |
| STATE-TRANSITION-TABLES§preamble-O5 | This document deliberately distinguishes lifecycle state from derived posture. |
| STATE-TRANSITION-TABLES§preamble-O6 | Persisted states: |
| STATE-TRANSITION-TABLES§preamble-O7 | Draft |
| STATE-TRANSITION-TABLES§preamble-O8 | Active |
| STATE-TRANSITION-TABLES§preamble-O9 | AwaitingEvidence |
| STATE-TRANSITION-TABLES§preamble-O10 | AwaitingVerification |
| STATE-TRANSITION-TABLES§preamble-O11 | Resolved |
| STATE-TRANSITION-TABLES§preamble-O12 | Closed |
| STATE-TRANSITION-TABLES§preamble-O13 | Archived |
| STATE-TRANSITION-TABLES§preamble-O14 | Reopened is an event that normally returns Resolved/Closed to Active. |
| STATE-TRANSITION-TABLES§preamble-O15 | \| From \| Command \| To \| Minimum structural condition \| |
| STATE-TRANSITION-TABLES§preamble-O16 | \| none \| StartInvestigation \| Draft \| valid identity/source/manifest \| |
| STATE-TRANSITION-TABLES§preamble-O17 | \| Draft \| ActivateInvestigation \| Active \| activation policy satisfied \| |
| STATE-TRANSITION-TABLES§preamble-O18 | \| Active \| MarkAwaitingEvidence \| AwaitingEvidence \| open evidence dependency/reason \| |
| STATE-TRANSITION-TABLES§preamble-O19 | \| AwaitingEvidence \| ActivateInvestigation \| Active \| wait cleared/work resumed \| |
| STATE-TRANSITION-TABLES§preamble-O20 | \| Active \| MarkAwaitingVerification \| AwaitingVerification \| verification work exists \| |
| STATE-TRANSITION-TABLES§preamble-O21 | \| AwaitingVerification \| ActivateInvestigation \| Active \| verification failed/new work \| |
| STATE-TRANSITION-TABLES§preamble-O22 | \| AwaitingVerification \| ResolveInvestigation \| Resolved \| resolution gate \| |
| STATE-TRANSITION-TABLES§preamble-O23 | \| Active \| ResolveInvestigation \| Resolved \| policy permits and resolution gate \| |
| STATE-TRANSITION-TABLES§preamble-O24 | \| Resolved \| CloseInvestigation \| Closed \| closure policy \| |
| STATE-TRANSITION-TABLES§preamble-O25 | \| Resolved/Closed \| ReopenInvestigation \| Active \| explicit reopening basis \| |
| STATE-TRANSITION-TABLES§preamble-O26 | \| any non-archived state \| ArchiveInvestigation \| Archived \| archive policy \| |
| STATE-TRANSITION-TABLES§preamble-O27 | \| Archived \| ActivateInvestigation \| Active \| explicit unarchive/reactivation policy \| |
| STATE-TRANSITION-TABLES§preamble-O28 | Persisted states: |
| STATE-TRANSITION-TABLES§preamble-O29 | Proposed |
| STATE-TRANSITION-TABLES§preamble-O30 | Plausible |
| STATE-TRANSITION-TABLES§preamble-O31 | Supported |
| STATE-TRANSITION-TABLES§preamble-O32 | Weakened |
| STATE-TRANSITION-TABLES§preamble-O33 | Falsified |
| STATE-TRANSITION-TABLES§preamble-O34 | Superseded |
| STATE-TRANSITION-TABLES§preamble-O35 | Leading is initially derived. |
| STATE-TRANSITION-TABLES§preamble-O36 | \| From \| Command \| To \| Minimum structural condition \| |
| STATE-TRANSITION-TABLES§preamble-O37 | \| none \| RecordHypothesis \| Proposed \| testable explanatory proposition \| |
| STATE-TRANSITION-TABLES§preamble-O38 | \| Proposed \| MarkHypothesisPlausible \| Plausible \| evaluable/relevant/no immediate falsifier \| |
| STATE-TRANSITION-TABLES§preamble-O39 | \| Plausible \| MarkHypothesisSupported \| Supported \| material support \| |
| STATE-TRANSITION-TABLES§preamble-O40 | \| Plausible/Supported \| MarkHypothesisWeakened \| Weakened \| weakening basis \| |
| STATE-TRANSITION-TABLES§preamble-O41 | \| Proposed/Plausible/Supported/Weakened \| FalsifyHypothesis \| Falsified \| scoped falsifying basis \| |
| STATE-TRANSITION-TABLES§preamble-O42 | \| Proposed/Plausible/Supported/Weakened \| SupersedeHypothesis \| Superseded \| explicit reason \| |
| STATE-TRANSITION-TABLES§preamble-O43 | \| Falsified \| ReopenHypothesis \| Plausible \| new/corrected evidence, scope, or premise \| |
| STATE-TRANSITION-TABLES§preamble-O44 | \| Weakened \| MarkHypothesisPlausible \| Plausible \| strengthening basis \| |
| STATE-TRANSITION-TABLES§preamble-O45 | \| Weakened \| MarkHypothesisSupported \| Supported \| sufficient support \| |
| STATE-TRANSITION-TABLES§preamble-O46 | Cause confirmation is not a Hypothesis lifecycle transition. |
| STATE-TRANSITION-TABLES§preamble-O47 | A supported hypothesis may participate in a separate CausalAssertion, which owns Confirmed/Reopened state. |
| STATE-TRANSITION-TABLES§preamble-O48 | No generic SetHypothesisState exists. |
| STATE-TRANSITION-TABLES§preamble-O49 | States: |
| STATE-TRANSITION-TABLES§preamble-O50 | ExplicitUnverified |
| STATE-TRANSITION-TABLES§preamble-O51 | ProvisionallyAccepted |
| STATE-TRANSITION-TABLES§preamble-O53 | Rejected |
| STATE-TRANSITION-TABLES§preamble-O54 | Expired |
| STATE-TRANSITION-TABLES§preamble-O55 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§preamble-O56 | \| none \| RecordAssumption \| ExplicitUnverified \| |
| STATE-TRANSITION-TABLES§preamble-O57 | \| ExplicitUnverified \| ProvisionallyAcceptAssumption \| ProvisionallyAccepted \| |
| STATE-TRANSITION-TABLES§preamble-O59 | \| ExplicitUnverified/ProvisionallyAccepted \| RejectAssumption \| Rejected \| |
| STATE-TRANSITION-TABLES§preamble-O62 | Evidence is append/supersede rather than a broad workflow. |
| STATE-TRANSITION-TABLES§preamble-O63 | \| State \| Command \| Result \| |
| STATE-TRANSITION-TABLES§preamble-O64 | \| none \| AdmitEvidence \| Active \| |
| STATE-TRANSITION-TABLES§preamble-O65 | \| Active \| VerifyEvidence \| Active + verification assessment \| |
| STATE-TRANSITION-TABLES§preamble-O66 | \| Active \| CorrectEvidenceMetadata \| Active + corrective event \| |
| STATE-TRANSITION-TABLES§preamble-O67 | \| Active \| SupersedeEvidence \| Superseded \| |
| STATE-TRANSITION-TABLES§preamble-O68 | \| Active/Superseded \| MarkEvidenceArtifactUnavailable \| availability changes \| |
| STATE-TRANSITION-TABLES§preamble-O69 | \| Active/Superseded \| RecordEvidenceIntegrityMismatch \| integrity status changes \| |
| STATE-TRANSITION-TABLES§preamble-O70 | Original evidence content is never overwritten. |
| STATE-TRANSITION-TABLES§preamble-O71 | States: |
| STATE-TRANSITION-TABLES§preamble-O72 | Proposed |
| STATE-TRANSITION-TABLES§preamble-O73 | Approved |
| STATE-TRANSITION-TABLES§preamble-O74 | InProgress |
| STATE-TRANSITION-TABLES§preamble-O75 | Blocked |
| STATE-TRANSITION-TABLES§preamble-O76 | Completed |
| STATE-TRANSITION-TABLES§preamble-O77 | Interpreted |
| STATE-TRANSITION-TABLES§preamble-O78 | Rejected |
| STATE-TRANSITION-TABLES§preamble-O79 | Cancelled |
| STATE-TRANSITION-TABLES§preamble-O80 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§preamble-O81 | \| none \| RecordInvestigationAction \| Proposed \| |
| STATE-TRANSITION-TABLES§preamble-O82 | \| Proposed \| ApproveInvestigationAction \| Approved \| |
| STATE-TRANSITION-TABLES§preamble-O83 | \| Proposed \| RejectInvestigationAction \| Rejected \| |
| STATE-TRANSITION-TABLES§preamble-O84 | \| Approved \| StartInvestigationAction \| InProgress \| |
| STATE-TRANSITION-TABLES§preamble-O85 | \| Approved/InProgress \| BlockInvestigationAction \| Blocked \| |
| STATE-TRANSITION-TABLES§preamble-O86 | \| Blocked \| UnblockInvestigationAction \| Approved or InProgress according to prior state \| |
| STATE-TRANSITION-TABLES§preamble-O87 | \| Approved/InProgress \| CancelInvestigationAction \| Cancelled \| |
| STATE-TRANSITION-TABLES§preamble-O88 | \| InProgress \| CompleteInvestigationAction \| Completed \| |
| STATE-TRANSITION-TABLES§preamble-O89 | \| Completed \| InterpretInvestigationAction \| Interpreted \| |
| STATE-TRANSITION-TABLES§preamble-O90 | States: |
| STATE-TRANSITION-TABLES§preamble-O91 | Identified |
| STATE-TRANSITION-TABLES§preamble-O92 | Investigating |
| STATE-TRANSITION-TABLES§preamble-O93 | Resolved |
| STATE-TRANSITION-TABLES§preamble-O94 | AcceptedResidual |
| STATE-TRANSITION-TABLES§preamble-O95 | Superseded |
| STATE-TRANSITION-TABLES§preamble-O96 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§preamble-O97 | \| none \| IdentifyUnknown \| Identified \| |
| STATE-TRANSITION-TABLES§preamble-O98 | \| Identified \| StartInvestigatingUnknown \| Investigating \| |
| STATE-TRANSITION-TABLES§preamble-O99 | \| Identified/Investigating \| ResolveUnknown \| Resolved \| |
| STATE-TRANSITION-TABLES§preamble-O100 | \| Identified/Investigating \| AcceptUnknownAsResidual \| AcceptedResidual \| |
| STATE-TRANSITION-TABLES§preamble-O101 | \| Identified/Investigating \| SupersedeUnknown \| Superseded \| |
| STATE-TRANSITION-TABLES§preamble-O102 | Materiality is orthogonal and policy-controlled. |
| STATE-TRANSITION-TABLES§preamble-O103 | States: |
| STATE-TRANSITION-TABLES§preamble-O104 | Active |
| STATE-TRANSITION-TABLES§preamble-O105 | Challenged |
| STATE-TRANSITION-TABLES§preamble-O106 | Superseded |
| STATE-TRANSITION-TABLES§preamble-O107 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§preamble-O108 | \| none \| EstablishNegativeKnowledge \| Active \| |
| STATE-TRANSITION-TABLES§preamble-O109 | \| Active \| ChallengeNegativeKnowledge \| Challenged \| |
| STATE-TRANSITION-TABLES§preamble-O110 | \| Active/Challenged \| SupersedeNegativeKnowledge \| Superseded \| |
| STATE-TRANSITION-TABLES§preamble-O111 | Challenge preserves the original scoped record. |
| STATE-TRANSITION-TABLES§preamble-O112 | States: |
| STATE-TRANSITION-TABLES§preamble-O113 | Detected |
| STATE-TRANSITION-TABLES§preamble-O114 | Triaged |
| STATE-TRANSITION-TABLES§preamble-O115 | Investigating |
| STATE-TRANSITION-TABLES§preamble-O116 | Resolved |
| STATE-TRANSITION-TABLES§preamble-O117 | AcceptedResidual |
| STATE-TRANSITION-TABLES§preamble-O118 | Superseded |
| STATE-TRANSITION-TABLES§preamble-O119 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§preamble-O120 | \| none \| RecordContradiction \| Detected \| |
| STATE-TRANSITION-TABLES§preamble-O121 | \| Detected \| TriageContradiction \| Triaged \| |
| STATE-TRANSITION-TABLES§preamble-O122 | \| Detected/Triaged \| StartInvestigatingContradiction \| Investigating \| |
| STATE-TRANSITION-TABLES§preamble-O123 | \| Detected/Triaged/Investigating \| ResolveContradiction \| Resolved \| |
| STATE-TRANSITION-TABLES§preamble-O124 | \| Detected/Triaged/Investigating \| AcceptContradictionAsResidual \| AcceptedResidual \| |
| STATE-TRANSITION-TABLES§preamble-O125 | \| Detected/Triaged/Investigating \| SupersedeContradiction \| Superseded \| |
| STATE-TRANSITION-TABLES§preamble-O126 | Resolution requires an explicit resolution basis. |
| STATE-TRANSITION-TABLES§preamble-O127 | Durable states: |
| STATE-TRANSITION-TABLES§preamble-O129 | InProgress |
| STATE-TRANSITION-TABLES§preamble-O130 | Resolved |
| STATE-TRANSITION-TABLES§preamble-O131 | Waived |
| STATE-TRANSITION-TABLES§preamble-O132 | Superseded |
| STATE-TRANSITION-TABLES§preamble-O134 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§preamble-O140 | Core invariant obligations are never waivable. |
| STATE-TRANSITION-TABLES§preamble-O141 | First-profile states/postures: |
| STATE-TRANSITION-TABLES§preamble-O142 | Proposed |
| STATE-TRANSITION-TABLES§preamble-O143 | Confirmed |
| STATE-TRANSITION-TABLES§preamble-O144 | Reopened |
| STATE-TRANSITION-TABLES§preamble-O145 | Superseded |
| STATE-TRANSITION-TABLES§preamble-O146 | "Supported" should initially be derived from evidence/support assessments. |
| STATE-TRANSITION-TABLES§preamble-O147 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§preamble-O148 | \| none \| RecordCausalAssertion \| Proposed \| |
| STATE-TRANSITION-TABLES§preamble-O149 | \| Proposed \| ConfirmCause \| Confirmed \| |
| STATE-TRANSITION-TABLES§preamble-O150 | \| Confirmed \| ReopenConfirmedCause \| Reopened \| |
| STATE-TRANSITION-TABLES§preamble-O151 | \| Proposed/Reopened \| SupersedeCausalAssertion \| Superseded \| |
| STATE-TRANSITION-TABLES§preamble-O152 | \| Reopened \| ConfirmCause \| Confirmed \| |
| STATE-TRANSITION-TABLES§preamble-O153 | This avoids prematurely persisting a causal support taxonomy. |
| STATE-TRANSITION-TABLES§preamble-O154 | States: |
| STATE-TRANSITION-TABLES§preamble-O155 | Proposed |
| STATE-TRANSITION-TABLES§preamble-O156 | Approved |
| STATE-TRANSITION-TABLES§preamble-O157 | InProgress |
| STATE-TRANSITION-TABLES§preamble-O158 | Implemented |
| STATE-TRANSITION-TABLES§preamble-O159 | AwaitingVerification |
| STATE-TRANSITION-TABLES§preamble-O160 | VerifiedEffective |
| STATE-TRANSITION-TABLES§preamble-O161 | VerificationFailed |
| STATE-TRANSITION-TABLES§preamble-O162 | ReworkRequired |
| STATE-TRANSITION-TABLES§preamble-O163 | Cancelled |
| STATE-TRANSITION-TABLES§preamble-O164 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§preamble-O165 | \| none \| RecordCorrectiveAction \| Proposed \| |
| STATE-TRANSITION-TABLES§preamble-O166 | \| Proposed \| ApproveCorrectiveAction \| Approved \| |
| STATE-TRANSITION-TABLES§preamble-O167 | \| Proposed/Approved \| CancelCorrectiveAction \| Cancelled \| |
| STATE-TRANSITION-TABLES§preamble-O168 | \| Approved \| StartCorrectiveAction \| InProgress \| |
| STATE-TRANSITION-TABLES§preamble-O169 | \| InProgress \| MarkCorrectiveActionImplemented \| Implemented \| |
| STATE-TRANSITION-TABLES§preamble-O170 | \| Implemented \| StartCorrectiveActionVerification \| AwaitingVerification \| |
| STATE-TRANSITION-TABLES§preamble-O171 | \| AwaitingVerification \| MarkCorrectiveActionVerified \| VerifiedEffective \| |
| STATE-TRANSITION-TABLES§preamble-O172 | \| AwaitingVerification \| MarkCorrectiveActionVerificationFailed \| VerificationFailed \| |
| STATE-TRANSITION-TABLES§preamble-O173 | \| VerificationFailed \| RequireCorrectiveActionRework \| ReworkRequired \| |
| STATE-TRANSITION-TABLES§preamble-O174 | \| ReworkRequired \| StartCorrectiveAction \| InProgress \| |
| STATE-TRANSITION-TABLES§preamble-O175 | States: |
| STATE-TRANSITION-TABLES§preamble-O176 | Active |
| STATE-TRANSITION-TABLES§preamble-O177 | Revised |
| STATE-TRANSITION-TABLES§preamble-O179 | Rejected |
| STATE-TRANSITION-TABLES§preamble-O180 | Deferred |
| STATE-TRANSITION-TABLES§preamble-O181 | Stale |
| STATE-TRANSITION-TABLES§preamble-O182 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§preamble-O183 | \| none \| RecordProposal \| Active \| |
| STATE-TRANSITION-TABLES§preamble-O184 | \| Active/Revised \| EditProposal \| Revised \| |
| STATE-TRANSITION-TABLES§preamble-O186 | \| Active/Revised/Deferred \| RejectProposal \| Rejected \| |
| STATE-TRANSITION-TABLES§preamble-O187 | \| Active/Revised \| DeferProposal \| Deferred \| |
| STATE-TRANSITION-TABLES§preamble-O188 | \| Active/Revised/Deferred \| MarkProposalStale \| Stale \| |
| STATE-TRANSITION-TABLES§preamble-O189 | A blocked target command does not produce ProposalAccepted. |
| STATE-TRANSITION-TABLES§preamble-O190 | States: |
| STATE-TRANSITION-TABLES§preamble-O191 | Requested |
| STATE-TRANSITION-TABLES§preamble-O192 | Approved |
| STATE-TRANSITION-TABLES§preamble-O193 | Rejected |
| STATE-TRANSITION-TABLES§preamble-O194 | Withdrawn |
| STATE-TRANSITION-TABLES§preamble-O195 | Expired |
| STATE-TRANSITION-TABLES§preamble-O196 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§preamble-O197 | \| none \| RequestReview \| Requested \| |
| STATE-TRANSITION-TABLES§preamble-O198 | \| Requested \| ApproveReview \| Approved \| |
| STATE-TRANSITION-TABLES§preamble-O199 | \| Requested \| RejectReview \| Rejected \| |
| STATE-TRANSITION-TABLES§preamble-O200 | \| Requested \| WithdrawReview \| Withdrawn \| |
| STATE-TRANSITION-TABLES§preamble-O201 | \| Requested \| ExpireReview \| Expired \| |
| STATE-TRANSITION-TABLES§preamble-O202 | A material state change may make an Approved review stale for a later transition without rewriting the historical review. |
| STATE-TRANSITION-TABLES§preamble-O203 | Conclusions are snapshot records, not mutable lifecycle state. |
| STATE-TRANSITION-TABLES§preamble-O204 | Current conclusion is derived as effective-current projection. |
| STATE-TRANSITION-TABLES§preamble-O205 | Domain-pack activation/upgrades are explicit events. |
| STATE-TRANSITION-TABLES§preamble-O206 | Optional packs may be deactivated if no required semantics become unavailable. |
| STATE-TRANSITION-TABLES§preamble-O207 | Policy pins change through explicit events. |
| STATE-TRANSITION-TABLES§preamble-O208 | Historical events remain associated with their original policy/semantic versions. |
| STATE-TRANSITION-TABLES§preamble-O209 | For every command: |
| STATE-TRANSITION-TABLES§preamble-O210 | validate command/schema; |
| STATE-TRANSITION-TABLES§preamble-O212 | compare expected aggregate version; |
| STATE-TRANSITION-TABLES§preamble-O213 | enforce core invariant firewall; |
| STATE-TRANSITION-TABLES§preamble-O214 | validate structural transition; |
| STATE-TRANSITION-TABLES§preamble-O215 | evaluate core/domain/experimental/organization policy; |
| STATE-TRANSITION-TABLES§preamble-O216 | evaluate actor authorization; |
| STATE-TRANSITION-TABLES§preamble-O217 | evaluate review requirements; |
| STATE-TRANSITION-TABLES§preamble-O218 | propose semantic events; |
| STATE-TRANSITION-TABLES§preamble-O219 | persist atomically; |
| STATE-TRANSITION-TABLES§preamble-O220 | evolve projection and capabilities. |
| STATE-TRANSITION-TABLES§preamble-O221 | A policy can block a structurally legal transition. |
| STATE-TRANSITION-TABLES§preamble-O222 | A policy cannot make a structurally impossible core transition legal. |
| STATE-TRANSITION-TABLES§preamble-O223 | Do not duplicate transition tables in UI, AI adapters, API handlers, and persistence. |
| STATE-TRANSITION-TABLES§preamble-O224 | There must be one domain decision path. |
| STATE-TRANSITION-TABLES§preamble-O226 | state x command legality |
| STATE-TRANSITION-TABLES§preamble-O227 | policy blockers |
| STATE-TRANSITION-TABLES§preamble-O228 | emitted event kinds |
| STATE-TRANSITION-TABLES§preamble-O229 | resulting projection |
| STATE-TRANSITION-TABLES§preamble-O230 | capability changes |
| STATE-TRANSITION-TABLES§preamble-O231 | version conflict behavior |
