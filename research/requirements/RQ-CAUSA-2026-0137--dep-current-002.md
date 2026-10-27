---
id: RQ-CAUSA-2026-0137
title: "DEP-CURRENT-002 Prefer latest approved stable"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DEP-CURRENT-002"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:25.620Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0137

Derived from baseline requirement `DEP-CURRENT-002` (Prefer latest approved stable). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| DEP-CURRENT-002-O1 | Select the newest approved stable release for each required component as the candidate. |
| DEP-CURRENT-002-O2 | A newer compatible source commit may be used instead only with explicit source SHA, build provenance, and documented approval. |
| DEP-CURRENT-002-O3 | Do not automatically select unstable previews or unreviewed main-branch commits merely because they are newer. |
| DEP-CURRENT-002-O4 | Do not silently retain R001 versions. |
