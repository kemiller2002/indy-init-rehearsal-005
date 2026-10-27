---
id: RQ-CAUSA-2026-0163
title: "EXT-DATA-010 EXT-DATA-010"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-010"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:35.924Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0163

Derived from baseline requirement `EXT-DATA-010` (EXT-DATA-010). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| EXT-DATA-010-O1 | R001 remains an immutable experimental baseline. |
| EXT-DATA-010-O2 | The next rehearsal MUST measure external-data startup, authorization, read, write, and recovery independently. |
| EXT-DATA-010-O3 | Do not claim an external provider PASS based on mocked requests alone. |
