---
id: RQ-CAUSA-2026-0312
title: "REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-c-replay Boundary C: replay"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-c-replay"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:39.202Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0312

Derived from baseline requirement `REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-c-replay` (Boundary C: replay). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-c-replay-O1 | Given an immutable canonical ordered history and semantic/policy version, replay validates every event through the authoritative engine and either yields a projection or a typed invalid-history fault. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-c-replay-O2 | No alternative browser-only validator. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-c-replay-O3 | Frozen fixtures are read-only. |
