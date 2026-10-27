# Causa acceptance (computed)

<!-- acceptance-verdict: INCOMPLETE -->

Combined result: NOT ACCEPTED (ledger verifier INCOMPLETE, plan verifier INCOMPLETE).

| Input | Value |
| --- | --- |
| run | `40579006057-1` |
| source SHA | `8d154928196bc95dcc6656d483c4acd13e909b6f` |
| deployed SHA | `none` |
| plan | `VP-e77a2134e1b0` |
| baseline sha256 | `621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9` |
| approvals sha256 | `3e70dcd2e87811c4f3744ee97585855639741c3ba7a63d98f4b842c7e86bace7` |
| evidence manifest sha256 | `b24bd2624613abf8a06052fa8d9301f4faa45bfa2323d7f57b660e9b6f6b975e` |

## Obligations (traceability)

- obligations: 326; bound 3, declared but not run 0, missing 323
- obligation results: PASS 3, FAIL 0, NOT_RUN 323
- P0 requirements: PASS 1, FAIL 0, NOT_RUN 107 (of 108)

## Plan verifier (requirements-compiler verify-plan)

- result INCOMPLETE
- requirements PASS: 0
- requirements FAIL: 0
- requirements BLOCKED: 0
- requirements NOT_RUN: 107
- requirements MISSING: 0
- requirements UNBOUND: 1
- requirements STALE: 0
- requirements INVALID: 0
- requirements EXCLUDED: 0
- obligations NOT_RUN: 324
- obligations UNBOUND: 2

## Stack lock gate (T11)

- gate INCOMPLETE (PASS 42, FAIL 0, GAP 3)
- GAP ordo --version: expected `1.18.0`, actual `unavailable` (not on PATH in this environment)
- GAP percepta --version: expected `0.3.0`, actual `unavailable` (not on PATH in this environment)
- GAP percepta-repo --version: expected `0.1.0`, actual `unavailable` (not on PATH in this environment)

## Ledger verifier reasons (compute-acceptance-verdict.py)

- gate G0: NOT_RUN
- gate G1: NOT_RUN
- gate G2: NOT_RUN
- gate G3: NOT_RUN
- gate G4: NOT_RUN
- gate G5: NOT_RUN
- gate G6: NOT_RUN
- gate G7: NOT_RUN
- gate G8: NOT_RUN
- gate G9: NOT_RUN
- P0 requirement AR-001: NOT_RUN
- P0 requirement AR-002: NOT_RUN
- P0 requirement AR-003: NOT_RUN
- P0 requirement AR-004: NOT_RUN
- P0 requirement AR-005: NOT_RUN
- P0 requirement AR-006: NOT_RUN
- P0 requirement AR-007: NOT_RUN
- P0 requirement AR-008: NOT_RUN
- P0 requirement AR-009: NOT_RUN
- P0 requirement AR-010: NOT_RUN
- P0 requirement AR-011: NOT_RUN
- P0 requirement AR-012: NOT_RUN
- P0 requirement AR-013: NOT_RUN
- P0 requirement AR-014: NOT_RUN
- P0 requirement AR-015: NOT_RUN
- P0 requirement AR-017: NOT_RUN
- P0 requirement AR-018: NOT_RUN
- P0 requirement AR-019: NOT_RUN
- P0 requirement AR-020: NOT_RUN
- P0 requirement AR-021: NOT_RUN
- P0 requirement AR-022: NOT_RUN
- P0 requirement AR-023: NOT_RUN
- P0 requirement AR-024: NOT_RUN
- P0 requirement EXT-DATA-001: NOT_RUN
- P0 requirement EXT-DATA-002: NOT_RUN
- P0 requirement EXT-DATA-003: NOT_RUN
- P0 requirement EXT-DATA-004: NOT_RUN
- P0 requirement EXT-DATA-005: NOT_RUN
- P0 requirement EXT-DATA-006: NOT_RUN
- P0 requirement EXT-DATA-007: NOT_RUN
- P0 requirement EXT-DATA-008: NOT_RUN
- P0 requirement EXT-DATA-009: NOT_RUN
- P0 requirement EXT-DATA-010: NOT_RUN
- P0 requirement EXT-DATA-011: NOT_RUN
- P0 requirement EXT-DATA-012: NOT_RUN
- P0 requirement EXT-DATA-013: NOT_RUN
- P0 requirement PF-001: NOT_RUN
- P0 requirement PF-002: NOT_RUN
- P0 requirement PF-003: NOT_RUN
- P0 requirement PF-004: NOT_RUN
- P0 requirement PF-005: NOT_RUN
- P0 requirement PF-006: NOT_RUN
- P0 requirement PF-007: NOT_RUN
- P0 requirement PF-008: NOT_RUN
- P0 requirement PF-009: NOT_RUN
- P0 requirement PF-011: NOT_RUN
- P0 requirement PF-012: NOT_RUN
- P0 requirement PF-013: NOT_RUN
- P0 requirement PF-014: NOT_RUN
- P0 requirement PF-015: NOT_RUN
- ... and 66 more (see verdict.json)
