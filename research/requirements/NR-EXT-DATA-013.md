---
id: NR-EXT-DATA-013
status: accepted
version: 1.0.0
priority: P0
source: EXT-DATA-013
---

# NR-EXT-DATA-013

Derived from baseline requirement `EXT-DATA-013` (EXT-DATA-013). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| EXT-DATA-013-O3 | The five synthetic v1.0.0 fixtures remain the storage contract's test corpus only (STORAGE-005, STORAGE-007). |

| ID | Statement |
|---|---|
| EXT-DATA-013-O1 | The active demonstration catalog is the one named by causa-data `catalog/active.json` (historical-v1): exactly three historical investigations, Apollo 1 (AS-204), Apollo 11 and Space Shuttle Challenger (STS-51-L). |
| EXT-DATA-013-O2 | After an explicit Connect to that repository and workspace, the application MUST load all three through the full contract validation and F# replay pipeline to the states recorded in the catalog manifest, MUST NOT present any synthetic investigation as a catalog case, and MUST NOT bundle any of their records. |
