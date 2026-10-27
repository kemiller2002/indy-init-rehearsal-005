---
id: RQ-CAUSA-2026-0151
title: "DESIGN-DECISION-BOUNDARIES§8-agent-rule 8. Agent rule"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DESIGN-DECISION-BOUNDARIES§8-agent-rule"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:31.227Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0151

Derived from baseline requirement `DESIGN-DECISION-BOUNDARIES§8-agent-rule` (8. Agent rule). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| DESIGN-DECISION-BOUNDARIES§8-agent-rule-O1 | Agents implementing the application must consult this document before making architecture-level assumptions. |
| DESIGN-DECISION-BOUNDARIES§8-agent-rule-O2 | When an unspecified decision is required during implementation: |
| DESIGN-DECISION-BOUNDARIES§8-agent-rule-O3 | prefer the most reversible option; |
| DESIGN-DECISION-BOUNDARIES§8-agent-rule-O4 | do not promote domain research into generic core; |
| DESIGN-DECISION-BOUNDARIES§8-agent-rule-O5 | record the choice as a ReversibleChoice unless an existing invariant determines it; |
| DESIGN-DECISION-BOUNDARIES§8-agent-rule-O6 | preserve compatibility seams; |
| DESIGN-DECISION-BOUNDARIES§8-agent-rule-O7 | create an explicit open decision if the choice would be difficult to undo. |
