---
id: RQ-CAUSA-2026-0158
title: "EXT-DATA-005 EXT-DATA-005"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-005"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:34.020Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0158

Derived from baseline requirement `EXT-DATA-005` (EXT-DATA-005). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| EXT-DATA-005-O1 | The GitHub provider MUST check repository and branch permissions, validate file paths and manifests, and load only schema-compatible data through the full contract validation and F# history-admissibility pipeline. |
| EXT-DATA-005-O2 | Malformed, forged, unauthorized, or incompatible data MUST fail closed with safe diagnostics. |
