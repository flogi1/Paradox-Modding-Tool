"""Builds profiles/scriptdocs/eu4.json from local raw-wikitext snapshots of the 5 EU IV
wiki reference pages, in wikitext/<Page>.wikitext.

Run: python build_eu4.py

Why local snapshots instead of a live fetch: eu4.paradoxwikis.com sits behind a JS
bot-challenge ("Client Challenge") that returns an identical, content-free HTML shell to
any plain HTTP client (urllib, curl, the MediaWiki api.php/rest.php/index.php?action=raw
endpoints all included - verified during this tool's own development, see the plan's Task 5
ledger). A real browser clears the challenge and can read the raw wikitext fine. Refreshing
this data after a patch/DLC therefore needs a browser-capable session (e.g. Claude Code with
its built-in browser, or a person doing it by hand): open
`https://eu4.paradoxwikis.com/index.php?title=<Page>&action=raw` for each of the 5 pages
below, save the page's raw text verbatim into wikitext/<Page>.wikitext (overwriting the old
snapshot), then run this script and commit both the updated .wikitext snapshots and the
regenerated profiles/scriptdocs/eu4.json.
"""
from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

from parse_eu4 import parse_effects, parse_triggers, parse_modifiers, parse_scopes_table, parse_scopes_logic, parse_on_actions

HERE = Path(__file__).resolve().parent
WIKITEXT_DIR = HERE / "wikitext"
PAGES = ["Effects", "Triggers", "Modifier_list", "Scopes", "On_Actions"]
OUTPUT = HERE.parents[1] / "profiles" / "scriptdocs" / "eu4.json"


def read_local(title: str) -> str:
    path = WIKITEXT_DIR / f"{title}.wikitext"
    if not path.exists():
        raise FileNotFoundError(
            f"Missing {path}. See this script's module docstring: snapshots must be "
            "captured via a browser (the wiki blocks plain HTTP clients) before running this."
        )
    return path.read_text(encoding="utf-8")


def merge_duplicates(entries: list[dict]) -> list[dict]:
    """Same union rule as ParadoxModTool.ScriptDocs.ScriptDocImporter.MergeDuplicates:
    union scopes/targets, keep the first non-empty description, first-wins for extra keys."""
    by_key: dict[tuple[str, str], dict] = {}
    order: list[tuple[str, str]] = []
    for entry in entries:
        key = (entry["kind"], entry["name"])
        if key not in by_key:
            by_key[key] = dict(entry)
            order.append(key)
            continue
        first = by_key[key]
        for scope in entry["scopes"]:
            if scope not in first["scopes"]:
                first["scopes"].append(scope)
        for target in entry["targets"]:
            if target not in first["targets"]:
                first["targets"].append(target)
        for k, v in entry["extra"].items():
            first["extra"].setdefault(k, v)
        if not first["description"] and entry["description"]:
            first["description"] = entry["description"]
    return [by_key[k] for k in order]


def main() -> None:
    pages = {title: read_local(title) for title in PAGES}

    entries = []
    entries += parse_effects(pages["Effects"])
    entries += parse_triggers(pages["Triggers"])
    entries += parse_modifiers(pages["Modifier_list"])
    entries += parse_scopes_table(pages["Scopes"])
    entries += parse_scopes_logic(pages["Scopes"])
    entries += parse_on_actions(pages["On_Actions"])
    entries = merge_duplicates(entries)

    from collections import Counter
    kind_counts = Counter(e["kind"] for e in entries)
    distinct_scopes = sorted({s for e in entries for s in e["scopes"]})
    print(f"{len(entries)} entries parsed: {dict(kind_counts)}")
    print(f"Distinct scope values ({len(distinct_scopes)}):")
    for s in distinct_scopes:
        print(f"  {s!r}")
    print("Review this list for anything that isn't a real EU IV scope name (an")
    print("organizational heading like 'Military'/'Navy' leaking through means a page's")
    print("structure changed and a parser in parse_eu4.py needs a look) before committing.")

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    payload = {"fetchedUtc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "entries": entries}
    OUTPUT.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Wrote {OUTPUT}")


if __name__ == "__main__":
    main()
