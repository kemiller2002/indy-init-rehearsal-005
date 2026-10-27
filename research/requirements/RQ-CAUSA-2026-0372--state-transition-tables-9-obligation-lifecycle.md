---
id: RQ-CAUSA-2026-0372
title: "STATE-TRANSITION-TABLES§9-obligation-lifecycle 9. Obligation lifecycle"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§9-obligation-lifecycle"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:04.431Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0372

Derived from baseline requirement `STATE-TRANSITION-TABLES§9-obligation-lifecycle` (9. Obligation lifecycle). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O2 | Accepted |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O7 | Derived obligations may exist before Accepted. |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O9 | \| derived candidate \| AcceptObligation \| Accepted \| |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O10 | \| Accepted \| StartObligation \| InProgress \| |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O11 | \| Accepted/InProgress \| ResolveObligation \| Resolved \| |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O12 | \| Accepted/InProgress \| WaiveObligation \| Waived if policy permits \| |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O13 | \| Accepted/InProgress \| SupersedeObligation \| Superseded \| |

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O1 | Durable states: |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O3 | InProgress |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O4 | Resolved |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O5 | Waived |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O6 | Superseded |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O8 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§9-obligation-lifecycle-O14 | Core invariant obligations are never waivable. |
