#!/bin/sh
# PF-012-O1: one command that builds Causa and starts a populated, fixture-backed UI locally (no network, no
# credentials). The test configuration (obj/site-test) carries the Percepta state hook; `?percepta=default` renders
# the fixture investigation through the real F# projection. The deployable site (dist/) never has the hook.
#   tools/acceptance/ci/fixture-ui.sh [PORT]        then open the printed URL
set -eu
port="${1:-4822}"
[ -d node_modules ] || npm ci
npm run build
[ -f obj/site-test/index.html ] || npm run build:test
echo "Causa with the fixture investigation: http://127.0.0.1:$port/index.html?percepta=default"
exec node scripts/serve.mjs obj/site-test "$port"
