"""Generic, deterministic MediaWiki wikitext table reader (no HTML rendering involved)."""
from __future__ import annotations

import re
from dataclasses import dataclass

_HEADING_RE = re.compile(r"(?m)^(={2,4})\s*(.+?)\s*\1\s*$")
_TABLE_RE = re.compile(r"\{\|.*?\n(.*?)\n\|\}", re.DOTALL)

# MediaWiki cell-attribute syntax: `| attr="v" attr2="v2" | content` uses a lone '|' (not
# '||') to separate HTML-style attributes (e.g. rowspan="2") from the cell's real content.
# Without stripping this, a cell's text is the literal attribute markup glued to the
# content (`rowspan="2" | adm` instead of `adm`) - found on real EU IV wiki rows.
_CELL_ATTR_PREFIX_RE = re.compile(r'^\s*(?:[\w-]+\s*=\s*"[^"]*"\s*)+\|(?!\|)')


@dataclass
class TableSection:
    """One {| ... |} table found in the document, with the active heading text per level
    (2, 3, 4) at that point in document order — e.g. ["Effect scopes", "Country"] for a
    table nested under an H3 inside an H2. Real EU IV wiki pages nest many tables under one
    H2 (organizational H3 subcategories like "Military"/"Navy", or genuinely distinct H3
    scopes like "Country"/"Province") — a table-per-H2-only reader would silently drop all
    but the first table per H2, which is why this walks every heading level, not just H2."""

    heading_path: list[str]
    table_wikitext: str


def sections_with_tables(wikitext: str) -> list[TableSection]:
    """Walks all headings of level 2-4 in document order, tracking the active heading text
    per level. Returns one TableSection per {| ... |} table found anywhere in the document,
    each with its heading_path (only the levels actually seen so far, e.g. just ["Overview"]
    before any H3, or ["Overview", "Dynamic scopes"] once inside one)."""
    path: list[str | None] = [None, None, None]
    headings = [(m.start(), len(m.group(1)), m.group(2).strip()) for m in _HEADING_RE.finditer(wikitext)]
    result: list[TableSection] = []
    hi = 0
    for m in _TABLE_RE.finditer(wikitext):
        tstart = m.start()
        while hi < len(headings) and headings[hi][0] < tstart:
            _, level, text = headings[hi]
            idx = level - 2
            path[idx] = text
            for j in range(idx + 1, 3):
                path[j] = None
            hi += 1
        result.append(TableSection(heading_path=[p for p in path if p is not None], table_wikitext=m.group(1)))
    return result


def split_rows(table_body_wikitext: str) -> list[str]:
    """Splits a table body (without the surrounding {| / |}) into per-row wikitext, one
    entry per '|-'-separated row. Drops a leading header row (starts with '!')."""
    raw_rows = re.split(r"\n\|-\s*\n?", table_body_wikitext)
    rows = [r for r in raw_rows if r.strip() and not r.strip().startswith("!")]
    return rows


def split_cells(row_wikitext: str) -> list[str]:
    """Splits one row's wikitext into cells: newline-leading '|' cells, or '||'-joined
    cells on one line, matching real EU4 wiki table markup (both forms occur)."""
    cells: list[str] = []
    for line in row_wikitext.strip("\n").split("\n"):
        line = line.strip()
        if not line.startswith("|"):
            if cells:
                cells[-1] += "\n" + line
            continue
        for part in line[1:].split("||"):
            cells.append(_CELL_ATTR_PREFIX_RE.sub("", part))
    return cells
