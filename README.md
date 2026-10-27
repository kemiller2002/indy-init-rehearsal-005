# indy-init-rehearsal-005
## Run it

Two commands, both offline and without credentials:

| What | Command |
| --- | --- |
| Build Causa and start a populated, fixture-backed UI | `tools/acceptance/ci/fixture-ui.sh` (then open `http://127.0.0.1:4322/index.html?percepta=default`) |
| Fast offline checks: domain and provider-contract tests, site build, browser suite without `@live` | `CAUSA_DATA_DIR=../causa-data tools/acceptance/ci/offline-checks.sh` |

`offline-checks.sh` writes each layer's measured time and outcome to `TestResults/layer-timings-local.json`; every CI
run records the same per job in its `acceptance-evidence` artifact (`gate-records/layer-timings.json`). No time budget
is set until a baseline of measured runs exists. The live provider, provider-conformance and deployed checks run only
in CI on main (they need `CAUSA_GITHUB_TOKEN` / `CAUSA_TEST_REPO` or the deployment). Prerequisites: .NET SDK 10,
Node 22, a causa-data checkout, Chromium for Playwright. The acceptance pipeline is described in
[acceptance/README.md](acceptance/README.md).
