#!/bin/sh
# Installs one host tool exactly as echelon-current 1.29.0 resolves it for linux-x64 (repository, tag, executable
# artifact), verified against the SHA-256 recorded in the pinned channel copy, into DIR/SYSTEM_ID, and prints the
# directory holding the executable (for $GITHUB_PATH).
#   install-host-tool.sh SYSTEM_ID EXECUTABLE [DIR]
set -eu
system="$1"; exe="$2"; base="${3:-.tools}"
channel="acceptance/stack/echelon-current-1.29.0.linux-x64.json"
q() { jq -r --arg s "$system" ".components[] | select(.systemId==\$s) | $1" "$channel"; }
repo=$(q .repository); tag=$(q .tag)
name=$(q '.artifacts[] | select(.purpose=="executable" and .platform=="linux-x64") | .name')
sha=$(q '.artifacts[] | select(.purpose=="executable" and .platform=="linux-x64") | .sha256')
[ -n "$name" ] && [ -n "$sha" ] || { echo "no linux-x64 executable for $system in $channel" >&2; exit 1; }
dir="$base/$system"
mkdir -p "$dir"
curl -fsSL --retry 3 -o "$dir/$name" "https://github.com/$repo/releases/download/$tag/$name"
echo "$sha  $dir/$name" | sha256sum -c - >&2
case "$name" in
  *.tar.gz) tar -xzf "$dir/$name" -C "$dir" ;;
  *) mv "$dir/$name" "$dir/$exe" ;;
esac
path=$(find "$dir" -type f -name "$exe" | head -n 1)
[ -n "$path" ] || { echo "$exe not found in $name" >&2; exit 1; }
chmod +x "$path"
dirname "$path"
