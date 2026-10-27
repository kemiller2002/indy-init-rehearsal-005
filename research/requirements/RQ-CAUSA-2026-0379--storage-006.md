---
id: RQ-CAUSA-2026-0379
title: "STORAGE-006 Integrity and concurrency"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STORAGE-006"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:07.699Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0379

Derived from baseline requirement `STORAGE-006` (Integrity and concurrency). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STORAGE-006-O2 | Verify append-only semantic history, aggregate-version ordering, duplicate/gap detection, expected-version conflict refusal, and atomic semantic transition persistence. |

| ID | Statement |
|---|---|
| STORAGE-006-O1 | Record fixture manifest and checksums. |
| STORAGE-006-O3 | Snapshots are derived; provider metadata is separate from domain meaning. |
