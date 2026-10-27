#!/usr/bin/env bash
# G0 producer: re-runs the requirements preservation gate exactly as recorded in
# evidence/requirements-preservation/gate-pre-implementation-command.txt (the `praxis requirements preservation
# verify` invocation in it, byte for byte, with only --output redirected to OUT_DIR), against the same inputs:
#   <indy-init>          Indy-init at INDY_INIT_REF (459f65a)        -> symlink to $INDY_INIT
#   <indy-init@aab8f80>  Indy-init at aab8f80 (the baseline source)   -> symlink to $INDY_INIT_SOURCE
# The recorded command uses these literal relative paths, so they are created (as symlinks) in the repository root
# for the run and removed afterwards; the result records the same paths as the committed evidence.
#
#   INDY_INIT=DIR INDY_INIT_SOURCE=DIR OUT_DIR=DIR preservation-gate.sh
# Writes OUT_DIR/gate.json (praxis.requirements-preservation/1), OUT_DIR/command.txt, OUT_DIR/status
# (success | failure) and OUT_DIR/compare.txt (differences from the committed result, informational).
# Exit 0 only when praxis exits 0 with verdict pass.
set -eu
: "${INDY_INIT:?}" "${INDY_INIT_SOURCE:?}" "${OUT_DIR:?}"
recorded=evidence/requirements-preservation/gate-pre-implementation-command.txt
committed=evidence/requirements-preservation/gate-pre-implementation.json
mkdir -p "$OUT_DIR"
out_abs=$(cd "$OUT_DIR" && pwd)/gate.json

# The recorded invocation: from its first line to the line ending in --json.
awk '/^praxis requirements preservation verify/ { on = 1 } on { print } on && /--json[[:space:]]*$/ { exit }' "$recorded" > "$OUT_DIR/recorded-command.txt"
grep -q -- '--output evidence/requirements-preservation/gate-pre-implementation.json --json' "$OUT_DIR/recorded-command.txt" \
  || { echo "recorded command not found in $recorded" >&2; exit 2; }
sed "s|--output evidence/requirements-preservation/gate-pre-implementation.json|--output '$out_abs'|" \
  "$OUT_DIR/recorded-command.txt" > "$OUT_DIR/command.txt"

for link in '<indy-init>' '<indy-init@aab8f80>'; do
  [ -e "$link" ] && { echo "$link already exists in the repository root" >&2; exit 2; }
done
ln -s "$INDY_INIT" '<indy-init>'
ln -s "$INDY_INIT_SOURCE" '<indy-init@aab8f80>'
praxis --version > "$OUT_DIR/praxis-version.txt" 2>&1 || true
set +e
sh "$OUT_DIR/command.txt" > "$OUT_DIR/stdout.txt" 2> "$OUT_DIR/stderr.txt"
status=$?
set -e
rm -f '<indy-init>' '<indy-init@aab8f80>'
echo "$status" > "$OUT_DIR/exit-code"

verdict=$(jq -r .verdict "$out_abs" 2>/dev/null || echo none)
if [ "$status" -eq 0 ] && [ "$verdict" = pass ]; then result=success; else result=failure; fi
echo "$result" > "$OUT_DIR/status"
# Informational: the re-run against the committed result (timestamps aside).
diff <(jq -S 'del(.generatedAt)' "$committed") <(jq -S 'del(.generatedAt)' "$out_abs") > "$OUT_DIR/compare.txt" 2>&1 \
  && echo "identical to $committed" > "$OUT_DIR/compare.txt" || true
echo "preservation gate: exit $status, verdict $verdict, $(jq -c .summary "$out_abs" 2>/dev/null) -> $result"
[ "$result" = success ]
