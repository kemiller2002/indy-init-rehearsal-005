---
id: RQ-CAUSA-2026-0376
title: "STORAGE-003 Schema evolution"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STORAGE-003"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:06.170Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0376

Derived from baseline requirement `STORAGE-003` (Schema evolution). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STORAGE-003-O1 | Changes to the frozen contract require an explicit new schema version, documented rationale, reviewed migration, forward/backward compatibility matrix, and test fixtures for both versions. |
| STORAGE-003-O4 | An agent MUST fail the acceptance gate on unapproved schema drift. |

| ID | Statement |
|---|---|
| STORAGE-003-O2 | No in-place migration or destructive rewrite of the frozen baseline. |
| STORAGE-003-O3 | Incompatible change requires explicit authorization. |
