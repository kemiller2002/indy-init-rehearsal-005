---
id: NR-STORAGE-006
status: accepted
version: 1.0.0
priority: unstated
source: STORAGE-006
---

# NR-STORAGE-006

Derived from baseline requirement `STORAGE-006` (Integrity and concurrency). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STORAGE-006-O2 | Verify append-only semantic history, aggregate-version ordering, duplicate/gap detection, expected-version conflict refusal, and atomic semantic transition persistence. |

| ID | Statement |
|---|---|
| STORAGE-006-O1 | Record fixture manifest and checksums. |
| STORAGE-006-O3 | Snapshots are derived; provider metadata is separate from domain meaning. |
