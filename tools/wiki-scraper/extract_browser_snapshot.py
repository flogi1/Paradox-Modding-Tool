"""Maintenance helper, part of the wiki-scraper refresh workflow (see README.md): extracts
raw wikitext saved by Claude Code's built-in browser get_page_text tool-result JSON into a
clean wikitext/<Page>.wikitext snapshot file, stripping the tool's own "Title:/URL:/Source
element:/---" header and trailing "Tab Context:" footer.

Usage: python extract_browser_snapshot.py <tool-result.txt> wikitext/<Page>.wikitext
"""
import json
import sys

src, dst = sys.argv[1], sys.argv[2]
with open(src, encoding="utf-8") as f:
    data = json.load(f)
full = "".join(d["text"] for d in data)
idx = full.find("---\n")
body = full[idx + 4:] if idx >= 0 else full
footer_idx = body.rfind("\n\nTab Context:")
if footer_idx >= 0:
    body = body[:footer_idx]
with open(dst, "w", encoding="utf-8") as f:
    f.write(body)
print(f"{len(body)} chars -> {dst}")
