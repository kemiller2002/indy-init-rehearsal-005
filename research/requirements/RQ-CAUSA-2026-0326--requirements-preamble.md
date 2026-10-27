---
id: RQ-CAUSA-2026-0326
title: "REQUIREMENTS§preamble Requirements"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REQUIREMENTS§preamble"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:45.066Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0326

Derived from baseline requirement `REQUIREMENTS§preamble` (Requirements). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REQUIREMENTS§preamble-O4 | Provider-neutral fixture data, rapid offline UI/domain testing, real GitHub integration, and release verification are specified in [PROVIDER-FIXTURE-AND-INTEGRATION-REQUIREMENTS.md](PROVIDER-FIXTURE-AND-INTEGRATION-REQUIREMENTS.md). |
| REQUIREMENTS§preamble-O6 | Causal diagram rendering must reuse the verified Echelon workflow layout capability as specified in [WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md](WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md). |
| REQUIREMENTS§preamble-O7 | WDL-001 through WDL-015 are normative for future rehearsals with causal diagrams; preserve their applicability, traceability, and acceptance obligations. |
| REQUIREMENTS§preamble-O9 | Resolve the latest stable compatible releases of each applicable Echelon system at baseline lock, verify integration, and pin immutable versions for reproducibility. |

| ID | Statement |
|---|---|
| REQUIREMENTS§preamble-O1 | The canonical long-term product requirements are in [PRODUCT-REQUIREMENTS.md](PRODUCT-REQUIREMENTS.md). |
| REQUIREMENTS§preamble-O2 | This file defines the first constrained implementation profile. |
| REQUIREMENTS§preamble-O3 | Hackathon-specific constraints must not leak into or narrow the reusable investigation kernel. |
| REQUIREMENTS§preamble-O5 | PF-001 through PF-021 are normative for future rehearsals where applicable; derived rehearsal requirements must explicitly reconcile these obligations and record approved exclusions. |
| REQUIREMENTS§preamble-O8 | All future rehearsals and builds MUST comply with [SYSTEM-VERSION-AND-COMPATIBILITY-REQUIREMENTS.md](SYSTEM-VERSION-AND-COMPATIBILITY-REQUIREMENTS.md), VER-001 through VER-010. |
| REQUIREMENTS§preamble-O10 | Do not assume a hardcoded version remains current. |
| REQUIREMENTS§preamble-O11 | Autonomous engineering reliability controls are specified in [AGENT-RELIABILITY-REQUIREMENTS.md](AGENT-RELIABILITY-REQUIREMENTS.md), AR-001 through AR-018, for applicable future rehearsals. |
