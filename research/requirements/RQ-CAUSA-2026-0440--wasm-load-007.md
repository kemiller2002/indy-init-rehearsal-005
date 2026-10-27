---
id: RQ-CAUSA-2026-0440
title: "WASM-LOAD-007 Asset and branding requirements"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "WASM-LOAD-007"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:34.491Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0440

Derived from baseline requirement `WASM-LOAD-007` (Asset and branding requirements). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| WASM-LOAD-007-O1 | Provide a reusable Causa icon suitable for the loading screen, favicon, and app identity. |
| WASM-LOAD-007-O2 | Prefer local/versioned assets that are available before WebAssembly loads; provide a text fallback if the icon fails. |
| WASM-LOAD-007-O3 | No remote third-party dependency may be required for the first render. |
