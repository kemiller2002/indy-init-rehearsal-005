---
id: RQ-CAUSA-2026-0057
title: "CAUSA-UI-EXPERIENCE§preamble Causa UI Experience Requirements"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "CAUSA-UI-EXPERIENCE§preamble"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:15:51.076Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0057

Derived from baseline requirement `CAUSA-UI-EXPERIENCE§preamble` (Causa UI Experience Requirements). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| CAUSA-UI-EXPERIENCE§preamble-O1 | Scope: investigation dashboard, causal graph, timeline, command interface, evidence inspector, persistence feedback, search, comparison, reporting, recovery, accessibility, and onboarding. |
| CAUSA-UI-EXPERIENCE§preamble-O2 | The F# domain engine is the sole authority for legal commands, state transitions, causal assertions, and verification. |
| CAUSA-UI-EXPERIENCE§preamble-O3 | UI components render projections; they MUST NOT invent conclusions, confidence scores, or permitted actions. |
| CAUSA-UI-EXPERIENCE§preamble-O4 | All displayed investigative facts MUST be traceable to immutable event history or explicitly marked as derived/presentation-only. |
| CAUSA-UI-EXPERIENCE§preamble-O5 | The frozen Causa v1.0.0 dataset MUST be rendered without mutation. |
| CAUSA-UI-EXPERIENCE§preamble-O6 | Editing requires an isolated working copy. |
| CAUSA-UI-EXPERIENCE§preamble-O7 | Use Forma for consistent components, Limen for WebAssembly integration, and Folio for print/reporting where appropriate. |
| CAUSA-UI-EXPERIENCE§preamble-O8 | No unnecessary framework dependencies. |
| CAUSA-UI-EXPERIENCE§preamble-O9 | Support iPad-first touch, keyboard, mobile, and desktop. |
| CAUSA-UI-EXPERIENCE§preamble-O10 | Loading behavior is separately specified in WASM-LOADING-EXPERIENCE-REQUIREMENTS.md. |
| CAUSA-UI-EXPERIENCE§preamble-O11 | Screens MUST distinguish unavailable data from zero counts, unknown values, and false claims. |
