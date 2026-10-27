---
id: RQ-CAUSA-2026-0310
title: "REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission Boundary A: command submission"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:38.323Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0310

Derived from baseline requirement `REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission` (Boundary A: command submission). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission-O2 | Output MUST distinguish accepted semantic events/new version from typed rejection/fault. |

| ID | Statement |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission-O1 | Input MUST include investigation ID, actor/provenance, typed command, expected aggregate version, and command identity/idempotency context where supported. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission-O3 | UI MUST NOT generate authoritative events directly. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission-O4 | Domain owner publishes concrete F# signatures and JSON/WASM boundary shape from source. |
