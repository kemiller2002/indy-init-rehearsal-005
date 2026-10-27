#!/bin/sh
# Content fingerprint of the tracked files a check reads, so a check whose exact inputs already passed is not re-run.
#   tools/acceptance/ci/fingerprint.sh [--salt TEXT]... [--exclude PATHSPEC-GLOB]...
# The fingerprint is the SHA-256 of "<mode> <blob> <path>" for every tracked file not excluded, plus the salts
# (e.g. the pinned verifier version). Git blob ids are content hashes, so equal fingerprints mean byte-identical
# inputs. Output: one lowercase hex line. (Reimplemented for R5 from the R4 remediation pattern.)
set -eu
salts=""
excludes=""
while [ $# -gt 0 ]; do
  case "$1" in
    --salt) salts="$salts
salt $2"; shift 2 ;;
    --exclude) excludes="$excludes
:(exclude,glob)$2"; shift 2 ;;
    *) echo "fingerprint.sh: unknown argument $1" >&2; exit 2 ;;
  esac
done
{
  printf '%s\n' "$salts"
  printf '%s\n' "$excludes" | sed '/^$/d' | tr '\n' '\0' | xargs -0 git ls-files -s -- . | sed 's/ [0-9]	/ /'
} | sha256sum | cut -d' ' -f1
