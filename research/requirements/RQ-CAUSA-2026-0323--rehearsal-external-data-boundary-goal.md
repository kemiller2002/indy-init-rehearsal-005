---
id: RQ-CAUSA-2026-0323
title: "REHEARSAL-EXTERNAL-DATA-BOUNDARY§goal Goal"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-EXTERNAL-DATA-BOUNDARY§goal"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:43.782Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0323

Derived from baseline requirement `REHEARSAL-EXTERNAL-DATA-BOUNDARY§goal` (Goal). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| REHEARSAL-EXTERNAL-DATA-BOUNDARY§goal-O1 | The app must build and operate from a clean checkout with **zero bundled demonstration investigations**. |
| REHEARSAL-EXTERNAL-DATA-BOUNDARY§goal-O2 | It knows how to create, validate, replay, serialize, and persist Causa objects, but obtains actual investigation records only after the user explicitly connects an authorized external GitHub repository. |
