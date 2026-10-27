---
id: RQ-CAUSA-2026-0142
title: "DEP-CURRENT-007 Avoid invalid experiments"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DEP-CURRENT-007"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:27.709Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0142

Derived from baseline requirement `DEP-CURRENT-007` (Avoid invalid experiments). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| DEP-CURRENT-007-O1 | A timed experiment must compare a known, fully verified platform baseline. |
| DEP-CURRENT-007-O2 | If current releases cannot be verified, report BLOCKED and separate framework defects from application-agent outcomes. |

| ID | Statement |
|---|---|
| DEP-CURRENT-007-O3 | Do not claim performance or correctness improvements attributable to a run with uncontrolled dependency drift. |
