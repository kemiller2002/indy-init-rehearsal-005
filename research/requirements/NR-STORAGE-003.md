---
id: NR-STORAGE-003
status: accepted
version: 1.0.0
priority: unstated
source: STORAGE-003
---

# NR-STORAGE-003

Derived from baseline requirement `STORAGE-003` (Schema evolution). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STORAGE-003-O1 | Changes to the frozen contract require an explicit new schema version, documented rationale, reviewed migration, forward/backward compatibility matrix, and test fixtures for both versions. |
| STORAGE-003-O4 | An agent MUST fail the acceptance gate on unapproved schema drift. |

| ID | Statement |
|---|---|
| STORAGE-003-O2 | No in-place migration or destructive rewrite of the frozen baseline. |
| STORAGE-003-O3 | Incompatible change requires explicit authorization. |
