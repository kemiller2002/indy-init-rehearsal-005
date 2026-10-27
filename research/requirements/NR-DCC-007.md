---
id: NR-DCC-007
status: accepted
version: 1.0.0
priority: unstated
source: DCC-007
---

# NR-DCC-007

Derived from baseline requirement `DCC-007` (Release candidate policy). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| DCC-007-O2 | Batch compatible fixes behind a single integration gate; run targeted F# tests and compilation during implementation, then a full integrated test/acceptance gate for the candidate. |

| ID | Statement |
|---|---|
| DCC-007-O1 | Avoid deploying a release candidate for every patch. |
| DCC-007-O3 | No fixed time delay may prevent a safety-critical or high-risk targeted check. |
| DCC-007-O4 | Do not defer detection of critical architecture defects. |
| DCC-007-O5 | Additional candidates require a documented failed gate, material change, or release rationale. |
