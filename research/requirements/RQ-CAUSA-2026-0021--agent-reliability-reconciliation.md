---
id: RQ-CAUSA-2026-0021
title: "AGENT-RELIABILITY§reconciliation Reconciliation"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "AGENT-RELIABILITY§reconciliation"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:15:32.150Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0021

Derived from baseline requirement `AGENT-RELIABILITY§reconciliation` (Reconciliation). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| AGENT-RELIABILITY§reconciliation-O2 | In particular AR-003 protects genuine approval authority, not agent-maintained pseudo-authority; AR-006 concerns domain correctness, not universal typed agent messages; AR-013 allows test prioritization, not skipping mandatory CI. |

| ID | Statement |
|---|---|
| AGENT-RELIABILITY§reconciliation-O1 | AR-019..AR-026 refine AR-001..AR-018 when there is tension. |
| AGENT-RELIABILITY§reconciliation-O3 | These requirements do not establish that any release or implementation is complete. |
