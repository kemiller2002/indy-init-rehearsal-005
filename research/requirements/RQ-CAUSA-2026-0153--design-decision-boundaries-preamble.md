---
id: RQ-CAUSA-2026-0153
title: "DESIGN-DECISION-BOUNDARIES§preamble Design Decision Boundaries"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DESIGN-DECISION-BOUNDARIES§preamble"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:32.075Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0153

Derived from baseline requirement `DESIGN-DECISION-BOUNDARIES§preamble` (Design Decision Boundaries). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| DESIGN-DECISION-BOUNDARIES§preamble-O1 | This document prevents provisional architecture and active research from hardening accidentally into permanent product constraints. |
| DESIGN-DECISION-BOUNDARIES§preamble-O2 | Every consequential design decision should be treated as one of: |
| DESIGN-DECISION-BOUNDARIES§preamble-O3 | LockedInvariant |
| DESIGN-DECISION-BOUNDARIES§preamble-O4 | StableDirection |
| DESIGN-DECISION-BOUNDARIES§preamble-O5 | ReversibleChoice |
| DESIGN-DECISION-BOUNDARIES§preamble-O6 | Experimental |
| DESIGN-DECISION-BOUNDARIES§preamble-O7 | Deferred |
| DESIGN-DECISION-BOUNDARIES§preamble-O8 | Agents and implementers must not promote a ReversibleChoice, Experimental item, or Deferred question into a LockedInvariant without explicit evidence and a recorded decision. |
| DESIGN-DECISION-BOUNDARIES§preamble-O9 | These are foundational product guarantees. |
