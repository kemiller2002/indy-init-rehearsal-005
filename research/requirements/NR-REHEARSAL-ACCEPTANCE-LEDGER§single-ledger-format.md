---
id: NR-REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format
status: accepted
version: 1.0.0
priority: unstated
source: REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format
---

# NR-REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format

Derived from baseline requirement `REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format` (Single ledger format). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O2 | schema: `causa.acceptance-ledger/2` |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O7 | requirements[]: {id, source_doc, priority, owner, implementation_paths[], tests[], evidence_paths[], status, reason} |

| ID | Statement |
|---|---|
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O1 | Generate a version-controlled JSON document with: |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O3 | run_id, source_commit_sha, deployed_commit_sha (nullable until deployed), contract_version, frozen_fixture_digest, dependency_lock_sha, interface_manifest_sha |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O4 | requirements_baseline: {id, repository, commit, sha256} |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O5 | approvals_sha256 and evidence_manifest_sha256, supplied by the independent verifier |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O6 | scope_changes[]: {requirement_id, action, approval_id, to when priority changes}; inline approvals are forbidden |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O8 | defect_classes[]: {id, severity, violated_invariant, original_repro, variants[], fix_sha, reviewer_evidence, evidence_paths[], status} |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O9 | gates[]: {id, name, scope, verified_sha, status, evidence_paths[], started_at, completed_at} |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O10 | metrics: {first_green_elapsed_seconds, first_candidate_elapsed_seconds, accepted_elapsed_seconds, candidate_count, repeated_defect_class_count, token_count, ci_queue_seconds, ci_test_seconds, verified_requirement_count} |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O11 | unresolved_decisions[] with owner, impact, and disposition. |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O12 | Allowed statuses: NOT_RUN, PASS, FAIL, BLOCKED. |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O13 | Never convert missing evidence to PASS. |
| REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format-O14 | No synthetic metrics. |
