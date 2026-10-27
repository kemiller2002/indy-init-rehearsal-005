---
id: NR-EXT-DATA-004
status: accepted
version: 1.0.0
priority: P0
source: EXT-DATA-004
---

# NR-EXT-DATA-004

Derived from baseline requirement `EXT-DATA-004` (EXT-DATA-004). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| EXT-DATA-004-O1 | Credentials MUST never be committed, bundled, logged, embedded in URLs, exposed in error text, stored in localStorage, or written into domain event files. |
| EXT-DATA-004-O2 | Prefer short-lived least-privilege tokens or GitHub App authorization. |
| EXT-DATA-004-O3 | In-memory-only credentials require reauthorization after reload. |
| EXT-DATA-004-O4 | Do not use an unrestricted personal access token by default. |
