---
id: NR-UI-004
status: accepted
version: 1.0.0
priority: P0
source: UI-004
---

# NR-UI-004

Derived from baseline requirement `UI-004` (Contextual command interface). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| UI-004-O1 | Render only policy-authorized commands returned by the F# engine for the current version. |
| UI-004-O2 | For prohibited actions, show a clear reason when the engine supplies one, without implying the action is available. |
| UI-004-O3 | Capture required command inputs with validated forms and confirmation for destructive or irreversible operations. |
| UI-004-O4 | Reject stale-version submissions, show domain fault messages safely, and avoid duplicate submission. |
| UI-004-O5 | Keyboard invocation and touch targets are required. |
| UI-004-O6 | UI MUST NOT implement a parallel policy engine. |
