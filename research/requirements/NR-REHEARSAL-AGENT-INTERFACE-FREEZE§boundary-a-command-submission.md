---
id: NR-REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission
status: accepted
version: 1.0.0
priority: unstated
source: REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission
---

# NR-REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission

Derived from baseline requirement `REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission` (Boundary A: command submission). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission-O2 | Output MUST distinguish accepted semantic events/new version from typed rejection/fault. |

| ID | Statement |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission-O1 | Input MUST include investigation ID, actor/provenance, typed command, expected aggregate version, and command identity/idempotency context where supported. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission-O3 | UI MUST NOT generate authoritative events directly. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§boundary-a-command-submission-O4 | Domain owner publishes concrete F# signatures and JSON/WASM boundary shape from source. |
