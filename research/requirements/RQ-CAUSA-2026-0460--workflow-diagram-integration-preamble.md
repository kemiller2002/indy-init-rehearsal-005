---
id: RQ-CAUSA-2026-0460
title: "WORKFLOW-DIAGRAM-INTEGRATION§preamble Workflow Layout Integration and Causal Diagram Requirements"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "WORKFLOW-DIAGRAM-INTEGRATION§preamble"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:43.053Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0460

Derived from baseline requirement `WORKFLOW-DIAGRAM-INTEGRATION§preamble` (Workflow Layout Integration and Causal Diagram Requirements). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| WORKFLOW-DIAGRAM-INTEGRATION§preamble-O2 | These requirements supplement the Causa UI, screen, provider, and acceptance contracts. |
| WORKFLOW-DIAGRAM-INTEGRATION§preamble-O4 | First discover and verify the actual owning repository, public API, supported rendering model, version, and availability. |

| ID | Statement |
|---|---|
| WORKFLOW-DIAGRAM-INTEGRATION§preamble-O1 | Authority: Indy-init. |
| WORKFLOW-DIAGRAM-INTEGRATION§preamble-O3 | Causa must compose the **existing Echelon workflow layout capability** rather than implement a new hardcoded graph layout engine. |
| WORKFLOW-DIAGRAM-INTEGRATION§preamble-O5 | Do not assume the layout engine lives in Forma Studio, and do not invent an API. |
| WORKFLOW-DIAGRAM-INTEGRATION§preamble-O6 | If the capability is unavailable or lacks a required contract, record the gap and propose work in its owning repository through the canonical Praxis input process. |
