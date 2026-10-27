---
id: NR-DESIGN-DECISION-BOUNDARIES§8-agent-rule
status: accepted
version: 1.0.0
priority: unstated
source: DESIGN-DECISION-BOUNDARIES§8-agent-rule
---

# NR-DESIGN-DECISION-BOUNDARIES§8-agent-rule

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
