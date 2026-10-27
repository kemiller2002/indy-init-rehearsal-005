---
id: NR-STATE-TRANSITION-TABLES§16-transition-evaluation-order
status: accepted
version: 1.0.0
priority: unstated
source: STATE-TRANSITION-TABLES§16-transition-evaluation-order
---

# NR-STATE-TRANSITION-TABLES§16-transition-evaluation-order

Derived from baseline requirement `STATE-TRANSITION-TABLES§16-transition-evaluation-order` (16. Transition evaluation order). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O3 | verify required semantic dependencies; |

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O1 | For every command: |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O2 | validate command/schema; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O4 | compare expected aggregate version; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O5 | enforce core invariant firewall; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O6 | validate structural transition; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O7 | evaluate core/domain/experimental/organization policy; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O8 | evaluate actor authorization; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O9 | evaluate review requirements; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O10 | propose semantic events; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O11 | persist atomically; |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O12 | evolve projection and capabilities. |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O13 | A policy can block a structurally legal transition. |
| STATE-TRANSITION-TABLES§16-transition-evaluation-order-O14 | A policy cannot make a structurally impossible core transition legal. |
