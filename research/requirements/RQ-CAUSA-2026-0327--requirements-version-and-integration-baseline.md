---
id: RQ-CAUSA-2026-0327
title: "REQUIREMENTS§version-and-integration-baseline Version and integration baseline"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REQUIREMENTS§version-and-integration-baseline"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:45.466Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0327

Derived from baseline requirement `REQUIREMENTS§version-and-integration-baseline` (Version and integration baseline). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REQUIREMENTS§version-and-integration-baseline-O2 | Resolve the latest stable compatible releases of each applicable Echelon system at baseline lock, verify integration, and pin immutable versions for reproducibility. |

| ID | Statement |
|---|---|
| REQUIREMENTS§version-and-integration-baseline-O1 | All future rehearsals and builds MUST comply with [SYSTEM-VERSION-AND-COMPATIBILITY-REQUIREMENTS.md](SYSTEM-VERSION-AND-COMPATIBILITY-REQUIREMENTS.md), VER-001 through VER-010. |
| REQUIREMENTS§version-and-integration-baseline-O3 | Do not assume a hardcoded version remains current. |
| REQUIREMENTS§version-and-integration-baseline-O4 | Autonomous engineering reliability controls are specified in [AGENT-RELIABILITY-REQUIREMENTS.md](AGENT-RELIABILITY-REQUIREMENTS.md), AR-001 through AR-018, for applicable future rehearsals. |
