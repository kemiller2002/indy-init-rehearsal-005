---
id: RQ-CAUSA-2026-0162
title: "EXT-DATA-009 EXT-DATA-009"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-009"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:35.550Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0162

Derived from baseline requirement `EXT-DATA-009` (EXT-DATA-009). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| EXT-DATA-009-O1 | Build and acceptance tests MUST demonstrate a clean install with zero production investigation fixtures, no preloaded investigation records, successful authorized retrieval from a separately populated test repository, denial without authorization, malformed-data refusal, and successful create/read/write/reload. |
| EXT-DATA-009-O2 | The deployed exact SHA must be verified. |
