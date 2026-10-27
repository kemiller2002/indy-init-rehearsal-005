---
id: RQ-CAUSA-2026-0382
title: "STORAGE-009 Ownership"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STORAGE-009"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:09.172Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0382

Derived from baseline requirement `STORAGE-009` (Ownership). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| STORAGE-009-O1 | The application storage adapter owns GitHub paths and provider tokens; the F# domain model owns semantic validity. |
| STORAGE-009-O2 | The frozen wire contract is independent of Forma, Limen, and UI release versions. |
