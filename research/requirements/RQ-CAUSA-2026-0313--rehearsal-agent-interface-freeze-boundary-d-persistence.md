---
id: RQ-CAUSA-2026-0313
title: "REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence Boundary D: persistence"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:39.585Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0313

Derived from baseline requirement `REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence` (Boundary D: persistence). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence-O1 | Provider-neutral operations: load, append with expected version, acknowledge remote persistence, surface conflict, and recover local edits. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence-O2 | Separate local-durable from remotely-synced status. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence-O3 | Storage adapter alone maps to causa-data v1.0.0 paths/serialization. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-d-persistence-O4 | No secrets in browser bundles. |
