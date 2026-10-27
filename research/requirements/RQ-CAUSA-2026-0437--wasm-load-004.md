---
id: RQ-CAUSA-2026-0437
title: "WASM-LOAD-004 Handoff"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "WASM-LOAD-004"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:33.320Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0437

Derived from baseline requirement `WASM-LOAD-004` (Handoff). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| WASM-LOAD-004-O1 | Keep the loading shell visible until the WebAssembly runtime and required application initialization have completed and the interactive UI is ready. |
| WASM-LOAD-004-O2 | Transition without a blank frame or duplicated controls. |
| WASM-LOAD-004-O3 | Do not mark ready merely because the .wasm download completed. |
