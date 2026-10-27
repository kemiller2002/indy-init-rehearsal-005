---
id: NR-PF-003
status: accepted
version: 1.0.0
priority: P0
source: PF-003
---

# NR-PF-003

Derived from baseline requirement `PF-003` (Explicit provider selection). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| PF-003-O1 | Support an explicit local fixture mode and real GitHub mode, using a documented, repeatable build/run/test command (FAKE or equivalent). |

| ID | Statement |
|---|---|
| PF-003-O2 | Fixture mode must require no network access, GitHub credentials, database, or external service. |
| PF-003-O3 | It must not silently activate in a production deployment. |
| PF-003-O4 | Display the active data mode conspicuously and never imply fixture data is live. |
