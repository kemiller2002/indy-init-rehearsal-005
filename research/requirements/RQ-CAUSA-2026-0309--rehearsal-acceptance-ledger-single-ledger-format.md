---
id: RQ-CAUSA-2026-0309
title: "REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format Single ledger format"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-ACCEPTANCE-LEDGER§single-ledger-format"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:37.892Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0309

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
