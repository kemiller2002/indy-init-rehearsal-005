---
id: NR-STORAGE-007
status: accepted
version: 1.0.0
priority: unstated
source: STORAGE-007
---

# NR-STORAGE-007

Derived from baseline requirement `STORAGE-007` (Release acceptance). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STORAGE-007-O1 | For every R002 and competition candidate: verify schema version/hash; load all valid frozen fixtures; replay histories through F# engine; compare expected projections; perform create/update/reload and conflict tests against disposable demo workspace; confirm seed hash unchanged. |
| STORAGE-007-O2 | Any unexpected schema change, silent rewrite, failed replay, or invalid fixture accepted as valid = FAIL. |
| STORAGE-007-O3 | Missing live API test = BLOCKED/UNKNOWN, never PASS. |
