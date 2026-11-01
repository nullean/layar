#!/usr/bin/env bash
# Build the documentation as CI should, then serve it: docs-builder, then scripts/docs-finish.sh (theme, root links,
# the landing page over the generated index.html). `docs-builder serve` renders on demand and never applies those, so
# it would preview the wrong home page in the default theme.
#
#   scripts/docs.sh            build and serve on http://localhost:4000
#   scripts/docs.sh --noserve  build only
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

# The sub-path GitHub Pages serves from; empty at the domain root (layar.nullean.net). The landing page's <base href>
# and the workflow's prefix: input must agree with it, and nothing but this check would notice if they drifted.
path_prefix=""
landing="docs/layar-landing.html"
workflow=".github/workflows/docs.yml"
expected_base="/"
[[ -n "$path_prefix" ]] && expected_base="/$path_prefix/"
actual_base="$(grep -o '<base href="[^"]*"' "$landing" | sed 's/<base href="//; s/"$//')"
if [[ "$actual_base" != "$expected_base" ]]; then
  echo "docs: $landing has <base href=\"$actual_base\"> but the site builds for \"$expected_base\"" >&2
  exit 1
fi
if [[ -n "$path_prefix" ]] && ! grep -q "prefix: *$path_prefix\$" "$workflow"; then
  echo "docs: warning: $workflow does not pass prefix: $path_prefix, so the deployed pages will not find their CSS" >&2
fi
if [[ -z "$path_prefix" ]] && grep -q 'prefix:' "$workflow"; then
  echo "docs: $workflow has a prefix: input but path_prefix is empty" >&2
  exit 1
fi

# docs-builder is a native binary, not a dotnet tool; cached under .artifacts. Delete it to pick up a newer release,
# or set DOCS_BUILDER_VERSION to pin one. DOCS_BUILDER=<path> uses that binary instead (a local build or install).
tool="${DOCS_BUILDER:-.artifacts/tools/docs-builder}"
if [[ -z "${DOCS_BUILDER:-}" && ! -x "$tool" ]]; then
  arch="$(uname -m)"; [[ "$arch" == "x86_64" ]] && arch="x64"
  archive="docs-builder-mac-$arch.zip"
  version="${DOCS_BUILDER_VERSION:-latest}"
  if [[ "$version" == "latest" ]]; then
    url="https://github.com/elastic/docs-builder/releases/latest/download/$archive"
  else
    url="https://github.com/elastic/docs-builder/releases/download/$version/$archive"
  fi
  echo "docs: downloading $url"
  mkdir -p .artifacts/tools
  curl -fsSL "$url" -o "/tmp/$archive"
  unzip -o -q -j "/tmp/$archive" docs-builder -d .artifacts/tools
  rm "/tmp/$archive"
  chmod +x "$tool"
fi

# The design-system layout is behind a feature flag, read from the environment only.
export FEATURE_NAVIGATION_PREVIEW=true
if [[ -n "$path_prefix" ]]; then
  "$tool" build --path docs --path-prefix "$path_prefix"
else
  "$tool" build --path docs
fi
out=".artifacts/docs/html"
scripts/docs-finish.sh "$out" "$path_prefix"

[[ "${1:-}" == "--noserve" ]] && exit 0
# Every link carries the prefix, so serve the output under it, as GitHub Pages does.
serve=".artifacts/docs/serve"
rm -rf "$serve" && mkdir -p "$serve"
if [[ -n "$path_prefix" ]]; then
  ln -s "$root/$out" "$serve/$path_prefix"
else
  serve="$out"
fi
echo "docs: serving http://localhost:4000/${path_prefix:+$path_prefix/}"
cd "$serve" && exec python3 -m http.server 4000
