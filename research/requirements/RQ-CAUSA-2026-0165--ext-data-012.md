---
id: RQ-CAUSA-2026-0165
title: "EXT-DATA-012 EXT-DATA-012"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-012"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:36.744Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0165

Derived from baseline requirement `EXT-DATA-012` (EXT-DATA-012). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| EXT-DATA-012-O1 | The data repository MUST be configurable and isolated from the application repository. |
| EXT-DATA-012-O2 | Treat repository content as untrusted even when authenticated. |
| EXT-DATA-012-O3 | Do not grant data access through publicly readable Pages assets. |
