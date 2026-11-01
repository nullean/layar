#!/usr/bin/env bash
# What both builds (scripts/docs.sh and .github/workflows/docs.yml) do after docs-builder: the layar theme appended to
# the design system's stylesheet, root links patched, and the landing page over the generated index.html.
# Needs a build with FEATURE_NAVIGATION_PREVIEW=true: without it the pages load no ds.css and this fails.
#
#   scripts/docs-finish.sh .artifacts/docs/html [path prefix]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:?usage: docs-finish.sh <docs-builder output dir> [path prefix]}"
prefix="${2:-}"

# The theme goes into the ds.css the pages actually load. Its name comes from docs-builder (plain ds.css with ?v=, or a
# hashed file), so read it from a built page instead of guessing. Before the landing page replaces index.html.
probe="$out/getting-started/index.html"
[[ -f "$probe" ]] || { echo "docs: $probe is missing, cannot find the design-system stylesheet" >&2; exit 1; }
href="$(grep -o 'href="[^"]*/_static/ds[^"/]*\.css[^"]*"' "$probe" | grep -v 'ds-layers' | head -n1 | sed 's/^href="//; s/"$//; s/?.*$//')" || true
if [[ -z "$href" ]]; then
  echo "docs: $probe loads no design-system ds.css; build with FEATURE_NAVIGATION_PREVIEW=true" >&2
  exit 1
fi
css="$out/_static/${href##*/_static/}"
[[ -f "$css" ]] || { echo "docs: $css (from $href) does not exist" >&2; exit 1; }
# An incremental docs-builder build keeps the previous ds.css, theme and all: cut it at the marker before appending.
marker="/* layar-docs.css: appended by scripts/docs-finish.sh */"
awk -v m="$marker" '$0 == m { exit } { print }' "$css" > "$css.tmp"
{ printf '%s\n' "$marker"; cat "$root/docs/layar-docs.css"; } >> "$css.tmp"
mv "$css.tmp" "$css"

# Root links that skip htmx: see scripts/docs-pages.py.
python3 -I "$root/scripts/docs-pages.py" "$out" "$prefix"

cp "$root/docs/layar-landing.html" "$out/index.html"
echo "docs: finished $out (theme appended to ${css#"$out"/}, landing page applied)"
