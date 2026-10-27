---
id: RQ-CAUSA-2026-0359
title: "STATE-TRANSITION-TABLES§12-proposal-lifecycle 12. Proposal lifecycle"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§12-proposal-lifecycle"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:58.866Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0359

Derived from baseline requirement `STATE-TRANSITION-TABLES§12-proposal-lifecycle` (12. Proposal lifecycle). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O4 | Accepted |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O11 | \| Active/Revised/Deferred \| AcceptProposal \| Accepted only if target command succeeds \| |

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O1 | States: |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O2 | Active |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O3 | Revised |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O5 | Rejected |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O6 | Deferred |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O7 | Stale |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O8 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O9 | \| none \| RecordProposal \| Active \| |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O10 | \| Active/Revised \| EditProposal \| Revised \| |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O12 | \| Active/Revised/Deferred \| RejectProposal \| Rejected \| |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O13 | \| Active/Revised \| DeferProposal \| Deferred \| |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O14 | \| Active/Revised/Deferred \| MarkProposalStale \| Stale \| |
| STATE-TRANSITION-TABLES§12-proposal-lifecycle-O15 | A blocked target command does not produce ProposalAccepted. |
