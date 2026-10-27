---
id: NR-DEP-CURRENT-006
status: accepted
version: 1.0.0
priority: unstated
source: DEP-CURRENT-006
---

# NR-DEP-CURRENT-006

Derived from baseline requirement `DEP-CURRENT-006` (Freeze at kickoff, refresh next run). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| DEP-CURRENT-006-O1 | After all applicable verification gates pass, record a complete immutable lock manifest for that run: dependency, selected version, source URL, commit SHA, integrity checksum, discovery timestamp, verification SHA and test evidence. |

| ID | Statement |
|---|---|
| DEP-CURRENT-006-O2 | The run uses exactly this set. |
| DEP-CURRENT-006-O3 | New upstream releases during the run are tracked as drift but do not automatically alter an in-progress experiment. |
| DEP-CURRENT-006-O4 | Repeat discovery and verification for the next run. |
