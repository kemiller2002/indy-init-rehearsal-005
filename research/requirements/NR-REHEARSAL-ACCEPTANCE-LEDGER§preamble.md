---
id: NR-REHEARSAL-ACCEPTANCE-LEDGER§preamble
status: accepted
version: 1.0.0
priority: unstated
source: REHEARSAL-ACCEPTANCE-LEDGER§preamble
---

# NR-REHEARSAL-ACCEPTANCE-LEDGER§preamble

Derived from baseline requirement `REHEARSAL-ACCEPTANCE-LEDGER§preamble` (Rehearsal Executable Acceptance Ledger Contract). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O1 | Generate a version-controlled JSON document with: |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O2 | schema: `causa.acceptance-ledger/2` |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O3 | run_id, source_commit_sha, deployed_commit_sha (nullable until deployed), contract_version, frozen_fixture_digest, dependency_lock_sha, interface_manifest_sha |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O4 | requirements_baseline: {id, repository, commit, sha256} |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O5 | approvals_sha256 and evidence_manifest_sha256, supplied by the independent verifier |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O6 | scope_changes[]: {requirement_id, action, approval_id, to when priority changes}; inline approvals are forbidden |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O7 | requirements[]: {id, source_doc, priority, owner, implementation_paths[], tests[], evidence_paths[], status, reason} |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O8 | defect_classes[]: {id, severity, violated_invariant, original_repro, variants[], fix_sha, reviewer_evidence, evidence_paths[], status} |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O9 | gates[]: {id, name, scope, verified_sha, status, evidence_paths[], started_at, completed_at} |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O10 | metrics: {first_green_elapsed_seconds, first_candidate_elapsed_seconds, accepted_elapsed_seconds, candidate_count, repeated_defect_class_count, token_count, ci_queue_seconds, ci_test_seconds, verified_requirement_count} |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O11 | unresolved_decisions[] with owner, impact, and disposition. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O12 | Allowed statuses: NOT_RUN, PASS, FAIL, BLOCKED. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O13 | Never convert missing evidence to PASS. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O14 | No synthetic metrics. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O15 | \| G0 Scope \| requirement IDs, owner, profile priority, source precedence resolution \| BLOCKED if mandatory conflict \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O16 | \| G1 Dependencies \| verified package/SHA/integrity lock, SDK, WASM/Forma/Folio smoke \| BLOCKED if unknown \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O17 | \| G2 Interfaces \| frozen versioned handoff manifest and producer/consumer tests \| BLOCKED if not extracted \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O18 | \| G3 Domain \| F# unit tests, illegal-transition and forged-history variants \| FAIL if invalid history accepted \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O19 | \| G4 Frozen data \| five golden scenarios, hashes, 90 negative fixtures where applicable \| FAIL on schema drift \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O20 | \| G5 UI \| P0 screen contracts, startup/error, iPad/keyboard/a11y, projection fidelity \| FAIL for missing required behavior \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O21 | \| G6 Persistence \| local vs remote truth, expected-version conflict, reload, no fixture mutation \| BLOCKED if live provider untested \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O22 | \| G7 Defect class \| independent review and SHA-bound closure for critical/recurring classes \| FAIL while open \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O23 | \| G8 Candidate \| full applicable browser/integration/security/report tests \| FAIL on missing required suite \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O24 | \| G9 Deployment \| deployed SHA equals verified SHA, successful acceptance job on same SHA \| FAIL/BLOCKED otherwise \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O25 | \| G10 Measurement \| elapsed time, candidates, review recurrence, token and test cost \| BLOCKED if unavailable; do not invent \| |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O26 | Accepted is permitted only if all required gates G0–G9 are PASS for the same verified source identity, no open critical defect class remains, and deployed acceptance is PASS. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O27 | G10 completeness is separately reported and cannot be silently assumed. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O28 | The run may be incomplete, blocked, or failed; partial successes are reported honestly. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O29 | **The verdict is computed, never written** (owner decision OD-INDY-7, [OWNER-DECISIONS-2026-10-24.md](OWNER-DECISIONS-2026-10-24.md)). |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O30 | `tools/compute-acceptance-verdict.py` derives ACCEPTED, INCOMPLETE, BLOCKED, FAILED or INVALID from the ledger and independently supplied trust inputs. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O31 | It requires the candidate repository and trusted source SHA; the exact requirements baseline and its independently supplied SHA-256; a source checkout at the baseline commit; an externally pinned approvals file and SHA-256 plus the trusted operator identities; and a content-addressed evidence manifest, evidence directory and manifest SHA-256. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O32 | It exits 1 when the ledger's `accepted` flag or the report disagrees. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O33 | The report carries exactly one `<!-- acceptance-verdict: VERDICT -->` line, and no Verdict/Outcome line may claim acceptance the computation does not give. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O34 | The verifier opens every baseline source document at the pinned commit. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O35 | It compares every baseline P0 identity with the ledger, so deleting or downgrading an item cannot remove it from acceptance. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O36 | A scope change is effective only when it exactly matches an operator approval in the externally pinned approvals file and that approval's operator identity is named independently with `--trusted-operator-id`. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O37 | Agent-authored approvals in the ledger and untrusted operator identities are invalid. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O38 | Every PASS evidence path names a `causa.acceptance-evidence/1` JSON result in a `causa.acceptance-evidence-manifest/1` bundle. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O39 | The verifier checks the bundle's run and source identities, the bytes and SHA-256 of each result, the result's subject, status, command and exit code, required CI/deployment evidence types, and independent defect review evidence. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O40 | Missing, stale, altered or cross-run evidence is INVALID. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O41 | Digest pinning establishes integrity; authentication of the pins remains the responsibility of the trusted CI artifact, protected reference, Dokimos attestation or independent verifier supplying them. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O42 | A rehearsal's CI runs the structural and independent-verdict regression suites on every push and pull request. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O43 | This repository does not claim that the job is non-bypassable unless repository rules independently require it. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O44 | There is no ledger-local exclusion path: a P0 requirement leaves the verdict only through an externally pinned operator approval bound to the exact baseline. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O45 | Map the normative requirements in REQUIREMENTS.md, CAUSA-UI-EXPERIENCE-REQUIREMENTS.md, WASM-LOADING-EXPERIENCE-REQUIREMENTS.md, FROZEN-DEMO-STORAGE-REQUIREMENTS.md, DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md and AGENT-DECISION-REDUCTION-PROFILE.md. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O46 | Map existing SCREEN-CONTRACTS.md where screens are implemented. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O47 | P1 scope is tracked explicitly; deferred items remain NOT_RUN rather than disappearing. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O48 | Use targeted checks during development; batch compatible fixes into a release candidate. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O49 | Independent architecture review before first candidate. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O50 | Preserve R001 baseline and historical results unchanged. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O51 | Emit one ledger per candidate and one final immutable report with exact source/deployment identities. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O52 | Implement a small validator/generator consuming requirement IDs, test results and gate artifacts. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O53 | It must reject duplicate IDs, unknown statuses, missing proof, mismatched SHAs, and an Accepted flag with incomplete required gates. |
| REHEARSAL-ACCEPTANCE-LEDGER§preamble-O54 | Do not build a new generic workflow platform merely to implement this ledger. |
