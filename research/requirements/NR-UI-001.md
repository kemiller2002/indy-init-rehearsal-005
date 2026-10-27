---
id: NR-UI-001
status: accepted
version: 1.0.0
priority: P0
source: UI-001
---

# NR-UI-001

Derived from baseline requirement `UI-001` (Investigation dashboard). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| UI-001-O1 | Before Connect, show an accessible, empty, disconnected workspace with the explicit Connect data repository action (EXT-DATA-002); after Connect, show the connected repository's investigations and the user's working copies, which for the active demonstration catalog (causa-data historical-v1, EXT-DATA-013) are exactly Apollo 1 (AS-204), Apollo 11 and Space Shuttle Challenger (STS-51-L). |
| UI-001-O2 | Each card/row MUST show title, lifecycle, last recorded activity, and derived counts of hypotheses, evidence, and unresolved contradictions where supported. |
| UI-001-O3 | Show severity and owner only if present in the domain contract; otherwise omit or explicitly label unassigned/unspecified. |
| UI-001-O4 | Show the next legal action only if provided by F# policy. |
| UI-001-O5 | Provide sort, selection, loading, empty and error states. |
| UI-001-O6 | Opening an investigation MUST preserve its identity and context. |
