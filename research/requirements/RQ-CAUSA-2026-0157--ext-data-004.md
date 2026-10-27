---
id: RQ-CAUSA-2026-0157
title: "EXT-DATA-004 EXT-DATA-004"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-004"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:33.630Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0157

Derived from baseline requirement `EXT-DATA-004` (EXT-DATA-004). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| EXT-DATA-004-O1 | Credentials MUST never be committed, bundled, logged, embedded in URLs, exposed in error text, stored in localStorage, or written into domain event files. |
| EXT-DATA-004-O2 | Prefer short-lived least-privilege tokens or GitHub App authorization. |
| EXT-DATA-004-O3 | In-memory-only credentials require reauthorization after reload. |
| EXT-DATA-004-O4 | Do not use an unrestricted personal access token by default. |
