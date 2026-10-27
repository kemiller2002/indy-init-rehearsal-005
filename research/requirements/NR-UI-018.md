---
id: NR-UI-018
status: accepted
version: 1.0.0
priority: P0
source: UI-018
---

# NR-UI-018

Derived from baseline requirement `UI-018` (Dedicated Administration page). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| UI-018-O3 | Distinguish unknown, unavailable, failed, and verified states. |

| ID | Statement |
|---|---|
| UI-018-O1 | Provide a separately navigable Administration page for system information and operational diagnostics. |
| UI-018-O2 | It MUST show, as available and applicable: active provider and catalog, catalog version and source reference, storage/schema contract version, application and Echelon component versions, build/deployed commit, dataset validation and domain replay status, provider connectivity and authentication status without revealing secrets, and relevant diagnostic/error details. |
| UI-018-O4 | Never imply an unverified operation passed. |
| UI-018-O5 | System diagnostics MUST NOT leak into investigation screens. |
| UI-018-O6 | Administration is read-only unless a separately authorized operational action is specified. |
