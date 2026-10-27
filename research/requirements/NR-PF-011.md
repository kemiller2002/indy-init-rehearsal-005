---
id: NR-PF-011
status: accepted
version: 1.0.0
priority: P0
source: PF-011
---

# NR-PF-011

Derived from baseline requirement `PF-011` (Four independent verification levels). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| PF-011-O1 | Domain: deterministic F# validation, reconstruction, state-transition tests; no external dependencies. |
| PF-011-O2 | UI: browser tests using local fixtures for Forma screens, responsive layouts, keyboard/focus, accessibility, semantic palette, loading and failure states; no external dependencies. |
| PF-011-O3 | Provider conformance: shared contract tests against fixture and GitHub adapters, with sanitized recorded real responses where appropriate. |

| ID | Statement |
|---|---|
| PF-011-O4 | Live integration: real GitHub requests against the authorized Causa data repository, validating catalog discovery, data transfer, reconstruction, expected digests, and errors. |
| PF-011-O5 | Each level must have an independently executable command, CI outcome, and evidence. |
| PF-011-O6 | A higher-level pass must not conceal a lower-level failure. |
