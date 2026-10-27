---
id: NR-UI-023
status: accepted
version: 1.0.0
priority: P0
source: UI-023
---

# NR-UI-023

Derived from baseline requirement `UI-023` (Separate technical integrity from investigative verification). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| UI-023-O1 | A successfully fetched, schema-validated, replayed, or cached investigation MUST NOT be labeled "Verified" unless the domain explicitly asserts the corresponding investigation-level verification state. |
| UI-023-O3 | Define disjoint labels and projection fields for technical data validation versus verified findings. |
| UI-023-O4 | Regression tests MUST catch any conflation, including an investigation that loads correctly but contains disputed or unresolved findings. |

| ID | Statement |
|---|---|
| UI-023-O2 | Data integrity and runtime processing belong in Administration; investigation-facing badges MUST express only domain-authoritative conclusions, evidence status, or lifecycle. |
