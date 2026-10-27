#!/usr/bin/env bash
# PF-012-O1/O2/O3: one command for the fast offline checks, by layer, with no secrets and no network to the
# provider: domain and provider-contract tests (dotnet test App.slnx: the F# engine, the persistence adapter
# against recorded responses), the site build, and the browser suite against local builds (no @live test).
# Each layer's wall time and outcome are written to TestResults/layer-timings-local.json (measured here; no budget
# is set before a baseline exists). Exit non-zero when any layer failed; every layer runs regardless.
#   CAUSA_DATA_DIR=../causa-data tools/acceptance/ci/offline-checks.sh
set -uo pipefail
export PATH="$HOME/.local/bin:$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
unset CAUSA_GITHUB_TOKEN CAUSA_TEST_REPO
mkdir -p TestResults
record=TestResults/layer-timings-local.json
echo '{"schema":"causa.layer-timings/1","run":"local","budgets":null,"jobs":[]}' > "$record"
failed=0
layer() { # name command...
  local name=$1; shift
  local start end status
  start=$(date +%s)
  "$@"; status=$?
  end=$(date +%s)
  jq --arg n "$name" --argjson s $((end - start)) --arg c "$([ $status -eq 0 ] && echo success || echo failure)" \
    '.jobs += [{name: $n, layer: $n, conclusion: $c, seconds: $s}]' "$record" > "$record.tmp" && mv "$record.tmp" "$record"
  echo "== $name: $([ $status -eq 0 ] && echo PASS || echo FAIL) in $((end - start))s"
  [ $status -eq 0 ] || failed=1
}
specs=$(ls tests/browser/*.spec.js | grep -v '/live-' | tr '\n' ' ')
layer domain-and-provider-contract sh -c 'dotnet build App.slnx -c Release && dotnet test App.slnx -c Release --no-build'
layer build sh -c '[ -d node_modules ] || npm ci; npm run build && npm run build:test'
# shellcheck disable=SC2086
layer ui npx playwright test $specs --grep-invert @live
jq -r '.jobs[] | "\(.layer)\t\(.conclusion)\t\(.seconds)s"' "$record"
exit $failed
