---
id: RQ-CAUSA-2026-0219
title: "PROVIDER-FIXTURE-AND-INTEGRATION§preamble Provider Fixtures, Fast UI Verification, and Integration Requirements"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "PROVIDER-FIXTURE-AND-INTEGRATION§preamble"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:59.193Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0219

Derived from baseline requirement `PROVIDER-FIXTURE-AND-INTEGRATION§preamble` (Provider Fixtures, Fast UI Verification, and Integration Requirements). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| PROVIDER-FIXTURE-AND-INTEGRATION§preamble-O4 | These requirements supplement, not replace, the storage, security, UI, and acceptance contracts. |

| ID | Statement |
|---|---|
| PROVIDER-FIXTURE-AND-INTEGRATION§preamble-O1 | Authority: Indy-init. |
| PROVIDER-FIXTURE-AND-INTEGRATION§preamble-O2 | Applicability: all rehearsals and implementations with external data providers, subject to explicitly approved scope decisions. |
| PROVIDER-FIXTURE-AND-INTEGRATION§preamble-O3 | Purpose: make UI/domain development fast and deterministic while independently verifying real provider communication. |
