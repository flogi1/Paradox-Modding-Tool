---
name: pmt-patch-recipes
description: Build or change a patch recipe for the Paradox Modding Tool (PMT). A patch recipe repeats the same change to a Paradox mod for many items at once (for example turn every special building into a normal building) and can be applied again after every game update. Use when a modder describes a recurring change to their mod and wants PMT to do it.
---

# Building PMT patch recipes

You help a modder who uses the **Paradox Modding Tool (PMT)** for Crusader Kings III, Europa Universalis IV, Europa
Universalis V or Victoria 3. A **patch recipe** describes a change the modder would otherwise redo by hand after every game
update: a **table** (one row per item, columns for what differs per item) and **rules** (what to do with each row, or once
for the whole recipe). PMT runs the rules for every row, shows a preview of every changed file, and writes only when the
modder presses "Apply". Applying again after a game update brings new game content into the generated text and keeps
the modder's own changes.

**The modder cannot read recipes.** They judge your work only by your plain explanation (the recipe's `description`)
and by PMT's preview of the file changes. So the recipe must be correct, your explanation must say exactly what will
change, and you must say plainly what the recipe cannot do.

The full format is in `reference.md`. A small complete example is in `examples/`: `harbors/recipe.json` is the recipe,
`harbors-game/` the game files it reads, `harbors-mod/` the mod before applying and `harbors-expected/` the mod after
applying once. Read it: it shows a derived column, an own column with a default, a per-row block copy, a per-row
template and a global line rule.

Answer in the modder's language, and write the `description` in that language too. Keep everything inside the recipe
that is script (keys, paths, template text) as the game and the mod have it.

## Workflow

1. **Understand the feature.** Read the description and every example file and code block. A code block names its path
   and whether it comes from the mod or the game; an excerpt is only part of its file. Find what repeats per item (that
   becomes a **row**, and what differs per item becomes a **column**) and what is the same for all items (that goes
   into the rule text).
2. **Find the rows.** Every row is an explicit entry in `rows`; PMT never creates rows by itself. Prefer a complete
   list from the game files over hand-picked keys: go through the files (for example every block in
   `common/buildings/00_special_buildings.txt` that has `type = special`) and write one row per key you find, unless the
   modder wants only some. Take keys only from the files you were given or can read, never from memory. Values that
   differ per row and can be found in the files (a province id, a title) become derived columns (a `lookup`), which
   PMT fills for each row; everything else becomes an own column. State in the explanation how you found the rows
   (which file, which pattern, how many).
3. **Ask** a few targeted questions when something is unclear ("Should the slot lines be commented out or deleted?",
   "Which file should the new buildings go into?"). Never invent paths, keys or game behavior. If a file you need was
   not given, ask for it (or, with file access, look in the game and mod folders yourself).
4. **Choose the rule kinds** by these fixed rules:
   - comment out, delete or replace lines, or remove whole blocks → `lines`;
   - copy a block from the game, change it and write it into a generated file of the mod → `blockCopy`;
   - change a block where it stands (in a mod file, or in a mod copy of a game file) → `blockEdit`;
   - new text per row in a generated region of a file (script or localization) → `template`;
   - an entry in a list inside a shared file (for example a building in `buildings = { … }`) → `listEntry`.

   Per-row rules (`"scope": "perRow"`) run once per row with that row's values; global rules (`"scope": "global"`) run
   once, know no column values and always run in full (`wholeRows` and row errors do not stop them). Rules run from top to bottom.
5. **Say what cannot be done.** If part of the task does not fit these five kinds (see "What the engine cannot do" in
   `reference.md`), build only the part that works and explain the rest under a heading **Limits** in the
   `description`: what is missing and what the modder must do by hand. Never build a rule that only half fits or that
   changes more than asked.
6. **Check.**
   - With file access and PMT installed, use `pmt-patch.exe` from the PMT program folder (the task gives you the full
     path and the real game and mod folders):
     - `pmt-patch check <recipe.json> [--game <folder>] [--mod <folder>] [--previous <recipe.json>]` checks the recipe
       like PMT's import. One line per issue (`<where>: <message>`, warnings start with `warning: `), then `OK` or
       `N issue(s)`. The exit code decides: 0 = no errors (warnings may remain), 1 = errors, 2 = wrong call. Pass
       `--previous` with the open recipe when you change an existing one.
     - `pmt-patch plan <recipe.json> --game <folder> --mod <folder> [--rule <id>] [--row <key>] [--diff] [--keep <folder>]`
       is a dry run (it runs the check first; errors stop it): it prints the resolved table, every file with its status (`new`, `changed`, `conflict`), skipped
       rows with rule and reason, warnings and a summary line. `--diff` adds a unified diff per file, `--rule` and
       `--row` narrow the run, `--keep <folder>` writes the planned files into a folder of yours (not inside the mod,
       the game or the recipe folder). Exit 0 = no skipped rows, conflicts or row errors; 1 = there are some.
     - Nothing is ever written into the mod. `plan` reads `<id>.memory.json` next to your recipe if it exists; keep the
       memory file you were given next to `recipe.json`.
     - Fix until `check` has no errors and `plan` has no skipped rows you cannot explain. Read the diff: is every change
       one the modder asked for, and nothing more? Read the warnings too.
   - Without file access, ask the modder to import your answer in PMT ("Create recipe with AI", tab "Result";
     German UI: "Rezept mit KI erstellen", "Ergebnis") and send back the messages ("Copy errors for the AI" /
     "Fehler für die KI kopieren"). PMT then shows the preview.
7. **When you change an existing recipe**, keep the recipe `id` and the `id` of every existing rule, also when you
   rewrite a rule completely. PMT finds the text it generated earlier and the modder's hand edits by these ids; a
   renamed id leaves the old generated text behind and skips the rows as "already defined". Add new rules with new ids.
   Do not drop a rule that already wrote something into the mod; if it must go, say so in the explanation.
8. **Answer** with the complete recipe in exactly this format:

```
<<<FILE recipe.json>>>
{ ... the whole recipe ... }
<<<END>>>
```

Only `recipe.json` is allowed. PMT reads the block even inside a code fence. You may write text before and after the
block; repeat the main points of the explanation there for the modder.

## The explanation (`description`)

The `description` is plain text (line breaks as `\n`, no Markdown tables) shown to the modder in PMT after the import
and stored in the recipe. Write it for someone who knows their mod but not PMT's mechanics. It contains:

- **What it does**, in the modder's terms ("turns the three harbors into normal buildings"), not in rule terms.
- **Files it changes**: every file, and whether it is new, a new copy of a game file, or an existing mod file.
- **Where the rows come from**: the file and pattern you used, how many rows, and how own column values were chosen.
- **What to check in the preview**: two or three concrete things the modder can look at.
- **Limits** (heading only when something cannot be done): what is missing and what to do by hand.

See the `description` in `examples/harbors/recipe.json`.

## Rules

- Write `kind` as the first property of every rule (`"kind": "lines"`, `"blockCopy"`, `"template"`, `"listEntry"`,
  `"blockEdit"`). Enum values are camelCase strings (`"perRow"`, `"comment"`, `"insertInto"`).
- The recipe `id` uses only lowercase letters, digits and `_`. Rule ids use only letters, digits and `_`, and are unique.
- Never write lines starting with `# PMT-` (`# PMT-Patch`, `# PMT-Unit`) into rule text: PMT writes these markers itself.
- Paths are relative to the game root or the mod root (`common/buildings/…`), with `/`, never absolute, never with a
  drive, a leading `/` or `..`.
- Placeholders like `{key}` work only in rule text (searches, templates, edit texts, block keys, list paths and entries),
  never in `files`, `sourceFiles`, `target`, `inside` or a `lines` rule's `replacement`: these are used exactly as written.
- Do not set `gameId` and `preset` (PMT sets them) and leave `watch` out unless the modder asks for the watcher.
- Keep the recipe as small as the task allows: few rules, no options you do not need. Start with what you are sure of.
- Change only what the modder asked for. A rule that would also hit lines or blocks the modder did not mean must be
  narrowed (a more exact search, fewer files) or explained.
