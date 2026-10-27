---
id: NR-AR-019
status: accepted
version: 1.0.0
priority: P0
source: AR-019
---

# NR-AR-019

Derived from baseline requirement `AR-019` (Computed verdict, not agent assertion). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| AR-019-O1 | The authoritative acceptance outcome MUST be computed from the evidence ledger, frozen baseline, required gates, requirements coverage, approved decisions, defect closures, and deployed SHA. |
| AR-019-O2 | Agent-authored accepted fields are advisory at most. |
| AR-019-O3 | Original Rehearsal 3 must yield BLOCKED; Rehearsal 2 candidate 45644a7 should yield ACCEPTED only if its actual ledger and evidence meet the frozen contract. |
| AR-019-O4 | Include tests for false accepted flags, missing CI, baseline tampering, and unauthorized waivers. |
