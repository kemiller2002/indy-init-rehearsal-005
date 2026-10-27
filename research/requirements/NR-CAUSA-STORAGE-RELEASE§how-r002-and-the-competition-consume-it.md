---
id: NR-CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it
status: accepted
version: 1.0.0
priority: unstated
source: CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it
---

# NR-CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it

Derived from baseline requirement `CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it` (How R002 and the competition consume it). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O1 | Pin the tag `causa-storage-v1.0.0` and check that the three identities above match. |
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O2 | Any mismatch is a FAIL |
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O3 | (STORAGE-007). |
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O4 | Run `tools/fetch-engine.sh`, then `causa gate . --anchor causa-storage-v1.0.0` in causa-data. |
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O5 | Write only under `workspaces/demo/<runId>/`, never under `fixtures/`. |
| CAUSA-STORAGE-RELEASE§how-r002-and-the-competition-consume-it-O6 | The provider token stays in the local store. |
