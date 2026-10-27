---
id: NR-UI-015
status: accepted
version: 1.0.0
priority: P0
source: UI-015
---

# NR-UI-015

Derived from baseline requirement `UI-015` (Acceptance and traceability). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| UI-015-O1 | For each UI requirement, create tagged acceptance tests and a traceability map to underlying domain contracts. |
| UI-015-O2 | Verify the three active-catalog investigations (EXT-DATA-013) load from the connected repository and replay, verify the five frozen v1.0.0 fixtures replay as the storage contract's test corpus, keyboard/touch paths, graph textual equivalent, timeline ordering, legal command enforcement, save/sync truthfulness, offline/conflict recovery, responsive reflow, report fidelity, and startup failure. |
| UI-015-O3 | Test on deployed exact SHA; mocked persistence MUST NOT be represented as live write verification. |
| UI-015-O4 | Missing verification is BLOCKED/UNKNOWN, not PASS. |
