---
id: NR-ADRP-005
status: accepted
version: 1.0.0
priority: unstated
source: ADRP-005
---

# NR-ADRP-005

Derived from baseline requirement `ADRP-005` (Data and persistence). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| ADRP-005-O5 | Show distinct local and remote persistence status; never claim live GitHub writes from mocked tests. |

| ID | Statement |
|---|---|
| ADRP-005-O1 | Use frozen causa-data/contract/v1.0.0/CONTRACT.md and tag causa-storage-v1.0.0 as the sole wire contract for this profile. |
| ADRP-005-O2 | Seed fixtures are immutable; working copies use separate paths. |
| ADRP-005-O3 | Storage adapters implement the existing provider-neutral interface and optimistic concurrency. |
| ADRP-005-O4 | No agent may improvise alternative field names, ordering, event shapes, or snapshot semantics. |
| ADRP-005-O6 | Changes to frozen contract require explicit versioning and authorization. |
