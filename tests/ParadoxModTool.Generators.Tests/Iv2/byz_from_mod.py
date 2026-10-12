"""Prepares the optional local comparison of one tag of a real IV2 mod (test Iv2LocalModTests).

Usage (on a COPY of the mod; the mod itself is only read):
    python byz_from_mod.py <path to iv2_generator.py> <mod copy> <TAG> <output folder> < /dev/null
Then run the tests with PMT_IV2_BYZ_DIR=<output folder>.

Writes into the output folder:
- state.json: the PMT work state, read with the Python generator's load_existing_idea_set
  (names, descriptions, levels and modifiers; no expensive levels, no translations);
- Base.bundle: the 5 shared base files and the tag's static modifiers file of the mod copy;
- Expected-existing.bundle: the Python generator's output for that state on those files.
"""
import json
import os
import shutil
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_expected  # noqa: E402


def state_from_config(g, cfg, languages):
    categories = []
    for cat in g.CATEGORIES:
        ck = cat["key"]
        ideas = []
        for idx in range(1, cfg.counts.get(ck, 0) + 1):
            entry = cfg.idea(ck, idx)
            levels = [{"values": {"modifiers": (entry.modifiers or {}).get(li, ""), "expensive": "no"}, "translations": {}, "children": {}}
                      for li in range(cfg.idea_level_count(ck, idx))]
            ideas.append({"values": {"name": entry.name, "desc": entry.desc}, "translations": {}, "children": {"levels": levels}})
        categories.append({"values": {}, "translations": {}, "children": {"ideas": ideas}})
    return {
        "packageId": "iv2",
        "key": cfg.tag,
        "head": {"values": {"tag": cfg.tag, "coa": ""}, "translations": {}, "children": {"categories": categories}},
        "modRoot": None,
        "target": "mod",
        "outputFolder": None,
        "languages": languages,
        "languageRules": {"extendEnglish": False, "copyEnglish": True, "englishForMissing": True},
    }


def main():
    generator, mod, tag, out = sys.argv[1:5]
    g = make_expected.load_generator(generator)
    cfg = g.load_existing_idea_set(mod, tag)
    languages = sorted(g.discover_iv2_localization_files(mod, tag)) or ["english"]
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(out, "mod.txt"), "w", encoding="utf-8") as f:
        f.write(os.path.abspath(mod))
    state_path = os.path.join(out, "state.json")
    with open(state_path, "w", encoding="utf-8") as f:
        json.dump(state_from_config(g, cfg, languages), f, ensure_ascii=False, indent=2)

    with tempfile.TemporaryDirectory() as temp:
        base = os.path.join(temp, "base")
        rels = [os.path.join(*parts) for parts in g.INTEGRATE_RELATIVE_PATHS.values()]
        rels.append(os.path.join("main_menu", "common", "static_modifiers", f"02_IV2_{tag}.txt"))
        for rel in rels:
            source = os.path.join(mod, rel)
            if os.path.exists(source):
                os.makedirs(os.path.dirname(os.path.join(base, rel)), exist_ok=True)
                shutil.copy2(source, os.path.join(base, rel))
        make_expected.pack(base, os.path.join(out, "Base.bundle"))

    sys.argv = [sys.argv[0], generator, state_path, os.path.join(out, "Base.bundle"), out]
    make_expected.main()
    os.remove(os.path.join(out, "Expected-fresh.bundle"))


if __name__ == "__main__":
    main()
