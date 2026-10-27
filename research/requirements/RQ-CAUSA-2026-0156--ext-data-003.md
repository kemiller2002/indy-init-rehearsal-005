---
id: RQ-CAUSA-2026-0156
title: "EXT-DATA-003 EXT-DATA-003"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-003"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:33.175Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0156

Derived from baseline requirement `EXT-DATA-003` (EXT-DATA-003). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| EXT-DATA-003-O1 | The user MUST specify or explicitly select the repository and permitted branch/workspace, and provide a scoped credential through a secure connection flow. |
| EXT-DATA-003-O2 | A token alone is not sufficient to infer a safe repository target. |
