---
id: NR-VER-003
status: accepted
version: 1.0.0
priority: P0
source: VER-003
---

# NR-VER-003

Derived from baseline requirement `VER-003` (Compatibility over blind upgrades). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| VER-003-O1 | Prefer latest stable only when integration contracts and acceptance tests pass. |
| VER-003-O2 | If the newest release is incompatible, fail the upgrade gate or obtain a documented, authorized exception for the newest known compatible release. |
| VER-003-O3 | Never silently fall back to an old version. |
