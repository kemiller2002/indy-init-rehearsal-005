---
id: RQ-CAUSA-2026-0306
title: "REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate Release acceptance predicate"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:36.549Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0306

Derived from baseline requirement `REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate` (Release acceptance predicate). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O1 | Accepted is permitted only if all required gates G0–G9 are PASS for the same verified source identity, no open critical defect class remains, and deployed acceptance is PASS. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O2 | G10 completeness is separately reported and cannot be silently assumed. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O3 | The run may be incomplete, blocked, or failed; partial successes are reported honestly. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O4 | **The verdict is computed, never written** (owner decision OD-INDY-7, [OWNER-DECISIONS-2026-10-24.md](OWNER-DECISIONS-2026-10-24.md)). |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O5 | `tools/compute-acceptance-verdict.py` derives ACCEPTED, INCOMPLETE, BLOCKED, FAILED or INVALID from the ledger and independently supplied trust inputs. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O6 | It requires the candidate repository and trusted source SHA; the exact requirements baseline and its independently supplied SHA-256; a source checkout at the baseline commit; an externally pinned approvals file and SHA-256 plus the trusted operator identities; and a content-addressed evidence manifest, evidence directory and manifest SHA-256. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O7 | It exits 1 when the ledger's `accepted` flag or the report disagrees. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O8 | The report carries exactly one `<!-- acceptance-verdict: VERDICT -->` line, and no Verdict/Outcome line may claim acceptance the computation does not give. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O9 | The verifier opens every baseline source document at the pinned commit. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O10 | It compares every baseline P0 identity with the ledger, so deleting or downgrading an item cannot remove it from acceptance. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O11 | A scope change is effective only when it exactly matches an operator approval in the externally pinned approvals file and that approval's operator identity is named independently with `--trusted-operator-id`. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O12 | Agent-authored approvals in the ledger and untrusted operator identities are invalid. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O13 | Every PASS evidence path names a `causa.acceptance-evidence/1` JSON result in a `causa.acceptance-evidence-manifest/1` bundle. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O14 | The verifier checks the bundle's run and source identities, the bytes and SHA-256 of each result, the result's subject, status, command and exit code, required CI/deployment evidence types, and independent defect review evidence. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O15 | Missing, stale, altered or cross-run evidence is INVALID. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O16 | Digest pinning establishes integrity; authentication of the pins remains the responsibility of the trusted CI artifact, protected reference, Dokimos attestation or independent verifier supplying them. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O17 | A rehearsal's CI runs the structural and independent-verdict regression suites on every push and pull request. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O18 | This repository does not claim that the job is non-bypassable unless repository rules independently require it. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-acceptance-predicate-O19 | There is no ledger-local exclusion path: a P0 requirement leaves the verdict only through an externally pinned operator approval bound to the exact baseline. |
