---
id: RQ-CAUSA-2026-0364
title: "STATE-TRANSITION-TABLES§17-implementation-rule 17. Implementation rule"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§17-implementation-rule"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:00.985Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0364

Derived from baseline requirement `STATE-TRANSITION-TABLES§17-implementation-rule` (17. Implementation rule). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STATE-TRANSITION-TABLES§17-implementation-rule-O3 | Table-driven tests should cover: |

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§17-implementation-rule-O1 | Do not duplicate transition tables in UI, AI adapters, API handlers, and persistence. |
| STATE-TRANSITION-TABLES§17-implementation-rule-O2 | There must be one domain decision path. |
| STATE-TRANSITION-TABLES§17-implementation-rule-O4 | state x command legality |
| STATE-TRANSITION-TABLES§17-implementation-rule-O5 | policy blockers |
| STATE-TRANSITION-TABLES§17-implementation-rule-O6 | emitted event kinds |
| STATE-TRANSITION-TABLES§17-implementation-rule-O7 | resulting projection |
| STATE-TRANSITION-TABLES§17-implementation-rule-O8 | capability changes |
| STATE-TRANSITION-TABLES§17-implementation-rule-O9 | version conflict behavior |
