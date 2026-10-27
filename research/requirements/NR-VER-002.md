---
id: NR-VER-002
status: accepted
version: 1.0.0
priority: P0
source: VER-002
---

# NR-VER-002

Derived from baseline requirement `VER-002` (Verify release freshness). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| VER-002-O1 | Query authoritative package registries, GitHub releases/tags, or the owning repository's documented release source immediately before baseline lock. |
| VER-002-O2 | Record checked timestamp, candidate release, source reference, and why it was selected. |
| VER-002-O3 | Do not equate latest main commit with latest released artifact. |
