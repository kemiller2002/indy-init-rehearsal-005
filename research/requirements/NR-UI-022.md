---
id: NR-UI-022
status: accepted
version: 1.0.0
priority: P0
source: UI-022
---

# NR-UI-022

Derived from baseline requirement `UI-022` (Minimal application chrome and investigation cards). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| UI-022-O1 | The global header MUST emphasize application identity and top-level navigation, not data-provider commit hashes or connection implementation. |
| UI-022-O2 | The footer MUST omit build SHAs, component-stack inventories, and build timestamps; these belong on Administration (UI-018). |
| UI-022-O3 | Investigation catalog cards MUST prioritize investigation title, actual domain lifecycle/status, latest meaningful activity, supported counts of open questions/evidence, and a clear Open Investigation action. |
| UI-022-O4 | Do not show raw internal case keys as primary headings, "Expected lifecycle", duplicated declared/derived counts, or technical replay/verification explanations. |
| UI-022-O5 | Internal identifiers remain discoverable where required for legitimate reference, provenance, and diagnostics. |
| UI-022-O6 | Unknown counts MUST NOT be presented as zero. |
