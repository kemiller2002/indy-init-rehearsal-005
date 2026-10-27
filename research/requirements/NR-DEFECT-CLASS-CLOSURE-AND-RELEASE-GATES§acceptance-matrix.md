---
id: NR-DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix
status: accepted
version: 1.0.0
priority: unstated
source: DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix
---

# NR-DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix

Derived from baseline requirement `DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix` (Acceptance matrix). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O1 | Every critical invariant has an owner and proof obligation before implementation fan-out. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O2 | An independent architecture/invariant review occurs before the first release candidate. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O3 | No critical or recurring defect class is closed by instance-only patching. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O4 | Every such closure has SHA-bound regression, adversarial coverage, and independent approval. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O5 | Verified completion is blocked on unresolved critical classes. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O6 | The exact deployed SHA has a successful acceptance-job conclusion before Accepted is asserted. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O7 | Final report includes release count, time breakdown, recurrence rate, token cost, and correctness/coverage evidence. |
