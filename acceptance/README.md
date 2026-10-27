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

Gates G0–G9 (`acceptance/gates.json`) are PASS only when every producer they name succeeded in the candidate run;
a gate with no producer wired stays NOT_RUN. G8 and G9 also require the computed `traceability` producer: every
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
| `tools/acceptance/Causa.Acceptance` | F# tool `causa-acceptance`: `lock-gate`, `bind`, `ledger`, `reuse-request`, `reuse-decision`, `provenance`, `report` |
| `tools/acceptance/ci/acceptance.sh` | bundle + ledger + both verifiers + report (pins: plan, baseline, approvals, operator) |
| `tools/acceptance/ci/fingerprint.sh` | content fingerprint that lets Echelon foundations / Praxis validation skip an input that already passed |
| `tools/acceptance/ci/install-dokimos.sh` | Dokimos 0.8.0 from the pinned channel, SHA-256 verified |
| `acceptance/stack/` | T11 pin: byte copy of the echelon-current 1.29.0 linux-x64 channel resolution |
| `acceptance/reuse.json` | Dokimos test-to-input mappings; branches may skip unchanged producers, main never reuses, live never |
| `acceptance/gates.json`, `acceptance/defect-classes.json` | gate producers, recurring defect classes |
| `acceptance/traceability.json` | every one of the 326 obligations and its bound tests |
| `evidence/acceptance/` | committed computed verdicts (CI's are in the `acceptance-evidence` artifact of each run) |

Pinned inputs: Indy-init `459f65a` (verifiers, plan `VP-e77a2134e1b0`), baseline sha256 `621e2f96…`, approvals
`kickoff/rehearsal-005/approvals.causa.json` at `295bc64` sha256 `3e70dcd2…`, trusted operator `kemiller2002`.

Known limit: `verify-plan` reports a requirement PASS only when the **plan** binds a test identity to every
obligation (Indy-init `curation/verification-plan.json` `testBindings`, then recompilation: a new plan id). Until
that curation is done from `acceptance/traceability.json`, a requirement whose tests all pass is `UNBOUND` there,
and the combined result cannot be ACCEPTED.
