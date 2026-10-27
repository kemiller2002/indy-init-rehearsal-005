---
id: RQ-CAUSA-2026-0311
title: "REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-b-versioned-projection Boundary B: versioned projection"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-b-versioned-projection"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:38.787Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0311

Derived from baseline requirement `REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-b-versioned-projection` (Boundary B: versioned projection). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-b-versioned-projection-O1 | A single immutable projection identifies investigation, aggregate version, lifecycle, hypotheses, evidence, contradictions, obligations, causal assertions, permitted capabilities and reasons for blocked actions. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-b-versioned-projection-O2 | Fields MUST map to existing domain types; do not add synthetic confidence, inferred cause, or made-up severity/owner. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-b-versioned-projection-O3 | Every screen must consume the same versioned projection or explicitly label stale state. |
