"""Produces the expected IV2 outputs with the Python generator for the PMT comparison test.

Usage (run once when the fixtures change; never needed for the normal test run):
    python make_expected.py <path to iv2_generator.py> <TST.json> <Base.bundle> <output folder> < /dev/null
    python make_expected.py pack <folder> <bundle>      (pack a folder into a bundle)
    python make_expected.py ai-chooser <TST.json> <bundle>...   (only the IV2 AI chooser additions)

Fixtures are bundles (one text file per scenario, "##FILE <relative path>" before each file), because
the IV2 paths exceed the Windows path limit inside the repository.
- "existing": Base is unpacked, then build_files (without the static modifiers file),
  merge_static_modifiers_into_mod on the static file and integrate_all in place.
- "fresh": build_files into an empty folder and integrate_all on missing base files
  (the generator's skeletons).
Writes Expected-existing.bundle and Expected-fresh.bundle into the output folder.
The coat of arms is not produced here; the C# test checks it against its own test zip.
Deliberate PMT deviations from the Python generator (PMT_DEVIATIONS) are applied to both bundles:
the generic actions check the mod's current limit variable iv2_var_current_national_idea_limit and
use the game's current automation category governmentreforms.
"""
import importlib.util
import json
import os
import shutil
import sys
import tempfile

MARK = "##FILE "

# Python output -> PMT output, where the PMT package deliberately differs
PMT_DEVIATIONS = [
    ("var:iv2_var_national_idea_limit > var:iv2_var_national_idea_current",
     "var:iv2_var_current_national_idea_limit > var:iv2_var_national_idea_current"),
    ("player_automated_category = governmentreform\n", "player_automated_category = governmentreforms\n"),
    # EU5 1.4 beta: "[Country]" gives the runtime error "couldn't convert parameter value"
    ('parameter_value = "[Country]"', 'parameter_value = "[Country.MakeScope]"'),
]


def apply_deviations(bundle):
    with open(bundle, encoding="utf-8") as f:
        text = f.read()
    for old, new in PMT_DEVIATIONS:
        text = text.replace(old, new)
    with open(bundle, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def pack(folder, bundle):
    parts = []
    for root, _, names in os.walk(folder):
        for name in names:
            full = os.path.join(root, name)
            rel = os.path.relpath(full, folder).replace("\\", "/")
            with open(full, encoding="utf-8-sig") as f:
                text = f.read().replace("\r\n", "\n")
            if not text.endswith("\n"):
                text += "\n"
            parts.append((rel, text))
    with open(bundle, "w", encoding="utf-8", newline="\n") as f:
        for rel, text in sorted(parts):
            f.write(MARK + rel + "\n" + text)


def unpack(bundle, folder):
    with open(bundle, encoding="utf-8") as f:
        text = f.read()
    for chunk in text.split(MARK)[1:]:
        rel, _, content = chunk.partition("\n")
        full = os.path.join(folder, *rel.split("/"))
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)


def load_generator(path):
    spec = importlib.util.spec_from_file_location("iv2_generator", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def config_from_state(g, state):
    tag = state["head"]["values"]["tag"]
    categories = state["head"]["children"]["categories"]
    counts, levels, ideas = {}, {}, {}
    for cat, node in zip(g.CATEGORIES, categories):
        ck = cat["key"]
        idea_nodes = node.get("children", {}).get("ideas", [])
        counts[ck] = len(idea_nodes)
        levels[ck] = [len(i.get("children", {}).get("levels", [])) for i in idea_nodes]
        for idx, idea in enumerate(idea_nodes, 1):
            level_nodes = idea.get("children", {}).get("levels", [])
            modifiers = {li: l["values"].get("modifiers", "") for li, l in enumerate(level_nodes)
                         if l["values"].get("modifiers", "").strip()}
            expensive = {li: True for li, l in enumerate(level_nodes) if l["values"].get("expensive") in ("yes", "true")}
            translations = {}
            for field in ("name", "desc"):
                for lang, text in idea.get("translations", {}).get(field, {}).items():
                    translations.setdefault(lang, {"name": "", "desc": ""})[field] = text
            values = idea.get("values", {})
            ideas[(ck, idx)] = g.IdeaEntry(ck, idx, values.get("name", ""), values.get("desc", ""), modifiers, translations, expensive)
    rules = state.get("languageRules", {})
    return g.Config(tag, counts, levels, ideas,
                    loc_languages=state.get("languages") or ["english"],
                    loc_extend_english=rules.get("extendEnglish", False),
                    loc_copy_english=rules.get("copyEnglish", False),
                    extend_missing_translations_to_english=rules.get("englishForMissing", False))


def integrate(g, cfg, root):
    paths = {key: os.path.join(root, *parts) for key, parts in g.INTEGRATE_RELATIVE_PATHS.items()}
    for key, path, ok, error in g.integrate_all(cfg, paths):
        if not ok:
            raise SystemExit(f"integrate {key} failed: {error}")


# ---- IV2 AI chooser (mod spec docs/superpowers/specs/2026-10-10-iv2-ai-idea-chooser-design.md, 6.5) ----
# Deliberate PMT additions on top of the Python generator: the base select action has no AI tick, a historic
# chooser per tag picks the ideas (weighted random_list) and gets its own AI list. Reference implementation
# below, independent of the Scriban template: entries copy allow/effect of the expected generic actions.

PMT_REGEX_DEVIATIONS = [
    (r"(iv2_ga_select_historic_[A-Z0-9]+_\d+ = \{\n\ttype = owncountry[ \t]*\n\n\tai_tick = )monthly\n\tai_tick_frequency = 36",
     r"\1never\n\t#ai_tick_frequency = 36"),
]
HISTORIC_ORDER_DECAY = 0.6      # = historic_order_decay in the mod's iv2_ai_chooser_config.json
CATEGORY_KEYS = ("adm", "dip", "army", "navy")
CATEGORY_BASES = (0, 10, 20, 30)


def read_bundle(bundle):
    with open(bundle, encoding="utf-8") as f:
        text = f.read()
    files = {}
    for chunk in text.split(MARK)[1:]:
        rel, _, content = chunk.partition("\n")
        files[rel] = content
    return files


def write_bundle(bundle, files):
    with open(bundle, "w", encoding="utf-8", newline="\n") as f:
        for rel in sorted(files):
            text = files[rel] if files[rel].endswith("\n") else files[rel] + "\n"
            f.write(MARK + rel + "\n" + text)


def _block(text, name):
    start = text.index(name + " = {")
    depth, i = 0, text.index("{", start)
    while True:
        depth += {"{": 1, "}": -1}.get(text[i], 0)
        if depth == 0:
            return text[start:i + 1]
        i += 1


def _inner(block, name):
    return _block(block, name)[len(name) + 4:-1]


def historic_weight(position, size, mean):
    import math
    w = 100 * HISTORIC_ORDER_DECAY ** position
    return max(1, math.floor(w * math.sqrt(mean / size) + 0.5))


def historic_chooser(state, ga_text):
    tag = state["head"]["values"]["tag"]
    cats = state["head"]["children"]["categories"]
    sizes = [len(c.get("children", {}).get("ideas", [])) for c in cats]
    filled = [s for s in sizes if s]
    mean = sum(filled) / len(filled) if filled else 0
    lines = ["# GENERATED by PMT package iv2 - historic AI chooser",
             f"iv2_ga_ai_choose_historic_{tag} = {{", "\ttype = owncountry", "\tai_tick = monthly", "\tai_tick_frequency = 6",
             "\tautomation_tick = never",
             f"\tpotential = {{ scope:actor = {{ is_ai = yes has_or_had_tag = {tag} }} }}",
             "\tallow = {", "\t\tscope:actor = {", "\t\t\tiv2_scripted_trigger_use_historic_ideas = yes",
             "\t\t\tvar:iv2_var_current_national_idea_limit > var:iv2_var_national_idea_current", "\t\t\tOR = {"]
    # allow = at least one entry valid (a random_list without a valid option is an engine error)
    for base, size in zip(CATEGORY_BASES, sizes):
        for pos in range(size):
            action = _block(ga_text, f"iv2_ga_select_historic_{tag}_{base + pos + 1}")
            lines.append("\t\t\t\tAND = {" + _inner(action, "allow") + "}")
    lines += ["\t\t\t}", "\t\t}", "\t}", "\tai_will_do = { add = { value = 1000 } }",
              "\teffect = {", "\t\tscope:actor = {", "\t\t\trandom_list = {"]
    for base, size in zip(CATEGORY_BASES, sizes):
        for pos in range(size):
            number = base + pos + 1
            action = _block(ga_text, f"iv2_ga_select_historic_{tag}_{number}")
            lines.append(f"\t\t\t\t{historic_weight(pos, size, mean)} = {{  # {tag}_{number}")
            lines.append("\t\t\t\t\ttrigger = {" + _inner(action, "allow") + "}")
            lines.append(_inner(action, "effect"))
            lines.append("\t\t\t\t}")
    lines += ["\t\t\t}", "\t\t}", "\t}", "}", ""]
    return "\n".join(lines)


def historic_ai_list(tag):
    return "\n".join([
        f"# IV2 AI chooser: the AI picks historic ideas only through iv2_ga_ai_choose_historic_{tag}",
        f"iv2_ga_list_ai_choose_historic_{tag} = {{", "\tpotential = {", "\t\tis_ai = yes",
        "\t\tiv2_scripted_trigger_use_historic_ideas = yes", f"\t\thas_or_had_tag = {tag}", "\t}",
        "\tactions = {", f"\t\tiv2_ga_ai_choose_historic_{tag}", "\t}", "}", ""])


def apply_ai_chooser(bundle, state):
    """Regex deviation on the generic actions, the chooser file and its AI list. Idempotent."""
    import re
    tag = state["head"]["values"]["tag"]
    files = read_bundle(bundle)
    ga = f"in_game/common/generic_actions/iv2_GA_ideas_{tag}.txt"
    for old, new in PMT_REGEX_DEVIATIONS:
        files[ga] = re.sub(old, new, files[ga])
    files[f"in_game/common/generic_actions/iv2_ga_ai_choose_historic_{tag}.txt"] = historic_chooser(state, files[ga])
    lists = f"in_game/common/generic_action_ai_lists/03_iv2_gal_nat_idea_{tag}.txt"
    for rel in [r for r in files if r.endswith(".yml")]:
        lang = rel.split("/")[2]
        if f" iv2_ga_ai_choose_historic_{tag}:" in files[rel]:
            continue
        if lang == "german":
            name, desc = "KI: historische Nationalidee wählen", f"Nur für die KI: wählt eine historische Nationalidee von ${tag}$ per gewichtetem Zufall."
        else:
            name, desc = "AI: choose a historic national idea", f"Only used by the AI: picks a historic national idea of ${tag}$ by weighted chance."
        anchor = f' iv2_hist_idea_flag_tt_desc_{tag}: "This is a Historic National Idea of ${tag}$."\n'
        files[rel] = files[rel].replace(anchor, anchor + f' iv2_ga_ai_choose_historic_{tag}: "{name}"\n'
                                        f' iv2_ga_ai_choose_historic_{tag}_desc: "{desc}"\n', 1)
    marker = f"# IV2 AI chooser: the AI picks historic ideas only through iv2_ga_ai_choose_historic_{tag}"
    base_lists = files[lists].split(marker)[0]  # drop an older version of the block
    files[lists] = base_lists.rstrip("\n") + "\n\n" + historic_ai_list(tag)
    write_bundle(bundle, files)


def main():
    if sys.argv[1] == "pack":
        pack(sys.argv[2], sys.argv[3])
        return
    if sys.argv[1] == "ai-chooser":
        # python make_expected.py ai-chooser <TST.json> <bundle> [<bundle> ...]: only the AI chooser additions
        with open(sys.argv[2], encoding="utf-8") as f:
            state = json.load(f)
        for bundle in sys.argv[3:]:
            apply_ai_chooser(bundle, state)
        return

    generator, state_path, base_bundle, out = sys.argv[1:5]
    g = load_generator(generator)
    with open(state_path, encoding="utf-8") as f:
        cfg = config_from_state(g, json.load(f))
    files = g.build_files(cfg)
    static_rel = f"main_menu/common/static_modifiers/02_IV2_{cfg.tag}.txt"

    with tempfile.TemporaryDirectory() as temp:
        existing = os.path.join(temp, "existing")
        unpack(base_bundle, existing)
        g.write_files({k: v for k, v in files.items() if k != static_rel}, existing)
        g.merge_static_modifiers_into_mod(cfg, os.path.join(existing, *static_rel.split("/")), mod_root=existing)
        shutil.rmtree(os.path.join(existing, "_iv2_generator_backups"), ignore_errors=True)
        integrate(g, cfg, existing)
        pack(existing, os.path.join(out, "Expected-existing.bundle"))

        fresh = os.path.join(temp, "fresh")
        os.makedirs(fresh)
        g.write_files(files, fresh)
        integrate(g, cfg, fresh)
        pack(fresh, os.path.join(out, "Expected-fresh.bundle"))
        for name in ("Expected-existing.bundle", "Expected-fresh.bundle"):
            apply_deviations(os.path.join(out, name))
            with open(state_path, encoding="utf-8") as f:
                apply_ai_chooser(os.path.join(out, name), json.load(f))
        print("existing:", sum(len(f) for _, _, f in os.walk(existing)), "files; fresh:", sum(len(f) for _, _, f in os.walk(fresh)), "files")


if __name__ == "__main__":
    main()
