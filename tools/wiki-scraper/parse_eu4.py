"""Page-specific parsers: one function per EU IV wiki reference page schema (see
docs/superpowers/specs/2026-09-22-wiki-scriptdocs-eu4-design.md for the mapping table, and
the implementation plan's Task 5 ledger for how the real page structure - discovered only
once the live wikitext could actually be fetched via a browser - differs from that spec's
first-pass assumptions: each page nests many tables under one H2 via H3 subheadings, so the
parsers below key off the FULL heading_path from mediawiki_table.sections_with_tables, not
just the first table under an H2)."""
from __future__ import annotations

import re

from mediawiki_table import sections_with_tables, split_rows, split_cells


def _strip_markup(text: str) -> str:
    text = re.sub(r"<br\s*/?>", "\n", text)
    text = re.sub(r"<[^>]+>", "", text)
    text = re.sub(r"'''?(.*?)'''?", r"\1", text)
    return text.strip()


def _cell(cells: list[str], i: int) -> str:
    return cells[i].strip() if len(cells) > i else ""


# Placeholder text wiki editors leave when a scope hasn't been documented yet (found on the
# Triggers page's "Scope" column, e.g. ''Scope needed'') - means "unknown", not a real scope.
_UNKNOWN_SCOPE_MARKERS = ("scope needed", "scope needed'")


def _clean_scope_list(text: str) -> list[str]:
    """Turns a raw scope/ROOT-style cell into a list of real scope names: strips wiki
    italics/HTML markup and a stray leading '|' (from a long ||-separated row line-wrapped
    across a bulleted <br> continuation - see the "messy_stray_pipe" test case), then splits
    on <br>/comma/newline for cells that list more than one applicable scope. A placeholder
    like ''Scope needed'' or an empty/dash cell yields no scopes (AnyScope downstream)."""
    cleaned = _strip_markup(text).lstrip("|").strip()
    if not cleaned or cleaned.lower() in ("anywhere", "-", "—") or cleaned.lower() in _UNKNOWN_SCOPE_MARKERS:
        return []
    parts = re.split(r"[\n,]", cleaned)
    return [p.strip() for p in parts if p.strip()]


# ---- Effects -----------------------------------------------------------------------------
# H2 sections: "Dual scope" (works in both country/province - AnyScope, not a literal "dual"
# scope), "Country scope", "Province scope" (real, usable scopes), "Flow control" (if/while/
# scripted-effects syntax, not scope-tagged effects), "Removed effects" (deprecated). H3
# subsections (General/Economy/Trade/...) are purely organizational and do not affect scope.
#
# The heading text itself ("Country scope") is NOT the scope value shipped: the engine's
# tracked scope vocabulary comes from Triggers' Scope column and the Scopes page's
# "Changes Scope To" targets, both bare "Country"/"Province" - mapping to the heading text
# verbatim would ship two disjoint, never-matching vocabularies for the same real scope.
_EFFECTS_SCOPE_HEADINGS = {"country scope": "Country", "province scope": "Province"}
_EFFECTS_ANYSCOPE_HEADINGS = {"dual scope"}


def parse_effects(wikitext: str) -> list[dict]:
    result = []
    for section in sections_with_tables(wikitext):
        if not section.heading_path:
            continue
        h2 = section.heading_path[0].lower()
        if h2 in _EFFECTS_ANYSCOPE_HEADINGS:
            scopes: list[str] = []
        elif h2 in _EFFECTS_SCOPE_HEADINGS:
            scopes = [_EFFECTS_SCOPE_HEADINGS[h2]]
        else:
            continue

        for row in split_rows(section.table_wikitext):
            cells = split_cells(row)
            if len(cells) < 4:
                continue
            name = _cell(cells, 0)
            description = _cell(cells, 3)
            notes = _strip_markup(_cell(cells, 4))
            version = _cell(cells, 5)
            if not name:
                continue
            extra = {}
            if notes:
                extra["Notes"] = notes
            if version:
                extra["Version Added"] = version
            result.append({
                "kind": "Effect", "name": name, "description": _strip_markup(description),
                "scopes": scopes, "targets": [], "extra": extra,
            })
    return result


# ---- Triggers ------------------------------------------------------------------------------
# Only two of the page's tables carry real trigger names with a usable scope: the big
# "List of triggers" table (H3 under "Simple triggers", columns Value/Type/Description/
# Scope/Example) and the small "Operators" table (H2 itself, AND/OR/NOT - AnyScope, same
# concept as the Scopes page's Logic scopes, deduplicated against it on import). The
# "Base scopes" table (ROOT/FROM/PREV/emperor/event_target:<x>, all "anywhere") is skipped:
# it overlaps the Scopes page's "Dynamic scopes" table without adding new information.
def parse_triggers(wikitext: str) -> list[dict]:
    result = []
    for section in sections_with_tables(wikitext):
        if not section.heading_path:
            continue
        last = section.heading_path[-1].lower()
        if last == "list of triggers":
            for row in split_rows(section.table_wikitext):
                cells = split_cells(row)
                if len(cells) < 5:
                    continue
                name, type_, description, scope = _cell(cells, 0), _cell(cells, 1), _cell(cells, 2), _cell(cells, 3)
                if not name:
                    continue
                scopes = _clean_scope_list(scope)
                result.append({
                    "kind": "Trigger", "name": name, "description": _strip_markup(description),
                    "scopes": scopes, "targets": [], "extra": {"Type": type_} if type_ else {},
                })
        elif last == "operators":
            for row in split_rows(section.table_wikitext):
                cells = split_cells(row)
                if len(cells) < 2:
                    continue
                name = _cell(cells, 0)
                description = _cell(cells, 1)
                if not name:
                    continue
                result.append({
                    "kind": "Trigger", "name": name, "description": _strip_markup(description),
                    "scopes": [], "targets": [], "extra": {},
                })
    return result


# ---- Modifier_list -------------------------------------------------------------------------
# H2 sections: "Overview" (no table), "Country modifiers", "Province modifiers" (the real,
# well-structured data - H3 subsections like Military/Navy are purely organizational),
# "Unique modifiers" (different, smaller column schema for event-modifier-only special
# cases - niche, skipped for now), "Removed modifiers" (deprecated, skipped).
#
# Same canonical-vocabulary mapping as Effects (see _EFFECTS_SCOPE_HEADINGS): ship bare
# "Country"/"Province", not the heading text "Country modifiers"/"Province modifiers".
_MODIFIER_SCOPE_HEADINGS = {"country modifiers": "Country", "province modifiers": "Province"}


def parse_modifiers(wikitext: str) -> list[dict]:
    result = []
    for section in sections_with_tables(wikitext):
        if not section.heading_path:
            continue
        h2 = section.heading_path[0].lower()
        if h2 not in _MODIFIER_SCOPE_HEADINGS:
            continue
        scopes = [_MODIFIER_SCOPE_HEADINGS[h2]]
        for row in split_rows(section.table_wikitext):
            cells = split_cells(row)
            if len(cells) < 3:
                continue
            name = _cell(cells, 0)
            description = _cell(cells, 2)
            effect_type = _cell(cells, 3)
            if not name:
                continue
            result.append({
                "kind": "Modifier", "name": name, "description": _strip_markup(description),
                "scopes": scopes, "targets": [],
                "extra": {"Effect type": effect_type} if effect_type else {},
            })
    return result


# ---- Scopes page: table entries (iterators/scope-links) -----------------------------------
# This page's structure is genuinely different from Effects/Modifier_list: H3 headings here
# ARE real scope distinctions (Country/Province/Tradenode/Unit/Missions/Trade node), not
# organizational fluff, so both H2 and H3 matter:
#   - "Overview" > "Dynamic scopes": ROOT/FROM/PREV/THIS, a 4-column table (Scope/
#     Description/Example/Notes - different schema from the 6-column tables below), usable
#     in any scope -> AnyScope.
#   - "Dual scopes" > <H3>: usable in both effect and trigger context. "Country and
#     Province" H3 is itself a mixed bucket -> AnyScope; the other H3s (Country/Province/
#     Trade node) map to that literal scope name.
#   - "Effect scopes" > <H3>: Kind=Effect, scope = H3 text.
#   - "Trigger scopes" > <H3>: Kind=Trigger, scope = H3 text.
#   - Anything else (the "Subscopes" prefix-reference table, "Removed scopes" and its
#     "Missions" sub-table) is intentionally skipped: reference/meta content or deprecated.
def parse_scopes_table(wikitext: str) -> list[dict]:
    result = []
    for section in sections_with_tables(wikitext):
        if not section.heading_path:
            continue
        h2 = section.heading_path[0].lower()
        h3 = section.heading_path[1].lower() if len(section.heading_path) > 1 else None

        if h2 == "overview" and h3 == "dynamic scopes":
            for row in split_rows(section.table_wikitext):
                cells = split_cells(row)
                if len(cells) < 2:
                    continue
                name, description = _cell(cells, 0), _cell(cells, 1)
                if not name:
                    continue
                result.append({
                    "kind": "Effect", "name": name, "description": _strip_markup(description),
                    "scopes": [], "targets": [], "extra": {},
                })
            continue

        if h2 == "dual scopes" and h3:
            kind = "Effect"
            scopes: list[str] = [] if h3 == "country and province" else [section.heading_path[1]]
        elif h2 == "effect scopes" and h3:
            kind = "Effect"
            scopes = [section.heading_path[1]]
        elif h2 == "trigger scopes" and h3:
            kind = "Trigger"
            scopes = [section.heading_path[1]]
        else:
            continue

        for row in split_rows(section.table_wikitext):
            cells = split_cells(row)
            if len(cells) < 3:
                continue
            name = _cell(cells, 0)
            if not name:
                continue
            description = _cell(cells, 2)
            changes_to = _cell(cells, 4)
            targets = [] if changes_to.lower() in ("-", "—", "") else [changes_to]
            result.append({
                "kind": kind, "name": name, "description": _strip_markup(description),
                "scopes": scopes, "targets": targets, "extra": {},
            })
    return result


# ---- Scopes page: logic operators (not a table row) ----------------------------------------
_LOGIC_RE = re.compile(r"====\s*(AND|OR|NOT|NOR)\s*====\s*\n(.*?)(?=\n====|\Z)", re.DOTALL)


def parse_scopes_logic(wikitext: str) -> list[dict]:
    result = []
    for m in _LOGIC_RE.finditer(wikitext):
        name = m.group(1)
        body = m.group(2)
        first_sentence = body.strip().split("\n", 1)[0]
        result.append({
            "kind": "Trigger", "name": name, "description": _strip_markup(first_sentence),
            "scopes": [], "targets": [], "extra": {},
        })
    return result


# ---- On_Actions ------------------------------------------------------------------------------
# H2 "List" (the real, large table) and "Actions not in the file" (small bonus table, same
# 6-column schema: On Action/Description/ROOT/THIS/FROM/Notes) are both real, current data.
# "Overview" has no table, "Removed on actions" is a different (2-column) schema and
# deprecated - skipped.
_ON_ACTION_HEADINGS = {"list", "actions not in the file"}


def parse_on_actions(wikitext: str) -> list[dict]:
    result = []
    for section in sections_with_tables(wikitext):
        if not section.heading_path:
            continue
        h2 = section.heading_path[0].lower()
        if h2 not in _ON_ACTION_HEADINGS:
            continue
        for row in split_rows(section.table_wikitext):
            cells = split_cells(row)
            if len(cells) < 3:
                continue
            name, description, root = _cell(cells, 0), _cell(cells, 1), _cell(cells, 2)
            this_ = _cell(cells, 3)
            from_ = _cell(cells, 4)
            if not name:
                continue
            scopes = _clean_scope_list(root)
            extra = {}
            if this_ and this_ not in ("-", "—"):
                extra["THIS"] = this_
            if from_ and from_ not in ("-", "—"):
                extra["FROM"] = from_
            result.append({
                "kind": "OnAction", "name": name, "description": _strip_markup(description),
                "scopes": scopes, "targets": [], "extra": extra,
            })
    return result
