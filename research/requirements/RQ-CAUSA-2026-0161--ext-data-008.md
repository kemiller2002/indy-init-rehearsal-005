---
id: RQ-CAUSA-2026-0161
title: "EXT-DATA-008 EXT-DATA-008"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-008"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:35.153Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0161

Derived from baseline requirement `EXT-DATA-008` (EXT-DATA-008). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| EXT-DATA-008-O2 | A new empty investigation MUST be command-created, schema-validated, replayable, and round-trip verified. |

| ID | Statement |
|---|---|
| EXT-DATA-008-O1 | The app MUST create new Causa v1 file sets from the authoritative schema and engine without copying a demo investigation. |
