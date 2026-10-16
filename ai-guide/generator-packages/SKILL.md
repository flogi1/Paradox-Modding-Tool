---
name: pmt-generator-packages
description: Build or change a generator package for the Paradox Modding Tool (PMT). A package turns a form into many mod files at once (for example a national idea set per country tag). Use when a modder gives you example files from their mod and wants a package that produces files like them.
---

# Building PMT generator packages

You help a modder who uses the **Paradox Modding Tool (PMT)**. PMT can generate many mod files from one form: the
modder fills in a form once per *set* (for example per country tag), and PMT writes all the files of that set. The
recipe for this is a **generator package**: a `package.json` plus Scriban templates in `templates/`.

Your job: from one finished example set in the modder's mod, build a package that produces exactly these files again,
with the varying parts as form fields. The full format is in `reference.md`; a small complete package is in
`examples/stadtwappen/` together with the mod files it produces (`examples/stadtwappen-mod/`).

Answer in the modder's language. Keep file contents (keys, paths, templates) in English or as the mod has them.

## Workflow

1. **Understand the example.** The task gives you whole example files, code blocks, or both. A code block names its
   path in the mod and says whether it is the *whole file* or an *excerpt*; an excerpt is only part of its file, the rest
   of that file must stay untouched (so never `ownFile` for it). `Note:` lines under a block header are the modder's
   explanation of that block; follow them. A block marked `game file` comes from the base game and is only a model of
   how the game does it; the package writes to the mod, never to game files. Read every example file and code block. Find what stays the same for every set and what varies
   (a tag, names, numbers, modifiers, descriptions). The thing that names a set is the **key field** (usually a tag).
   Repeating parts (ideas, levels, entries) become **levels**; values inside them become **fields**.
2. **Ask few, targeted questions** when the example does not tell you something, for example: "Is `HAM` the tag that
   names the set?", "Can the number of ideas vary? Between which limits?", "Which texts are translated?". Do not ask
   what the files already show.
3. **Design the form:** head fields (key field first), levels (`root`, `child`), constants for fixed groups, field
   types (`text`, `multilineText`, `number`, `bool`, `choice`, `image`, `modifiers`), `multilingual` for texts that
   appear in localization. A running number or id (a group number, an index that keeps growing in the mod) is a
   `number` field without an upper limit; `choice` is only for a fixed list that the example or the modder names.
   Do not invent limits such as `1`–`9`.
4. **Choose an output per example file** (see "Output kinds" in `reference.md`):
   - a file that belongs only to this set → `ownFile` (or `syncBlocks` when the file has one top-level block per element
     and other blocks may be added by hand);
   - localization `.yml` → `localization` (one file per language, PMT writes the `l_<language>:` header and escapes);
   - an entry inside a shared base file that other sets also use → `insertSection` (between markers) or `ensureLine`
     (one line in a list such as `OR = { ... }`);
   - an image from an asset library → `copyBinary`.

   The output `path` is the example file's path in the mod, written into the package as fixed text; only the part that
   names the set becomes `{{ tag }}`. If the task does not show where a file belongs in the mod, ask the modder for
   that path. Use a form field for a path only when the path really changes from set to set.
5. **Write the templates** so that the example comes out exactly, character for character where possible.
   Use `{{ tag }}` (your key field id) and loops like `{{ for i in ideas }}`. Watch whitespace: `~}}` removes the
   following line break *and* the next line's indentation, so start indented lines with an expression or keep the tag
   on the same line (see the example templates).
6. **Optional:** a `read` section so PMT can read existing sets back from the mod ("Read from mod" / "Aus Mod einlesen", and the
   `roundtrip` check below), and `lookups` when names should come from a database. Both are described in
   `reference.md`. Add `read` whenever the example contains the elements in one file with numbered block keys:
   it is the best way to prove the package is right.
7. **Check your package.**
   - If you can run programs (you have file access and PMT is installed), use `pmt-generator.exe` from the PMT
     program folder:
     - `pmt-generator check <package folder>` — loads the package like PMT, renders every template once with sample
       values and lists every problem; must end with `OK`.
     - `pmt-generator probe <package folder> <mod folder> [<key>]` — shows which files and blocks your `read` patterns
       match and what they read.
     - `pmt-generator roundtrip <package folder> <mod folder> <key>` — reads the set from the mod, generates it again and
       compares every file. Goal: every line `same`. Fix templates until it is.
   - If you cannot run programs, ask the modder to import your answer in PMT ("Create package with AI", tab
     "Result"), press "Check", run "Probe run" in the "Read back" mask and "Test in generator", and to paste the
     messages back to you ("Copy errors for the AI"). German UI: "Paket mit KI erstellen", "Ergebnis", "Prüfen",
     "Probelauf", "Einlesen", "Im Generator testen", "Fehler für die KI kopieren".
8. **Check before you answer**, even when you cannot run programs:
   - Every name inside `{{ }}` is the id of a field or level you defined, a loop variable, or a built-in name from
     `reference.md` ("Templates"). A name that exists nowhere (for example `groupName` without a field `groupName`)
     makes PMT stop with "variable or function not found".
   - Every `.yml` file is an output of kind `localization`, never `insertSection` or `ownFile`.
   - Every `template` an output names is one of your `templates/*.sbn` files.
   - Every `for` and `if` has its `end`.
9. **Answer with the files** in exactly this format, one block per file, nothing else inside the blocks:

```
<<<FILE package.json>>>
{ ... }
<<<END>>>
<<<FILE templates/modifiers.sbn>>>
...
<<<END>>>
```

Allowed files: `package.json`, `functions.sbn` (optional shared Scriban functions) and `templates/<name>.sbn`
(letters, digits, `.`, `_`, `-`; no sub-folders). PMT reads these blocks when the modder pastes your whole answer,
even if you put them inside code fences. You may write explanations before and after the blocks.

## Rules

- Never put game files or images from the game into the package. An asset with `"origin": "game"` is never exported;
  only assets the modder made (`"origin": "mod"`) may travel with the package.
- Paths are relative to the mod (`in_game/...`, `main_menu/...`) and must not contain `..`, a drive or a leading `/`.
- When you change an existing package, keep its `id` and `markerId`, so PMT finds the sections it wrote before.
- Invent nothing the example does not show. If a value is unclear, ask.
- Keep the package small at first (one example set, the files you are sure about), check it, then extend it.
- `gameId` is the game of the mod: `eu5` (Europa Universalis V), `eu4` (Europa Universalis IV) or `ck3` (Crusader
  Kings III); the task tells you which one.
