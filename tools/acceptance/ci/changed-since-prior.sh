#!/bin/sh
# Union of the paths changed between each prior provenance record's revision and HEAD (for the Dokimos reuse request).
# A revision that is not in the history makes the change set unknowable: a sentinel unmapped path is emitted so that
# every producer runs.
#   changed-since-prior.sh PRIOR.json...
set -eu
for f in "$@"; do
  [ -f "$f" ] || continue
  for rev in $(jq -r '.[].ProducedAtRevision // empty' "$f"); do
    if git cat-file -e "$rev^{commit}" 2>/dev/null; then
      git diff --name-only "$rev" HEAD
    else
      echo "UNKNOWN-PRIOR-REVISION/$rev"
    fi
  done
done | sort -u
