---
id: RQ-CAUSA-2026-0141
title: "DEP-CURRENT-006 Freeze at kickoff, refresh next run"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DEP-CURRENT-006"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:27.282Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0141

Derived from baseline requirement `DEP-CURRENT-006` (Freeze at kickoff, refresh next run). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| DEP-CURRENT-006-O1 | After all applicable verification gates pass, record a complete immutable lock manifest for that run: dependency, selected version, source URL, commit SHA, integrity checksum, discovery timestamp, verification SHA and test evidence. |

| ID | Statement |
|---|---|
| DEP-CURRENT-006-O2 | The run uses exactly this set. |
| DEP-CURRENT-006-O3 | New upstream releases during the run are tracked as drift but do not automatically alter an in-progress experiment. |
| DEP-CURRENT-006-O4 | Repeat discovery and verification for the next run. |
