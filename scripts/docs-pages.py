"""Patch the HTML pages docs-builder wrote, for scripts/docs-finish.sh.

    python3 -I scripts/docs-pages.py <output dir> <path prefix>

Links to the site root load it whole (hx-boost="false"). The root is the landing page, which has none of the docs
shell, so htmx's boosted swap of #main-container would find nothing to swap and leave the reader on the docs.
Under a path prefix docs-builder writes the root both as /prefix/ (the header brand) and /prefix (the sidebar and the
pager), so both forms are patched.
"""

import re
import sys
from pathlib import Path

out, prefix = Path(sys.argv[1]), sys.argv[2].strip("/")
root = f"/{prefix}/" if prefix else "/"
root_href = re.escape(f"/{prefix}") + "/?" if prefix else "/"

root_link = re.compile(r'<a\b(?![^>]*\bhx-boost=)([^>]*\bhref="' + root_href + r'")')
any_root = re.compile(r'<a\b[^>]*\bhref="' + root_href + r'"')
pages = links = 0
seen_root = False
for page in out.rglob("*.html"):
    html = page.read_text(encoding="utf-8")
    seen_root = seen_root or bool(any_root.search(html))
    new, n_links = root_link.subn(r'<a hx-boost="false"\1', html)
    if new != html:
        page.write_text(new, encoding="utf-8")
        pages += 1
        links += n_links

if not seen_root:
    print(f'docs: no links to "{root}" found; the docs-builder markup changed', file=sys.stderr)
    sys.exit(1)
print(f"docs: {pages} pages patched ({links} root links unboosted)")
