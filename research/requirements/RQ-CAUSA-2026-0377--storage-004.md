---
id: RQ-CAUSA-2026-0377
title: "STORAGE-004 Repository isolation"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STORAGE-004"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:06.630Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0377

Derived from baseline requirement `STORAGE-004` (Repository isolation). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| STORAGE-004-O1 | Proposed personal-account datastore: kemiller2002/causa-data (private initially). |
| STORAGE-004-O2 | Keep immutable seed fixtures in a tagged commit and mutable demo writes in a separate branch/path. |
| STORAGE-004-O3 | Never write secrets or personal data to fixtures. |
| STORAGE-004-O4 | Do not store GitHub tokens. |
