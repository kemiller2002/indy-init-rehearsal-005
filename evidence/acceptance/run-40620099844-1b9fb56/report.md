# Causa acceptance (computed)

<!-- acceptance-verdict: INCOMPLETE -->

Combined result: NOT ACCEPTED (ledger verifier INCOMPLETE, plan verifier INCOMPLETE).

| Input | Value |
| --- | --- |
| run | `40620099844-1` |
| source SHA | `1b9fb56873fe7a98dffd5172066d909449c17eae` |
| deployed SHA | `1b9fb56873fe7a98dffd5172066d909449c17eae` |
| plan | `VP-e77a2134e1b0` |
| baseline sha256 | `621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9` |
| approvals sha256 | `3e70dcd2e87811c4f3744ee97585855639741c3ba7a63d98f4b842c7e86bace7` |
| evidence manifest sha256 | `78a763b9c401984c7b5779483949d2dcf2552f3166fde52d22e59abffce0f0ee` |

## Obligations (traceability)

- obligations: 326; bound 277, declared but not run 0, missing 49
- obligation results: PASS 277, FAIL 0, NOT_RUN 49
- P0 requirements: PASS 77, FAIL 0, NOT_RUN 31 (of 108)

## Plan verifier (requirements-compiler verify-plan)

- result INCOMPLETE
- requirements PASS: 0
- requirements FAIL: 0
- requirements BLOCKED: 0
- requirements NOT_RUN: 31
- requirements MISSING: 0
- requirements UNBOUND: 77
- requirements STALE: 0
- requirements INVALID: 0
- requirements EXCLUDED: 0
- obligations NOT_RUN: 102
- obligations UNBOUND: 224

## Stack lock gate (T11)

- gate PASS (PASS 55, FAIL 0, GAP 0)

## Ledger verifier reasons (compute-acceptance-verdict.py)

- gate G8: NOT_RUN
- gate G9: NOT_RUN
- P0 requirement AR-005: NOT_RUN
- P0 requirement AR-008: NOT_RUN
- P0 requirement AR-011: NOT_RUN
- P0 requirement AR-012: NOT_RUN
- P0 requirement AR-015: NOT_RUN
- P0 requirement AR-017: NOT_RUN
- P0 requirement AR-019: NOT_RUN
- P0 requirement AR-020: NOT_RUN
- P0 requirement AR-021: NOT_RUN
- P0 requirement AR-023: NOT_RUN
- P0 requirement AR-024: NOT_RUN
- P0 requirement EXT-DATA-005: NOT_RUN
- P0 requirement EXT-DATA-006: NOT_RUN
- P0 requirement PF-005: NOT_RUN
- P0 requirement PF-009: NOT_RUN
- P0 requirement PF-014: NOT_RUN
- P0 requirement PF-015: NOT_RUN
- P0 requirement PF-019: NOT_RUN
- P0 requirement UI-011: NOT_RUN
- P0 requirement UI-015: NOT_RUN
- P0 requirement UI-016: NOT_RUN
- P0 requirement UI-021: NOT_RUN
- P0 requirement UI-030: NOT_RUN
- P0 requirement UI-031: NOT_RUN
- P0 requirement UI-040: NOT_RUN
- P0 requirement VER-001: NOT_RUN
- P0 requirement VER-002: NOT_RUN
- P0 requirement VER-003: NOT_RUN
- P0 requirement VER-004: NOT_RUN
- P0 requirement VER-010: NOT_RUN
- P0 requirement WDL-014: NOT_RUN
