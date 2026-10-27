---
id: NR-VER-004
status: accepted
version: 1.0.0
priority: P0
source: VER-004
---

# NR-VER-004

Derived from baseline requirement `VER-004` (Reproducible lock). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| VER-004-O1 | Pin exact package versions and immutable artifact digests/commit SHAs in a machine-readable dependency manifest. |
| VER-004-O2 | The lock is fixed for a given rehearsal/run and must not drift mid-run. |
| VER-004-O3 | Re-resolve latest versions for each new rehearsal or explicitly authorized refresh. |
