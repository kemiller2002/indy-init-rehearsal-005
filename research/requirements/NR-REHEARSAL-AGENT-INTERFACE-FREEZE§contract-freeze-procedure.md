---
id: NR-REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure
status: accepted
version: 1.0.0
priority: unstated
source: REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure
---

# NR-REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure

Derived from baseline requirement `REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure` (Contract freeze procedure). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O1 | Extract actual types and exported interfaces from accepted R001 F# source and Limen adapter. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O2 | Publish one versioned interface manifest including module/type names, request/response examples, error variants and contract tests. |

| ID | Statement |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O3 | Run golden replay of all five frozen scenarios and negative-history cases. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O4 | Each owner signs off against the exact interface-manifest SHA before parallel work. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O5 | Breaking change requires coordinator decision, manifest revision, consumer notification and compatibility proof. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O6 | No agent edits another owner's shared interface independently. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure-O7 | An interface not extracted and validated is BLOCKED, not frozen. |
