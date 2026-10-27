---
id: RQ-CAUSA-2026-0374
title: "STORAGE-001 Immutable versioned storage contract"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STORAGE-001"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:05.225Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0374

Derived from baseline requirement `STORAGE-001` (Immutable versioned storage contract). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| STORAGE-001-O1 | Before seeding demo data, select and publish a canonical versioned storage contract including repository paths, manifest, event envelope, event ordering, identifier formats, field names/types, null semantics, timestamps, snapshot rules, and serialization. |
| STORAGE-001-O2 | Freeze version 1.0 for all rehearsal and competition runs. |
| STORAGE-001-O3 | Application builds MUST NOT silently change it. |
