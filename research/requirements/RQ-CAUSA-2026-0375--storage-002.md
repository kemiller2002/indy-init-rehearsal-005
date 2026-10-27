---
id: RQ-CAUSA-2026-0375
title: "STORAGE-002 Cross-run compatibility"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STORAGE-002"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:05.700Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0375

Derived from baseline requirement `STORAGE-002` (Cross-run compatibility). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STORAGE-002-O2 | Tests MUST compare canonical state and semantic digests, not merely successful JSON parsing. |

| ID | Statement |
|---|---|
| STORAGE-002-O1 | Every new application build MUST read the same frozen dataset and reconstruct equivalent domain state by replaying authoritative events. |
| STORAGE-002-O3 | A fresh build MUST read/write/reload against the same contract without rewriting seed data. |
