---
id: NR-ADRP-008
status: accepted
version: 1.0.0
priority: unstated
source: ADRP-008
---

# NR-ADRP-008

Derived from baseline requirement `ADRP-008` (Agent ownership and interfaces). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| ADRP-008-O1 | Assign one owner each for F# domain/policy, UI/WASM, storage/auth adapter, and integration/release evidence. |
| ADRP-008-O2 | The coordinator owns cross-boundary decisions and source-of-truth updates. |
| ADRP-008-O3 | Freeze command/projection/provider interfaces before parallel fan-out. |
| ADRP-008-O4 | Agents may work in parallel on separate files/slices but MUST NOT independently change shared contract files. |
| ADRP-008-O5 | Contract changes require a single owner, explicit version, consumer notification, and targeted compatibility proof. |
