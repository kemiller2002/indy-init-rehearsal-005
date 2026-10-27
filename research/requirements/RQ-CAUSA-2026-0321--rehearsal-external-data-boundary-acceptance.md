---
id: RQ-CAUSA-2026-0321
title: "REHEARSAL-EXTERNAL-DATA-BOUNDARY§acceptance Acceptance"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-EXTERNAL-DATA-BOUNDARY§acceptance"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:42.864Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0321

Derived from baseline requirement `REHEARSAL-EXTERNAL-DATA-BOUNDARY§acceptance` (Acceptance). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-EXTERNAL-DATA-BOUNDARY§acceptance-O1 | A clean production build contains no demo investigation IDs, titles, events, snapshots or fixture bundle; first launch has zero investigations; connection to an explicitly chosen authorized data repository reveals only the permitted records; disconnect removes remote access and cached sensitive display state as policy requires; write and conflict tests are backed by independent SHA reads. |
