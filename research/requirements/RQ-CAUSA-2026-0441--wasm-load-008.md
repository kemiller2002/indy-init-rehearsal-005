---
id: RQ-CAUSA-2026-0441
title: "WASM-LOAD-008 Acceptance"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "WASM-LOAD-008"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:34.901Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0441

Derived from baseline requirement `WASM-LOAD-008` (Acceptance). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| WASM-LOAD-008-O1 | Verify via automated browser checks: shell appears before delayed WebAssembly initialization; correct ready handoff; no false progress; simulated download/init failure shows recovery; retry works; reduced-motion, keyboard, accessibility and responsive checks pass. |
| WASM-LOAD-008-O2 | Include a deployment-level check of the actual published build. |
| WASM-LOAD-008-O3 | Preserve the exact verified SHA and acceptance-gate requirements in DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md. |
