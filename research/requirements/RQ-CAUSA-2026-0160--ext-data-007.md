---
id: RQ-CAUSA-2026-0160
title: "EXT-DATA-007 EXT-DATA-007"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-007"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:34.769Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0160

Derived from baseline requirement `EXT-DATA-007` (EXT-DATA-007). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| EXT-DATA-007-O1 | The UI MUST distinguish disconnected, connecting, authorized-empty, loading, incompatible, access-denied, offline, and conflict states. |
| EXT-DATA-007-O2 | Never imply a successful connection merely because a token was supplied. |
