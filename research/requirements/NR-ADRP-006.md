---
id: NR-ADRP-006
status: accepted
version: 1.0.0
priority: unstated
source: ADRP-006
---

# NR-ADRP-006

Derived from baseline requirement `ADRP-006` (Reuse and dependency decision). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| ADRP-006-O2 | Before EVERY run, discover the newest approved stable releases across all in-scope Echelon systems, resolve and test their compatibility as a set, then pin exact verified versions in the run lock. |

| ID | Statement |
|---|---|
| ADRP-006-O1 | Use existing F#/.NET, Limen, Forma, Folio, Aegis, Praxis, Ordo, Dokimos, and Percepta capabilities when they meet the contract. |
| ADRP-006-O3 | Historical R001 versions are not defaults. |
| ADRP-006-O4 | No automatic downgrade; block and resolve incompatibilities or obtain an explicit time-bounded exception. |
| ADRP-006-O5 | Follow REHEARSAL-DEPENDENCY-LOCK.md. |
| ADRP-006-O6 | Do not add new UI, state-management, graph, or animation dependencies without an explicit integration-cost justification and coordinator approval. |
| ADRP-006-O7 | Where a library lacks required functionality, implement the smallest isolated adapter. |
