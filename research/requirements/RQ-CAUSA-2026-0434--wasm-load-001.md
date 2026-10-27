---
id: RQ-CAUSA-2026-0434
title: "WASM-LOAD-001 Initial loading screen"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "WASM-LOAD-001"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:32.073Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0434

Derived from baseline requirement `WASM-LOAD-001` (Initial loading screen). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| WASM-LOAD-001-O1 | The application MUST render an accessible, branded loading screen immediately, before the F#/Limen WebAssembly runtime is initialized and interactive. |
| WASM-LOAD-001-O2 | The screen MUST be implemented in the initial HTML/CSS shell so that it does not depend on successful WebAssembly startup. |
| WASM-LOAD-001-O3 | Do not display a blank page while the runtime loads. |
