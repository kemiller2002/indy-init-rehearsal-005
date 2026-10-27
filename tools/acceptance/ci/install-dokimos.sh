#!/bin/sh
# Installs the Dokimos release echelon-current 1.29.0 resolves (linux-x64), verified against the SHA-256 recorded in
# the pinned channel resolution, into DIR (default .tools/dokimos) and prints the executable path.
set -eu
dir="${1:-.tools/dokimos}"
channel="acceptance/stack/echelon-current-1.29.0.linux-x64.json"
version=$(jq -r '.components[] | select(.systemId=="dokimos") | .version' "$channel")
tag=$(jq -r '.components[] | select(.systemId=="dokimos") | .tag' "$channel")
sha=$(jq -r '.components[] | select(.systemId=="dokimos") | .artifacts[] | select(.name=="dokimos-linux-x64.tar.gz") | .sha256' "$channel")
mkdir -p "$dir"
curl -fsSL --retry 3 -o "$dir/dokimos.tar.gz" "https://github.com/kemiller2002/dokimos/releases/download/$tag/dokimos-linux-x64.tar.gz"
echo "$sha  $dir/dokimos.tar.gz" | sha256sum -c - >&2
tar -xzf "$dir/dokimos.tar.gz" -C "$dir"
exe=$(find "$dir" -type f -name dokimos | head -n 1)
chmod +x "$exe"
reported=$("$exe" version | jq -r .ReleaseVersion)
[ "$reported" = "$version" ] || { echo "dokimos reports $reported, channel resolves $version" >&2; exit 1; }
echo "$exe"
