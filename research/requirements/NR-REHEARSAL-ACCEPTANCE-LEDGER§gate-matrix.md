---
id: NR-REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix
status: accepted
version: 1.0.0
priority: unstated
source: REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix
---

# NR-REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix

Derived from baseline requirement `REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix` (Gate matrix). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O2 | \| G1 Dependencies \| verified package/SHA/integrity lock, SDK, WASM/Forma/Folio smoke \| BLOCKED if unknown \| |
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O3 | \| G2 Interfaces \| frozen versioned handoff manifest and producer/consumer tests \| BLOCKED if not extracted \| |
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O4 | \| G3 Domain \| F# unit tests, illegal-transition and forged-history variants \| FAIL if invalid history accepted \| |
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O9 | \| G8 Candidate \| full applicable browser/integration/security/report tests \| FAIL on missing required suite \| |
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O10 | \| G9 Deployment \| deployed SHA equals verified SHA, successful acceptance job on same SHA \| FAIL/BLOCKED otherwise \| |
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O11 | \| G10 Measurement \| elapsed time, candidates, review recurrence, token and test cost \| BLOCKED if unavailable; do not invent \| |

| ID | Statement |
|---|---|
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O1 | \| G0 Scope \| requirement IDs, owner, profile priority, source precedence resolution \| BLOCKED if mandatory conflict \| |
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O5 | \| G4 Frozen data \| five golden scenarios, hashes, 90 negative fixtures where applicable \| FAIL on schema drift \| |
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O6 | \| G5 UI \| P0 screen contracts, startup/error, iPad/keyboard/a11y, projection fidelity \| FAIL for missing required behavior \| |
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O7 | \| G6 Persistence \| local vs remote truth, expected-version conflict, reload, no fixture mutation \| BLOCKED if live provider untested \| |
| REHEARSAL-ACCEPTANCE-LEDGER§gate-matrix-O8 | \| G7 Defect class \| independent review and SHA-bound closure for critical/recurring classes \| FAIL while open \| |
