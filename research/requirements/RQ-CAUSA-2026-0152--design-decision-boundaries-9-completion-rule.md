---
id: RQ-CAUSA-2026-0152
title: "DESIGN-DECISION-BOUNDARIES§9-completion-rule 9. Completion rule"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DESIGN-DECISION-BOUNDARIES§9-completion-rule"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:31.650Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0152

Derived from baseline requirement `DESIGN-DECISION-BOUNDARIES§9-completion-rule` (9. Completion rule). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| DESIGN-DECISION-BOUNDARIES§9-completion-rule-O1 | A feature is not complete if its implementation silently resolves a Deferred or Experimental question by coupling persisted authoritative data to one answer. |
| DESIGN-DECISION-BOUNDARIES§9-completion-rule-O2 | The feature must either: |
| DESIGN-DECISION-BOUNDARIES§9-completion-rule-O3 | remain behind an extension/version boundary; |
| DESIGN-DECISION-BOUNDARIES§9-completion-rule-O4 | remain a reversible projection; |
| DESIGN-DECISION-BOUNDARIES§9-completion-rule-O5 | or explicitly promote the decision through the recorded process. |
