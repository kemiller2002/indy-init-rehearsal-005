#!/bin/sh
# Builds the evidence bundle and causa.acceptance-ledger/2 for one run at one source SHA, then computes the verdict
# with BOTH Indy-init verifiers (never re-implemented here):
#   - python3 tools/compute-acceptance-verdict.py   (gates, defect classes, ledger, evidence bytes)
#   - requirements-compiler verify-plan             (per-obligation test identity and freshness)
# and renders the report, whose only verdict line is re-checked by the ledger verifier for overclaims.
#
# Environment:
#   INDY_INIT       Indy-init checkout with full history (default ../indy-init)
#   COMPILER        requirements-compiler.dll (default $INDY_INIT/tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll)
#   CA              command prefix for causa-acceptance (default: dotnet tools/acceptance/Causa.Acceptance/bin/Release/net10.0/causa-acceptance.dll)
#   PLAN_ID         pinned verification plan id (default VP-e77a2134e1b0); the run refuses any other plan
#   PLAN_PATH       plan path inside INDY_INIT (default requirements-compiled/v1/verification-plan.json)
#   CAUSA_DATA_DIR  causa-data checkout (pinned) whose frozen lock digest the ledger records (optional)
#   RUN_ID SOURCE_SHA [DEPLOYED_SHA] PRODUCERS(file: workflow-reported producers) OUT(dir) RESULTS(dir, default TestResults)
# The computed producers of acceptance/gates.json (`causa-acceptance produce`) are appended to PRODUCERS; their
# records are written to OUT/producers/.
# Exit: 0 when the verdict was computed (whatever it is); non-zero only when it could not be computed honestly.
set -eu
INDY_INIT="${INDY_INIT:-../indy-init}"
COMPILER="${COMPILER:-$INDY_INIT/tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll}"
CA="${CA:-dotnet tools/acceptance/Causa.Acceptance/bin/Release/net10.0/causa-acceptance.dll}"
RESULTS="${RESULTS:-TestResults}"
DEPLOYED_SHA="${DEPLOYED_SHA:-}"
: "${RUN_ID:?}" "${SOURCE_SHA:?}" "${PRODUCERS:?}" "${OUT:?}"

# Pins (acceptance/README.md). Changing any of them is a deliberate, reviewed change. The plan id changes when the
# requirements owner recompiles the plan with new testBindings (acceptance/README.md, "Plan curation"); the
# workflow then pins the new INDY_INIT_REF, PLAN_ID and PLAN_PATH together.
PLAN_ID="${PLAN_ID:-VP-e77a2134e1b0}"
PLAN_PATH="${PLAN_PATH:-requirements-compiled/v1/verification-plan.json}"
BASELINE_SHA256=621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9
APPROVALS_COMMIT=295bc6475a17d54d83a1c9e4f7679fab30e43169
APPROVALS_PATH=kickoff/rehearsal-005/approvals.causa.json
APPROVALS_SHA256=3e70dcd2e87811c4f3744ee97585855639741c3ba7a63d98f4b842c7e86bace7
OPERATOR=kemiller2002

plan="$INDY_INIT/$PLAN_PATH"
baseline="$INDY_INIT/requirements/causa-requirements-baseline.json"
mkdir -p "$OUT/inputs"
actual_plan=$(jq -r .planIdentity.planId "$plan")
[ "$actual_plan" = "$PLAN_ID" ] || { echo "verification plan is $actual_plan, pinned $PLAN_ID" >&2; exit 2; }
# The id is the digest's prefix: a plan whose identity was edited by hand is refused.
plan_digest=$(jq -r .planIdentity.digest "$plan")
[ "VP-$(printf %s "$plan_digest" | cut -c1-12)" = "$PLAN_ID" ] || { echo "plan id $PLAN_ID does not match its digest $plan_digest" >&2; exit 2; }
git -C "$INDY_INIT" show "$APPROVALS_COMMIT:$APPROVALS_PATH" > "$OUT/inputs/approvals.causa.json"
echo "$APPROVALS_SHA256  $OUT/inputs/approvals.causa.json" | sha256sum -c - >&2
echo "$BASELINE_SHA256  $baseline" | sha256sum -c - >&2

# Computed gate producers (interface-manifest, domain-obligations, frozen-data, stack-smoke, defect-class-review).
$CA produce --root . --plan "$plan" --results "$RESULTS" --sources tests --assemblies . \
  --gates acceptance/gates.json --defects acceptance/defect-classes.json --producers "$PRODUCERS" \
  --run-id "$RUN_ID" --source-sha "$SOURCE_SHA" --out "$OUT/producers.json" --records-dir "$OUT/producers" \
  --record-prefix producers/ >&2

deployed_arg=""
[ -n "$DEPLOYED_SHA" ] && deployed_arg="--deployed-sha $DEPLOYED_SHA"
ifm=docs/architecture/interface-manifest.json
ifm_sha="unavailable: no interface manifest in this candidate"
[ -f "$ifm" ] && ifm_sha="$(sha256sum "$ifm" | cut -d' ' -f1) $ifm"
frozen="unavailable: no frozen fixture set in this run"
if [ -n "${CAUSA_DATA_DIR:-}" ] && [ -f "$CAUSA_DATA_DIR/compatibility/v1.0.0/lock.json" ]; then
  frozen="$(sha256sum "$CAUSA_DATA_DIR/compatibility/v1.0.0/lock.json" | cut -d' ' -f1) causa-data@$(git -C "$CAUSA_DATA_DIR" rev-parse HEAD):compatibility/v1.0.0/lock.json"
fi
# shellcheck disable=SC2086
manifest_sha=$($CA ledger --root . --plan "$plan" --results "$RESULTS" --sources tests --assemblies . \
  --gates acceptance/gates.json --defects acceptance/defect-classes.json --producers "$OUT/producers.json" \
  --interface-manifest-sha "$ifm_sha" --frozen-fixture-digest "$frozen" \
  --run-id "$RUN_ID" --source-sha "$SOURCE_SHA" $deployed_arg --out-dir "$OUT/bundle" \
  --command "tools/acceptance/ci/acceptance.sh (run $RUN_ID)" | tail -n 1)
echo "$manifest_sha" > "$OUT/evidence-manifest.sha256"
# Ordo (sde verification schedule): the deterministic schedule over the admitted plan, with every evidence entry's
# bytes checked against the manifest, bound to this run and source SHA. Recorded, never a verdict input.
if command -v ordo > /dev/null 2>&1; then
  plan_abs=$(realpath "$plan")
  set +e
  (cd "$OUT/bundle" && ordo verification schedule --plan "$plan_abs" \
     --expected-digest "$plan_digest" --evidence-manifest evidence-manifest.json --source-sha "$SOURCE_SHA" \
     --run-id "$RUN_ID" --output "$OUT/verification-schedule.json") > "$OUT/verification-schedule.log" 2>&1
  echo "$?" > "$OUT/verification-schedule.exit"
  set -e
else
  echo "ordo is not on PATH; no verification schedule in this run" > "$OUT/verification-schedule.log"
fi
# PF-018-O1: every executed test and fixture set mapped to the requirements it serves and their authoritative source
# (the plan's baseline repository and commit; the fixtures' causa-data commit).
fixture_ids='[]'
if [ -n "${CAUSA_DATA_DIR:-}" ]; then
  fixture_ids=$(jq -c '.sets[]' acceptance/fixtures.json | while read -r set; do
    dir="$CAUSA_DATA_DIR/$(echo "$set" | jq -r .path)"
    ids=$(ls "$dir" 2>/dev/null | jq -R . | jq -s -c .)
    echo "$set" | jq -c --argjson ids "$ids" '. + {ids: $ids}'
  done | jq -s -c .)
fi
jq --arg sha "$SOURCE_SHA" --argjson sets "$fixture_ids" --slurpfile fx acceptance/fixtures.json --slurpfile plan "$plan" '
  {schema: "causa.test-requirement-map/1", sourceSha: $sha,
   requirementsSource: {planId: $plan[0].planIdentity.planId, repository: $plan[0].source.repository, commit: $plan[0].source.commit,
                        baselineSha256: $plan[0].source.baselineSha256},
   fixturesSource: {repository: $fx[0].repository, commit: $fx[0].commit},
   tests: ([.obligations[] | . as $o | .tests[] | {testId, outcome, resultFile, obligation: $o.id, requirement: $o.requirement}]
           | group_by(.testId) | map({testId: .[0].testId, outcome: .[0].outcome, resultFile: .[0].resultFile,
               obligations: (map(.obligation) | unique), requirements: (map(.requirement) | unique)})),
   fixtures: $sets}' "$OUT/bundle/traceability.json" > "$OUT/test-requirement-map.json"
# Input for the requirements owner's plan curation (testBindings); never applied here.
$CA curation --traceability "$OUT/bundle/traceability.json" --out "$OUT/curation-test-bindings.json"

verify() {
  python3 "$INDY_INIT/tools/compute-acceptance-verdict.py" "$OUT/bundle/ledger.json" --repo . \
    --baseline "$baseline" --baseline-sha256 "$BASELINE_SHA256" --baseline-source "$INDY_INIT" \
    --approvals "$OUT/inputs/approvals.causa.json" --approvals-sha256 "$APPROVALS_SHA256" \
    --evidence-manifest "$OUT/bundle/evidence-manifest.json" --evidence-manifest-sha256 "$manifest_sha" \
    --evidence-root "$OUT/bundle" --trusted-source-sha "$SOURCE_SHA" --trusted-operator-id "$OPERATOR" "$@"
}

set +e
verify > "$OUT/verdict.json"; ledger_exit=$?
dotnet "$COMPILER" verify-plan --plan "$plan" --baseline "$baseline" --ledger "$OUT/bundle/ledger.json" \
  --evidence-manifest "$OUT/bundle/evidence-manifest.json" --evidence-manifest-sha256 "$manifest_sha" \
  --evidence-root "$OUT/bundle" --trusted-source-sha "$SOURCE_SHA" --approvals "$OUT/inputs/approvals.causa.json" \
  --trusted-operator-id "$OPERATOR" --out "$OUT/plan-assessment.json" > "$OUT/plan-assessment.log" 2>&1; plan_exit=$?
set -e
case "$ledger_exit" in 0|3) ;; *) echo "compute-acceptance-verdict.py exited $ledger_exit" >&2; cat "$OUT/verdict.json" >&2; exit 1 ;; esac
case "$plan_exit" in 0|3) ;; *) echo "verify-plan exited $plan_exit" >&2; cat "$OUT/plan-assessment.log" >&2; exit 1 ;; esac

lock_arg=""
[ -f "$RESULTS/lock-gate/lock-gate.json" ] && lock_arg="--lock-gate $RESULTS/lock-gate/lock-gate.json"
# shellcheck disable=SC2086
$CA report --verdict "$OUT/verdict.json" --assessment "$OUT/plan-assessment.json" --traceability "$OUT/bundle/traceability.json" \
  $lock_arg --out "$OUT/report.md" \
  --context "run=$RUN_ID" --context "source SHA=$SOURCE_SHA" --context "deployed SHA=${DEPLOYED_SHA:-none}" \
  --context "plan=$PLAN_ID" --context "baseline sha256=$BASELINE_SHA256" --context "approvals sha256=$APPROVALS_SHA256" \
  --context "evidence manifest sha256=$manifest_sha"

# The report may state only the computed verdict (exit 1 = a claim problem).
set +e
verify --report "$OUT/report.md" > "$OUT/report-check.json"; report_exit=$?
set -e
case "$report_exit" in 0|3) ;; *) echo "the report overclaims" >&2; cat "$OUT/report-check.json" >&2; exit 1 ;; esac

echo "ledger verifier: $(jq -r .verdict "$OUT/verdict.json"); plan verifier: $(jq -r .verdict "$OUT/plan-assessment.json")"
