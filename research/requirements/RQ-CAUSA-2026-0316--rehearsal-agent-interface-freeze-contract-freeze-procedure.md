---
id: RQ-CAUSA-2026-0316
title: "REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure Contract freeze procedure"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:40.756Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0316

Derived from baseline requirement `REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure` (Contract freeze procedure). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O1 | Extract actual types and exported interfaces from accepted R001 F# source and Limen adapter. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O2 | Publish one versioned interface manifest including module/type names, request/response examples, error variants and contract tests. |

| ID | Statement |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O3 | Run golden replay of all five frozen scenarios and negative-history cases. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O4 | Each owner signs off against the exact interface-manifest SHA before parallel work. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O5 | Breaking change requires coordinator decision, manifest revision, consumer notification and compatibility proof. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O6 | No agent edits another owner's shared interface independently. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O7 | An interface not extracted and validated is BLOCKED, not frozen. |
