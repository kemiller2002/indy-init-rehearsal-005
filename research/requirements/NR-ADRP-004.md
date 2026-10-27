---
id: NR-ADRP-004
status: accepted
version: 1.0.0
priority: unstated
source: ADRP-004
---

# NR-ADRP-004

Derived from baseline requirement `ADRP-004` (Domain/UI contract). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| ADRP-004-O2 | Commands include expected aggregate version and return accepted event(s) or typed faults. |

| ID | Statement |
|---|---|
| ADRP-004-O1 | UI receives a versioned projection and legal capabilities from the F# engine. |
| ADRP-004-O3 | The UI never computes legality, evidence weight, confidence, or causal truth. |
| ADRP-004-O4 | A successful command refreshes all dependent views from the same projection version. |
| ADRP-004-O5 | Invalid or unsupported required semantics are read-only/blocked. |
| ADRP-004-O6 | Historical and frozen-fixture views are read-only. |
