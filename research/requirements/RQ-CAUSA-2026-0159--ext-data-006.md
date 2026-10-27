---
id: RQ-CAUSA-2026-0159
title: "EXT-DATA-006 EXT-DATA-006"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-006"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:34.391Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0159

Derived from baseline requirement `EXT-DATA-006` (EXT-DATA-006). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| EXT-DATA-006-O2 | Creating a new investigation is an explicit user action; write/publish requires separately verified authorization, optimistic concurrency, and no overwrite of unread remote history. |

| ID | Statement |
|---|---|
| EXT-DATA-006-O1 | No data or repository mutation occurs on connect or read. |
