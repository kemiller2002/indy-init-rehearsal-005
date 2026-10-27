---
id: NR-DEP-CURRENT-008
status: accepted
version: 1.0.0
priority: unstated
source: DEP-CURRENT-008
---

# NR-DEP-CURRENT-008

Derived from baseline requirement `DEP-CURRENT-008` (Enforcement). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| DEP-CURRENT-008-O1 | Add an automated dependency-discovery job and a compatibility matrix gate before agent fan-out. |
| DEP-CURRENT-008-O2 | Fail CI if actual installed versions differ from the run lock, or if required version, SHA, provenance, or verification evidence is missing. |
| DEP-CURRENT-008-O3 | Store a diff against the previous run so updates are explicit. |
