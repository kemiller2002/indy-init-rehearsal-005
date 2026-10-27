---
id: NR-ADRP-010
status: accepted
version: 1.0.0
priority: unstated
source: ADRP-010
---

# NR-ADRP-010

Derived from baseline requirement `ADRP-010` (Test cadence and release batching). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| ADRP-010-O2 | Integration/browser/accessibility tests may be batched to reduce queue overhead but cannot be suppressed when needed to validate a risky boundary. |
| ADRP-010-O5 | A release is Accepted only when the deployed exact SHA has a successful deployed acceptance job. |

| ID | Statement |
|---|---|
| ADRP-010-O1 | F# compile/unit and targeted invariant checks run during implementation. |
| ADRP-010-O3 | First full candidate runs the complete applicable matrix. |
| ADRP-010-O4 | Avoid repeated deployments for individual fixes; batch related changes. |
| ADRP-010-O6 | Never claim elapsed-time savings until measured. |
