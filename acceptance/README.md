# Acceptance pipeline (Rehearsal 5)

Acceptance is computed, never declared. Every CI run of `.github/workflows/build-and-test.yml` builds an evidence
bundle and a `causa.acceptance-ledger/2` for its own source SHA, then runs **both** Indy-init verifiers unchanged:
`tools/compute-acceptance-verdict.py` and `requirements-compiler verify-plan`. The report's only verdict line is the
computed one. Accepted means both say ACCEPTED, at the deployed SHA.

## How to tag a test (all streams)

Put the obligation id (`VPO-` + 16 hex, from `acceptance/traceability.json` or
Indy-init `requirements-compiled/v1/verification-plan.json`) on the test that proves it. Nothing else is needed:
the pipeline discovers the results and binds them.

**F# / xunit** (either form works; several ids per test are fine):

```fsharp
[<Fact; Trait("obligation", "VPO-a5795f29e0de763c")>]
let ``a refused save changes nothing`` () = ...

[<Fact>]
let ``VPO-a5795f29e0de763c a refused save changes nothing`` () = ...
```

TRX does not carry traits; the tool reads `Trait("obligation", …)` from the built test assembly's metadata. The
test project must be in `App.slnx` (the core job runs `dotnet test App.slnx`).

**Playwright** (title, tag or annotation):

```js
test("connect loads three cases", { tag: ["@VPO-0123456789abcdef"] }, async ({ page }) => { ... });
test("VPO-0123456789abcdef connect loads three cases", async ({ page }) => { ... });
test("…", { annotation: { type: "obligation", description: "VPO-0123456789abcdef" } }, async () => { ... });
```

**Live tests** (real GitHub, `CAUSA_GITHUB_TOKEN` / `CAUSA_TEST_REPO`) must also carry the tag `@live`. The
browser job runs everything except `@live`; the live job runs only `@live`, on main, with traces off, and never
uploads anything that contains the token.

Rules:

- Tag only what the test really proves. An obligation is PASS only when every test bound to it ran and passed;
  one failing bound test makes it FAIL. A skipped test is not a pass, and CI fails a run with skipped tests.
- A requirement is PASS only when **all** of its obligations are PASS.
- A tag naming an id that is not in the plan fails `causa-acceptance bind` (reported as `unknownTags`).
- Never edit `acceptance/traceability.json` by hand; it is generated.

## Statuses

| Binding | Meaning |
| --- | --- |
| `missing` | no test anywhere names the obligation |
| `declared` | a file under `tests/` names it, but no result in this run contains a tagged test |
| `bound` | an executed result names it |

Gates G0–G9 (`acceptance/gates.json`) are PASS only when every producer they name succeeded in the candidate run
(see "Gate producers" below). G8 and G9 also require the computed `traceability` producer: every
obligation bound and passing. Defect classes (`acceptance/defect-classes.json`) are closed only by the independent
reviewer recording `fixSha` and `reviewerEvidence`.

## Run it locally

```sh
export PATH=$HOME/.local/bin:$HOME/.dotnet:$PATH
dotnet build App.slnx -c Release
dotnet test App.slnx -c Release --no-build --logger "trx;LogFilePrefix=core" --results-directory TestResults/dotnet
PLAYWRIGHT_JSON_OUTPUT_FILE=TestResults/playwright/browser-results.json npx playwright test --grep-invert @live --reporter=list,json
CA="dotnet tools/acceptance/Causa.Acceptance/bin/Release/net10.0/causa-acceptance.dll"
$CA bind --root . --plan ../indy-init/requirements-compiled/v1/verification-plan.json \
  --results TestResults --sources tests --assemblies . --out acceptance/traceability.json
$CA lock-gate --root . --stack acceptance/stack/echelon-current-1.29.0.linux-x64.json --pin acceptance/stack/pin.json \
  --out TestResults/lock-gate/lock-gate.json
# full ledger + both verifiers (needs an Indy-init clone with history):
echo '{"producers":[]}' > /tmp/producers.json
RUN_ID=local SOURCE_SHA=$(git rev-parse HEAD) PRODUCERS=/tmp/producers.json OUT=/tmp/acc INDY_INIT=../indy-init \
  tools/acceptance/ci/acceptance.sh
```

## Pieces

| Path | What |
| --- | --- |
| `tools/acceptance/Causa.Acceptance` | F# tool `causa-acceptance`: `lock-gate`, `bind`, `ledger`, `produce`, `curation`, `apply-curation`, `reuse-request`, `reuse-decision`, `provenance`, `report` |
| `tools/acceptance/ci/acceptance.sh` | computed producers + bundle + ledger + both verifiers + report (pins: plan id/path, baseline, approvals, operator) |
| `tools/acceptance/ci/preservation-gate.sh` | the G0 producer: the recorded preservation gate command, re-run |
| `tools/acceptance/ci/curate-plan.sh` | plan curation on a copy of Indy-init (see "Plan curation") |
| `tools/acceptance/ci/fingerprint.sh` | content fingerprint that lets Echelon foundations / Praxis validation skip an input that already passed |
| `tools/acceptance/ci/install-dokimos.sh`, `install-host-tool.sh` | host tools exactly as the pinned channel resolves them, SHA-256 verified |
| `acceptance/stack/` | T11 pin: byte copy of the echelon-current 1.29.0 linux-x64 channel resolution |
| `acceptance/reuse.json` | Dokimos test-to-input mappings; branches may skip unchanged producers, main never reuses, live never |
| `acceptance/gates.json`, `acceptance/defect-classes.json` | gates, computed producer specs, recurring defect classes |
| `acceptance/traceability.json` | every one of the 326 obligations and its bound tests |
| `evidence/acceptance/` | committed computed verdicts (CI's are in the `acceptance-evidence` artifact of each run) |

Pinned inputs: Indy-init `459f65a` (verifiers, plan `VP-e77a2134e1b0`), baseline sha256 `621e2f96…`, approvals
`kickoff/rehearsal-005/approvals.causa.json` at `295bc64` sha256 `3e70dcd2…`, trusted operator `kemiller2002`.

## Gate producers

A gate (`acceptance/gates.json` `gates`) is PASS only when every producer it `requires` succeeded in the candidate
run. Producers are either reported by a CI job or computed by `causa-acceptance produce` from what the run observed
(`acceptance/gates.json` `producers`; each writes a `causa.acceptance-producer/1` record to
`acceptance-evidence/producers/<id>.json` naming every check and the exact tests it selected).

| Producer | Gate | Kind | Succeeds only when |
| --- | --- | --- | --- |
| `preservation-gate` | G0 | job `preservation-gate` | `praxis requirements preservation verify`, re-run byte for byte as recorded in `evidence/requirements-preservation/gate-pre-implementation-command.txt` (Indy-init 459f65a + aab8f80, Praxis 3.22.0 from the pinned channel), exits 0 with verdict pass. `compare.txt` in its record says whether the result equals the committed one. |
| `lock-gate` | G1, G8, G9 | job `lock-gate` | T11 stack lock gate PASS |
| `wasm-smoke` | G1 | job `wasm-smoke` (required) | the engine compiled to .NET WebAssembly loads Apollo 1, Apollo 11 and Challenger from causa-data (pinned) to their catalog state digests and returns typed faults |
| `stack-smoke` | G1 | computed | browser tests tagged `@stack-smoke-limen`, `@stack-smoke-forma`, `@stack-smoke-folio` each ran and passed, and the T15 five-golden-scenario replay passed |
| `interface-manifest` | G2 | computed | `docs/architecture/interface-manifest.json` is committed with schema `causa.interface-manifest/1` (its SHA-256 is read from the file and recorded in the ledger), and the `InterfaceManifestTests` staleness test ran and passed |
| `core`, `domain-obligations` | G3 | job / computed | plan requirements routed to G3 bound and passing (the plan routes none, and the record says so); the F# engine suite (≥ 20 tests) all passed, with illegal-transition and forged-history variants among them |
| `frozen-data` | G4 | computed | plan requirements routed to G4 bound and passing; the T15 `FixtureReplayTests` (≥ 6: golden scenarios, negative fixtures, locked digests) ran and passed. The ledger records the causa-data lock digest. |
| `browser`, `percepta-verify-app` | G5 | jobs `browser`, `percepta` | `percepta verify-app --base-url <dist/ served>` with `.percepta/bindings.json` exits 0. Not produced while there is no `npm run build` output or no bindings. |
| `live` | G6, G8, G9 | job `live` | the `@live` suite ran and passed on main |
| `defect-class-review` | G7 | computed | every class in `acceptance/defect-classes.json` is closed by the independent reviewer (`status` PASS, `fixSha` an ancestor of the candidate, `reviewerEvidence`) and each failing-first test it lists (`DCT-…` in the test name) ran and passed |
| `traceability` | G8, G9 | computed | every plan obligation bound to executed tests that all passed |
| `deploy`, `deployed-smoke` | G9 | jobs | main push, `DEPLOY_TARGET=github-pages`, the served `version.json` equals the source SHA |

Selection is never inferred: a test counts for a producer only by its identity prefix, a name substring or a tag
listed in `gates.json`. A selected test that failed fails the producer; one that did not run leaves it not produced.

**For the shell stream:** tag the stack smoke browser tests `@stack-smoke-limen` (Limen kernel starts and binds the
engine), `@stack-smoke-forma` (a Forma `ef-*` component renders), `@stack-smoke-folio` (the Folio print page
renders). Percepta runs against `dist/` as built by `npm run build`; if `window.__perceptaSetState` is only in a
test build, make `npm run build` emit that build or say so to the acceptance stream.

## Tests of the pipeline and of this run's evidence

Pipeline obligations (AR-*, VER-*, PF-*, EXT-DATA-001/009/010/013, WDL-015) are proven by real tests of the pipeline,
its tools and this run's evidence, tagged with their VPO ids like any other test:

| Where | What | Runs in |
| --- | --- | --- |
| `tools/acceptance/Causa.Acceptance.Tests/PipelineTests.fs` | the committed workflow and reuse config: credentials scope, layers, triggers, mandatory checks, reuse, fail-closed live | core |
| `tools/acceptance/Causa.Acceptance.RunTests/VerifierTests.fs` | the pinned verifiers over this run's bundle and adversarial copies (false accepted flag, forged rows, missing CI evidence, tampered baseline, self-approved exclusion, narrative report, stale evidence, unpinned plan) | acceptance (before the bundle is built; their TRX joins the run's results) |
| `tools/acceptance/Causa.Acceptance.RunTests/EvidenceTests.fs` | this run's bundle citations, lock-gate record, per-layer results, preservation record, record-free site, R001 immutability | acceptance |
| `tools/acceptance/Causa.Acceptance.RunTests/PreservationTests.fs` | the recorded preservation gate against omitted, negated, weakened and swapped derivations | preservation-adversarial (not required) |
| `tools/acceptance/deployed/deployed.spec.js` | the Pages deployment at the exact SHA: every artifact file byte for byte, start-up without a test hook, screenshots at desktop / iPad portrait / iPad landscape / mobile | deployed-smoke (screenshots in `acceptance-evidence/gate-records/deployed-screenshots`) |

Run the acceptance-job tests locally with the job's inputs: `INDY_INIT`, `CAUSA_DATA_DIR`, `RESULTS` (collected
results), `PRODUCERS`, `SOURCE_SHA`, `RUN_ID`, `GATE_RECORDS`, `SITE_DIR`; the preservation tests with `INDY_INIT`
and `INDY_INIT_SOURCE` (Indy-init at aab8f80) and Praxis on PATH.

## Derivation check (AR-002-O2) and the Praxis finding

`causa-acceptance derivation-check` checks the derived package independently of the Praxis preservation gate (its
parser shares no code with `tools/derive-requirements`): every baseline obligation, through the disposition's
mapping, must be carried as a derived criterion whose text is byte-identical to its text in the pinned baseline
(sha256 `621e2f96…`, Indy-init 459f65a), and every derived criterion must come from a baseline obligation. Any
difference fails with the obligation id, unless the disposition records an `approvedTransformation` whose approval
is in the pinned approvals file (none is recorded). The preservation-gate job runs both checks on the real package
and G0's producer succeeds only when both pass (`gate-records/preservation-gate/derivation-check.json`); the
preservation-adversarial job runs both against omission, negation, weakening, value-swap and role-swap mutations.

**Finding for Praxis (route through its Praxis input after the rehearsal):** `praxis requirements preservation
verify` 3.22.0 passes a derivation whose tokens are all present but swapped: the WCAG ratios of UI-016-O11
exchanged, and the source precedence order of ADRP-001-O1 exchanged. The `FINDING …` tests in
`tools/acceptance/Causa.Acceptance.RunTests/PreservationTests.fs` characterize the gap (they start failing once
Praxis detects swaps); AR-002-O2 is proven by the derivation check, not by Praxis.

## Plan curation (the path to ACCEPTED)

`requirements-compiler verify-plan` reports a requirement PASS only when the **plan** gives every one of its
obligations a test identity (`testIdentity.status: bound`). Indy-init's plan `VP-e77a2134e1b0` binds none, so a
requirement whose tests all pass is `UNBOUND` there and the combined verdict cannot be ACCEPTED. Binding is a
curation of Indy-init `requirements-compiled/v1/curation/verification-plan.json` (`testBindings`) followed by a
recompilation, which yields a **new plan id**. It belongs to the requirements owner; this repository only prepares it.

Every CI run writes `curation-test-bindings.json` into its `acceptance-evidence` artifact: for each baseline
obligation (`AR-004-O2`, …) the exact test identities that were bound to it **and ran in that run** (`live` for the
live suite, else `deterministic`). Unbound obligations are omitted, never guessed.

Procedure (verified locally on 2026-10-27: the unmodified 459f65a package reproduces with `--check`; applying a
run's 41 bindings compiled PASS to `VP-def803ec5237`, and `verify-plan` against it moved those 11 requirements from
UNBOUND to PASS with the same evidence):

```sh
export PATH=$HOME/.local/bin:$HOME/.dotnet:$PATH
dotnet build tools/acceptance/Causa.Acceptance -c Release
(cd ../indy-init && dotnet build tools/requirements-compiler/src -c Release)
# 1. Inputs: the bindings of the candidate run (main, deployed SHA), and a checkout of every repository listed in
#    Indy-init requirements-compiled/v1/curation/sources.json under one directory.
gh run download <run-id> -R kemiller2002/indy-init-rehearsal-005 -n acceptance-evidence -D /tmp/acc-<run-id>
# 2. Apply to a COPY (a detached worktree of the pinned INDY_INIT_REF under WORK) and recompile there:
INDY_INIT=../indy-init BINDINGS=/tmp/acc-<run-id>/curation-test-bindings.json REPOS=/path/to/checkouts \
  WORK=/tmp/curation-<run-id> tools/acceptance/ci/curate-plan.sh          # prints the new plan id
```

`curate-plan.sh` runs `causa-acceptance apply-curation` (replaces only the `testBindings` value, keeping every other
byte of the curation file; refuses the whole set if any binding names an obligation outside the plan, names no test,
an empty id or an unknown kind, or binds an obligation twice), then `requirements-compiler … --out
requirements-compiled/v1` (must PASS) and the same with `--check` (must reproduce). It commits and pushes nothing.

3. The requirements owner reviews the copy's diff (`git -C WORK/indy-init diff`: the curation file, the regenerated
   `verification-plan.json` and `package-manifest.json`), commits it to Indy-init and records the new commit.
4. This repository then changes, in one reviewed commit to `.github/workflows/build-and-test.yml`, `INDY_INIT_REF`
   (the new Indy-init commit), `PLAN_ID` (the printed id) and, if it moved, `PLAN_PATH`. `acceptance.sh` refuses any
   plan whose id differs from `PLAN_ID` or whose id is not its digest's prefix. The baseline, approvals and
   operator pins are unchanged by a curation (the compiler rejects bindings that would change them).

Apply it once the run's traceability binds every obligation it can: each application is a new plan id, and an
obligation the plan leaves unbound stays `UNBOUND`/`NOT_RUN`. Renaming a bound test after curation does not break
`verify-plan` (it checks the plan's binding, not the name), but the ledger's own traceability still requires the
renamed test to carry the VPO tag, so keep tags on tests rather than relying on names.
