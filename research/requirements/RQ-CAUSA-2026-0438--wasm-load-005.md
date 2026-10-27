---
id: RQ-CAUSA-2026-0438
title: "WASM-LOAD-005 Failure and recovery"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "WASM-LOAD-005"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:33.705Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0438

Derived from baseline requirement `WASM-LOAD-005` (Failure and recovery). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| WASM-LOAD-005-O1 | On timeout or initialization failure, show a human-readable error and a functional Retry action that reattempts initialization or safely reloads. |
| WASM-LOAD-005-O2 | Never leave the spinner indefinitely. |
| WASM-LOAD-005-O3 | Avoid exposing sensitive implementation details in user-visible errors. |
