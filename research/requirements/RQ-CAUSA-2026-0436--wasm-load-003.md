---
id: RQ-CAUSA-2026-0436
title: "WASM-LOAD-003 Clear states"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "WASM-LOAD-003"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:32.926Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0436

Derived from baseline requirement `WASM-LOAD-003` (Clear states). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| WASM-LOAD-003-O1 | Distinguish downloading/initializing, ready, and failed-to-load states. |
| WASM-LOAD-003-O2 | Only show percentage progress when measured against real runtime progress; otherwise use indeterminate progress and descriptive text (e.g. |
| WASM-LOAD-003-O3 | "Starting Causa…"). |
| WASM-LOAD-003-O4 | Never display fabricated progress. |
