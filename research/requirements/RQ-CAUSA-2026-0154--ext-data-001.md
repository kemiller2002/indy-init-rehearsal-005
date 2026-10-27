---
id: RQ-CAUSA-2026-0154
title: "EXT-DATA-001 EXT-DATA-001"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-001"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:32.450Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0154

Derived from baseline requirement `EXT-DATA-001` (EXT-DATA-001). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| EXT-DATA-001-O1 | The application source, test assets included in production, release artifacts, Pages deployment, and runtime bundle MUST NOT contain the five R001 demonstration investigation datasets, event histories, golden states, or other prepopulated investigation content. |
| EXT-DATA-001-O2 | The data contract, schema definitions, type declarations, command/event catalog, migration rules, and synthetic test generators MAY be included; synthetic tests MUST NOT ship production records. |
