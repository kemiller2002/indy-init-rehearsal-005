---
id: RQ-CAUSA-2026-0380
title: "STORAGE-007 Release acceptance"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STORAGE-007"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:08.205Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0380

Derived from baseline requirement `STORAGE-007` (Release acceptance). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STORAGE-007-O1 | For every R002 and competition candidate: verify schema version/hash; load all valid frozen fixtures; replay histories through F# engine; compare expected projections; perform create/update/reload and conflict tests against disposable demo workspace; confirm seed hash unchanged. |
| STORAGE-007-O2 | Any unexpected schema change, silent rewrite, failed replay, or invalid fixture accepted as valid = FAIL. |
| STORAGE-007-O3 | Missing live API test = BLOCKED/UNKNOWN, never PASS. |
