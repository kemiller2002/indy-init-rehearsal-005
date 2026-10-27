---
id: NR-STORAGE-002
status: accepted
version: 1.0.0
priority: unstated
source: STORAGE-002
---

# NR-STORAGE-002

Derived from baseline requirement `STORAGE-002` (Cross-run compatibility). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| STORAGE-002-O2 | Tests MUST compare canonical state and semantic digests, not merely successful JSON parsing. |

| ID | Statement |
|---|---|
| STORAGE-002-O1 | Every new application build MUST read the same frozen dataset and reconstruct equivalent domain state by replaying authoritative events. |
| STORAGE-002-O3 | A fresh build MUST read/write/reload against the same contract without rewriting seed data. |
