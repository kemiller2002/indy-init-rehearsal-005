#!/usr/bin/env bash
# Plan curation, prepared for the requirements owner (acceptance/README.md, "Plan curation"). Applies a run's
# curation-test-bindings.json to a COPY of Indy-init and recompiles the requirements package there. It never
# commits, pushes or touches the given Indy-init clone's working tree: the copy is a detached worktree in WORK.
#
#   INDY_INIT=DIR    Indy-init clone (full history) containing BASE_REF
#   BASE_REF=SHA     Indy-init commit to curate (default: the INDY_INIT_REF pinned in build-and-test.yml)
#   BINDINGS=FILE    curation-test-bindings.json from a run's acceptance-evidence artifact
#   REPOS=DIR        checkouts of every repository listed in requirements-compiled/v1/curation/sources.json
#                    (causa-data praxis ordo dokimos percepta forma limen folio echelon-registry conditor
#                    indy-init-rehearsal-001 indy-init-rehearsal-002 indy-init-rehearsal-003), each containing
#                    its recorded commit
#   WORK=DIR         where the curated copy is created (WORK/indy-init)
#   CA, COMPILER     as in acceptance.sh
# Prints the new plan id. Exit non-zero when a binding is refused or the compilation does not PASS.
set -euo pipefail
: "${INDY_INIT:?}" "${BINDINGS:?}" "${REPOS:?}" "${WORK:?}"
BASE_REF="${BASE_REF:-$(sed -n 's/^  INDY_INIT_REF: *//p' .github/workflows/build-and-test.yml)}"
CA="${CA:-dotnet tools/acceptance/Causa.Acceptance/bin/Release/net10.0/causa-acceptance.dll}"
COMPILER="${COMPILER:-$INDY_INIT/tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll}"
copy="$WORK/indy-init"
cur=requirements-compiled/v1/curation/verification-plan.json
plan=requirements-compiled/v1/verification-plan.json

mkdir -p "$WORK"
[ -e "$copy" ] && { echo "$copy exists; use a fresh WORK" >&2; exit 2; }
git -C "$INDY_INIT" worktree add --detach "$copy" "$BASE_REF" >&2
old=$(jq -r .planIdentity.planId "$copy/$plan")

# 1. testBindings only (every other byte of the curation file is kept); refused whole on any unknown obligation.
$CA apply-curation --curation "$copy/$cur" --plan "$copy/$plan" --bindings "$BINDINGS" --out "$WORK/curation.json" >&2
cp "$WORK/curation.json" "$copy/$cur"

# 2. Recompile (requirements-compiled/v1/README.md "Regenerate"): exit 0 PASS, 3 FAIL, 2 unusable input.
repo_args=()
for r in causa-data praxis ordo dokimos percepta forma limen folio echelon-registry conditor \
  indy-init-rehearsal-001 indy-init-rehearsal-002 indy-init-rehearsal-003; do
  repo_args+=(--repo "$r=$REPOS/$r")
done
(cd "$copy" && dotnet "$COMPILER" --source . --committed-baseline requirements/causa-requirements-baseline.json \
  --curation requirements-compiled/v1/curation --out requirements-compiled/v1 "${repo_args[@]}") > "$WORK/compile.log" 2>&1 \
  || { tail -20 "$WORK/compile.log" >&2; echo "compilation did not PASS" >&2; exit 1; }
# 3. The regenerated package is reproducible from the curated inputs.
(cd "$copy" && dotnet "$COMPILER" --source . --committed-baseline requirements/causa-requirements-baseline.json \
  --curation requirements-compiled/v1/curation --out requirements-compiled/v1 "${repo_args[@]}" --check) > "$WORK/check.log" 2>&1 \
  || { tail -20 "$WORK/check.log" >&2; echo "--check found differences" >&2; exit 1; }

new=$(jq -r .planIdentity.planId "$copy/$plan")
git -C "$copy" diff --stat >&2
jq -r '[.requirements[].testIdentity.status] | group_by(.) | map("\(.[0]) \(length)") | join(", ") | "requirements by test identity: " + .' "$copy/$plan" >&2
echo "plan $old -> $new (curated copy: $copy; nothing committed or pushed)" >&2
echo "$new"
