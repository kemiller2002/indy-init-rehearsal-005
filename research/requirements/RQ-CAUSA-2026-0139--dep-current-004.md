---
id: RQ-CAUSA-2026-0139
title: "DEP-CURRENT-004 Verify before lock"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DEP-CURRENT-004"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:26.451Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0139

Derived from baseline requirement `DEP-CURRENT-004` (Verify before lock). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| DEP-CURRENT-004-O1 | For the candidate stack, run clean restore/install, F# build/tests, Limen startup, Forma rendering and accessibility, Folio printing, frozen-data golden replay, persistence conflict/remote-acknowledgment tests where applicable, and exact-deployment acceptance. |
| DEP-CURRENT-004-O3 | Do not label a candidate VERIFIED based solely on published version metadata. |

| ID | Statement |
|---|---|
| DEP-CURRENT-004-O2 | Collect machine-readable outcomes, commands, source SHAs, artifact digests, and logs. |
