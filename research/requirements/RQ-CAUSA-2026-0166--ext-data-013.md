---
id: RQ-CAUSA-2026-0166
title: "EXT-DATA-013 EXT-DATA-013"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: critical
tags: [rehearsal-005, identity-derivation]
source: "EXT-DATA-013"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:37.146Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0166

Derived from baseline requirement `EXT-DATA-013` (EXT-DATA-013). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| EXT-DATA-013-O3 | The five synthetic v1.0.0 fixtures remain the storage contract's test corpus only (STORAGE-005, STORAGE-007). |

| ID | Statement |
|---|---|
| EXT-DATA-013-O1 | The active demonstration catalog is the one named by causa-data `catalog/active.json` (historical-v1): exactly three historical investigations, Apollo 1 (AS-204), Apollo 11 and Space Shuttle Challenger (STS-51-L). |
| EXT-DATA-013-O2 | After an explicit Connect to that repository and workspace, the application MUST load all three through the full contract validation and F# replay pipeline to the states recorded in the catalog manifest, MUST NOT present any synthetic investigation as a catalog case, and MUST NOT bundle any of their records. |
