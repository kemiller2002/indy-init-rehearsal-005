---
id: RQ-CAUSA-2026-0051
title: "CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it How R002 and the competition consume it"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:15:45.090Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0051

Derived from baseline requirement `CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it` (How R002 and the competition consume it). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O1 | Pin the tag `causa-storage-v1.0.0` and check that the three identities above match. |
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O2 | Any mismatch is a FAIL |
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O3 | (STORAGE-007). |
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O4 | Run `tools/fetch-engine.sh`, then `causa gate . --anchor causa-storage-v1.0.0` in causa-data. |
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O5 | Write only under `workspaces/demo/<runId>/`, never under `fixtures/`. |
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O6 | The provider token stays in the local store. |
