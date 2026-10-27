---
id: RQ-CAUSA-2026-0358
title: "STATE-TRANSITION-TABLES§11-corrective-action-lifecycle 11. Corrective action lifecycle"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§11-corrective-action-lifecycle"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:58.444Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0358

Derived from baseline requirement `STATE-TRANSITION-TABLES§11-corrective-action-lifecycle` (11. Corrective action lifecycle). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O1 | States: |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O2 | Proposed |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O3 | Approved |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O4 | InProgress |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O5 | Implemented |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O6 | AwaitingVerification |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O7 | VerifiedEffective |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O8 | VerificationFailed |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O9 | ReworkRequired |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O10 | Cancelled |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O11 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O12 | \| none \| RecordCorrectiveAction \| Proposed \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O13 | \| Proposed \| ApproveCorrectiveAction \| Approved \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O14 | \| Proposed/Approved \| CancelCorrectiveAction \| Cancelled \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O15 | \| Approved \| StartCorrectiveAction \| InProgress \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O16 | \| InProgress \| MarkCorrectiveActionImplemented \| Implemented \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O17 | \| Implemented \| StartCorrectiveActionVerification \| AwaitingVerification \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O18 | \| AwaitingVerification \| MarkCorrectiveActionVerified \| VerifiedEffective \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O19 | \| AwaitingVerification \| MarkCorrectiveActionVerificationFailed \| VerificationFailed \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O20 | \| VerificationFailed \| RequireCorrectiveActionRework \| ReworkRequired \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O21 | \| ReworkRequired \| StartCorrectiveAction \| InProgress \| |
