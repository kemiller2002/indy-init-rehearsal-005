---
id: NR-DCC-004
status: accepted
version: 1.0.0
priority: unstated
source: DCC-004
---

# NR-DCC-004

Derived from baseline requirement `DCC-004` (Class-level correction). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| DCC-004-O1 | A critical/recurring defect MUST NOT be closed solely because its reported example stops failing. |
| DCC-004-O2 | The fix MUST prevent the broader class wherever feasible (e.g. replay all event histories through the canonical Decide/Evolve validation path, rather than patching specific forged sequences). |
| DCC-004-O3 | If structural prevention is impossible, document why, enumerate remaining exposure, and require an explicit risk decision. |
