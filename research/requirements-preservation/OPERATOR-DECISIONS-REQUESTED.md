# Rehearsal 5: operator decisions requested (T07 → T09)

Status: **requested**. Nothing in this repository is an approval or a review. The derivation tool writes none (RDP-002, RDP-003).

## Derivation

| | |
|---|---|
| Baseline | `BL-CAUSA-INDY-INIT-aab8f80c7743`, Indy-init `aab8f80c7743392e794b3b631542903ed4ed6f64`, sha256 `621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9` |
| Scope | No exclusions: all 461 requirements (108 P0) are `preserved` (Indy-init `kickoff/rehearsal-005/README.md`) |
| Package | `research/requirements/` (461 documents, one per baseline requirement, every obligation carried verbatim as the criterion with its id), `research/requirements-preservation/disposition.json` |
| Tool | `tools/derive-requirements` (F#); inputs and outputs hashed in `derivation-record.json` |
| Gate dry run | `evidence/requirements-preservation/gate-dry-run-no-reviews.json`: exit 3, **blocked**. Findings: 775 `needs-review` (`equivalence-unverified`), nothing else. |

## Request: 775 equivalence reviews

`review-requests.json` lists every obligation without tokens that is shorter than five words. Praxis decides token-less obligations by verbatim carriage only from five words up, so each of these needs `review: { reviewer, date, basis }` from a trusted person.

For every request the tool re-parses the rendered markdown and compares the criterion with the obligation text byte for byte (`byteIdentical`); all 775 are byte-identical, and every obligation text was also found verbatim in its source document at `aab8f80`. The judgement asked is whether verbatim carriage of these short fragments is acceptable.

No approval is requested: Rehearsal 5 excludes nothing.

## Then

Record the reviews outside this repository (Indy-init `kickoff/rehearsal-005/equivalence-reviews.json`), re-run the derivation with `--reviews`, which refuses any review that does not cover a review-requested, byte-identical obligation, and run T08. Exit 0 gives PRE-REQ-04.
