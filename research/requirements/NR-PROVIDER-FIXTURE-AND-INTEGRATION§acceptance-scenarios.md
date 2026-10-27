---
id: NR-PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios
status: accepted
version: 1.0.0
priority: unstated
source: PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios
---

# NR-PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios

Derived from baseline requirement `PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios` (Acceptance scenarios). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios-O1 | Offline startup: one documented command starts a populated Causa UI with no network or token; screen state and active fixture mode are visible. |
| PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios-O2 | Contract parity: identical valid fixture data yields identical domain reconstruction whether read from fixture or GitHub adapter. |
| PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios-O3 | Failure fidelity: malformed events fail closed; missing branch, 401/403, 404, 422, rate-limit, and timeout are classified correctly. |
| PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios-O4 | Stale-response regression: request B supersedes A; B completes first; late A cannot replace B. |
| PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios-O5 | Reverse completion order also tested. |
| PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios-O6 | Cache/auth regression: changing credentials invalidates relevant cached responses; an unauthorized request cannot be satisfied by stale cached success. |
| PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios-O7 | Visual checks: populated fixtures expose the complete UI-016 palette and labels; contrast and responsive checks pass. |
| PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios-O8 | Real integration: approved GitHub repository and pinned catalog are fetched over the network; all three historical investigation digests match. |
| PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios-O9 | Gate honesty: disconnected integration or missing secret reports BLOCKED, not PASS, while deterministic offline checks remain independently green. |
| PROVIDER-FIXTURE-AND-INTEGRATION§acceptance-scenarios-O10 | Traceability: each PF requirement is mapped to an implementation test and evidence record; no future rehearsal may silently omit an applicable P0 requirement. |
