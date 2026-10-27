---
id: RQ-CAUSA-2026-0362
title: "STATE-TRANSITION-TABLES§15-domain-pack-and-policy-version-behavior 15. Domain-pack and policy version behavior"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§15-domain-pack-and-policy-version-behavior"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:00.123Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0362

Derived from baseline requirement `STATE-TRANSITION-TABLES§15-domain-pack-and-policy-version-behavior` (15. Domain-pack and policy version behavior). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§15-domain-pack-and-policy-version-behavior-O1 | Domain-pack activation/upgrades are explicit events. |
| STATE-TRANSITION-TABLES§15-domain-pack-and-policy-version-behavior-O2 | Optional packs may be deactivated if no required semantics become unavailable. |
| STATE-TRANSITION-TABLES§15-domain-pack-and-policy-version-behavior-O3 | Policy pins change through explicit events. |
| STATE-TRANSITION-TABLES§15-domain-pack-and-policy-version-behavior-O4 | Historical events remain associated with their original policy/semantic versions. |
