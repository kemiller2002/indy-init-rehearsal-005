---
id: RQ-CAUSA-2026-0164
title: "EXT-DATA-011 EXT-DATA-011"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-011"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:36.309Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0164

Derived from baseline requirement `EXT-DATA-011` (EXT-DATA-011). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| EXT-DATA-011-O1 | Preserve compatibility with the frozen Causa storage contract as a *reader/writer specification* without vendoring its demonstration records. |
| EXT-DATA-011-O2 | Version the contract and fail closed on incompatible data. |
