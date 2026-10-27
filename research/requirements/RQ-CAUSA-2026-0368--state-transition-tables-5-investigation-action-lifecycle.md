---
id: RQ-CAUSA-2026-0368
title: "STATE-TRANSITION-TABLES§5-investigation-action-lifecycle 5. Investigation action lifecycle"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§5-investigation-action-lifecycle"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:02.653Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0368

Derived from baseline requirement `STATE-TRANSITION-TABLES§5-investigation-action-lifecycle` (5. Investigation action lifecycle). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O1 | States: |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O2 | Proposed |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O3 | Approved |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O4 | InProgress |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O5 | Blocked |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O6 | Completed |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O7 | Interpreted |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O8 | Rejected |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O9 | Cancelled |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O10 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O11 | \| none \| RecordInvestigationAction \| Proposed \| |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O12 | \| Proposed \| ApproveInvestigationAction \| Approved \| |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O13 | \| Proposed \| RejectInvestigationAction \| Rejected \| |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O14 | \| Approved \| StartInvestigationAction \| InProgress \| |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O15 | \| Approved/InProgress \| BlockInvestigationAction \| Blocked \| |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O16 | \| Blocked \| UnblockInvestigationAction \| Approved or InProgress according to prior state \| |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O17 | \| Approved/InProgress \| CancelInvestigationAction \| Cancelled \| |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O18 | \| InProgress \| CompleteInvestigationAction \| Completed \| |
| STATE-TRANSITION-TABLES§5-investigation-action-lifecycle-O19 | \| Completed \| InterpretInvestigationAction \| Interpreted \| |
