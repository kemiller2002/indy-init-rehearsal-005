---
id: RQ-CAUSA-2026-0363
title: "STATE-TRANSITION-TABLES§16-transition-evaluation-order 16. Transition evaluation order"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§16-transition-evaluation-order"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:00.602Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0363

Derived from baseline requirement `STATE-TRANSITION-TABLES§16-transition-evaluation-order` (16. Transition evaluation order). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O3 | verify required semantic dependencies; |

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O1 | For every command: |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O2 | validate command/schema; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O4 | compare expected aggregate version; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O5 | enforce core invariant firewall; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O6 | validate structural transition; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O7 | evaluate core/domain/experimental/organization policy; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O8 | evaluate actor authorization; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O9 | evaluate review requirements; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O10 | propose semantic events; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O11 | persist atomically; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O12 | evolve projection and capabilities. |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O13 | A policy can block a structurally legal transition. |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O14 | A policy cannot make a structurally impossible core transition legal. |
