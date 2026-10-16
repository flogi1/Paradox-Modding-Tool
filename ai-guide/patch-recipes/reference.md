# Patch recipe reference

A recipe is one JSON file (`recipe.json`). Property names are camelCase, enum values are camelCase strings. Properties
you leave out get their default. Unknown properties are ignored with a warning. Example: `examples/harbors/recipe.json`.

## Recipe

| Property | Type | Meaning |
|---|---|---|
| `id` | string, required | Lowercase letters, digits, `_`. Generated regions and the memory file belong to this id: never change it when you edit a recipe. |
| `name` | string | Display name. |
| `description` | string | Your explanation for the modder (see SKILL.md), plain text with `\n`. |
| `columns` | list of columns | Own and derived columns, in this order (a derived column may use earlier columns). |
| `rows` | list of rows | One row per item. |
| `rules` | list of rules | Run from top to bottom. |
| `wholeRows` | bool, default false | "Rows completely or not at all", for per-row rules only: if a per-row rule skips a row, every per-row rule leaves that row out. Global rules always run in full, also for items whose row is skipped. Use it when the per-row rules only make sense together. |
| `gameId` | string | Set by PMT to the current game. Do not set it. |
| `preset` | string or null | Set by PMT for built-in presets. Do not set it. |
| `watch` | object | The watcher (see below). Leave it out unless asked. |

`watch` has `patterns` (list of strings: extra search texts; the watcher lists every game and mod `.txt` file that
contains one of them or a row key, so the modder sees places the recipe does not cover yet) and `ignored` (list of
objects with `path`, a file or folder pattern, and `note`, why it is ignored).

## Columns and rows

Column (`columns[]`):

| Property | Type | Meaning |
|---|---|---|
| `name` | string | Letters, digits, `_`, not starting with a digit, unique, not `key` (`key` is the row key and always exists). |
| `type` | `text` (default), `number`, `list` | `list`: comma-separated values ("a, b"); see "Placeholders and templates". PMT does not compute with `number`. |
| `default` | string | Used when a row has no value. Not filled with placeholders. |
| `lookup` | object or absent | Present = derived column: PMT finds the value itself, rows never set it. |
| `titleKey` | bool | The value is a title (Crusader Kings III): PMT checks it against `common/landed_titles`; an unknown title is a row error. |

Lookup (`columns[].lookup`):

| Property | Type | Meaning |
|---|---|---|
| `files` | glob | Files to search, relative to the game root (`vanilla`) or the mod root (`mod`). |
| `source` | `vanilla` (default), `mod`, `modOverVanilla` | `modOverVanilla`: the mod's file where it has one, else the game file of the same path. |
| `match` | text with placeholders | A statement `key = value`, for example `special_building_slot = {key}`. Several searches separated by `;` all count. |
| `take` | `topLevelKey` (default), `enclosingKey` | Which block key becomes the value: the outermost block around the hit (for example the province id of a history file) or the innermost one. |
| `optional` | bool | Not found gives an empty value instead of a row error. |
| `all` | bool | All hits as a list ("a, b, c") instead of the first; the column then counts as a list column. |

How a lookup searches: it reads the files in ordinal path order and indexes every statement `key = value` with a
single value that stands inside a top-level block (at any depth; statements at the top level itself are not found).
The first hit in file order wins. The search value is compared exactly (quotes around it are ignored). A list column
used in `match` gives one search per value.

Row (`rows[]`): `key` (string, unique, required) and `values` (object: own column name → value). Values are strings
(numbers and `true`/`false` are accepted and read as text). Only own columns may appear in `values`; an empty or
missing value uses the column's `default`. Rows are always listed explicitly: a lookup fills a column for each existing
row, it never adds rows.

A row with an error (key missing or doubled, a derived value not found, unknown title) is skipped by every per-row rule
with that reason; the other rows run normally.

## Rules

Every rule has:

| Property | Type | Meaning |
|---|---|---|
| `kind` | `lines`, `blockCopy`, `template`, `listEntry`, `blockEdit` | Write it as the first property. |
| `id` | string, required | Letters, digits, `_`; unique in the recipe. Generated regions and memory belong to it: keep it when editing. |
| `name` | string | Shown in messages and the preview. |
| `enabled` | bool, default true | false = the rule stays in the recipe but does not run. |
| `scope` | `perRow` (default), `global` | `perRow`: once per row with its values. `global`: once, with no row values (`{key}` is empty, other placeholders stay as text), so use no placeholders. A global rule always runs, regardless of row errors and `wholeRows`. |

Paths: `files`, `sourceFiles` and `target` are relative to the game root (where the rule reads game files) or the mod
root (where it reads or writes mod files). They are never filled with placeholders. `target` is always one file: no `;`,
`*` or `?`, no trailing `/`, no spaces at the ends of a name.

### `lines`: change or comment lines, remove blocks

| Property | Type | Meaning |
|---|---|---|
| `files` | glob | Mod files to change. While it is empty the rule does nothing (`check` warns). |
| `match` | text with placeholders | What to find (per row: one search per row). |
| `matchKind` | `text` (default), `regex` | `text`: whole words only, spaces around `= < > { } !` optional, any run of spaces between words; `province` does not match `every_province`. `regex`: a .NET regular expression (escape `\` in JSON as `\\`; placeholder values are inserted unescaped). |
| `action` | `comment` (default), `delete`, `replace`, `removeBlock` | See below. |
| `replacement` | text | For `replace`. Placeholders are **not** filled. With both match kinds it is a .NET regex substitution: `$0` and `$&` insert the whole hit, `$_` the whole input; `$1` inserts group 1 only with `matchKind` `regex` (a `text` search has no groups); write `$$` for a literal `$`. |
| `followNext` | bool | For `removeBlock`: also remove the blocks reached through `next_building` (upgrade levels). |
| `createCopy` | bool | If a game file matching `files` has a hit and the mod has no copy, PMT first copies the whole game file into the mod (same path), then changes it. |
| `vanillaPathsOnly` | bool | Only mod files whose path also exists in the game (copies of game files); the mod's own files stay untouched. |

- `comment` puts `#` in front of the hit. If the hit shares its line with other code, the hit moves to its own line
  and the rest stays code. Never comment out a hit that contains `{` or `}`: its partner brace stays and the file
  breaks (use `removeBlock`, or a `blockEdit` with `remove`).
- `delete` removes the hit; a line that becomes empty is removed.
- `replace` replaces only the hit; a hit may span several lines. Replace rules also apply to the text that
  `blockCopy` and `template` generate into a file their `files` matches. PMT warns when the replacement matches the
  search again (the rule would not be repeatable).
- `removeBlock`: `match` is a top-level block key (usually `{key}`); every top-level definition of it in the files is
  removed.
- Comment lines, text after `#` and the generated regions of the same recipe are never matched, so a second run
  changes nothing.
- Without `createCopy` the rule changes only files that already exist in the mod.

### `blockCopy`: copy a game block into a generated region

| Property | Type | Meaning |
|---|---|---|
| `sourceFiles` | glob | Game files to take the block from (only game files are read). |
| `blockKey` | text with placeholders, default `{key}` | Key of the top-level block to copy. |
| `followNext` | bool | Also copy the blocks reached through `next_building`, into the same unit. |
| `edits` | list of edits | Applied to each copied block, in order. |
| `target` | path | Mod file that gets the region (created if missing; the region goes at its end). |
| `adoptExisting` | bool | If the key is already defined at the top level of a mod `.txt` file in the target's folder (outside the region), move that block (edited, with its changes) into the region and remove it from its file, instead of skipping the row. |

The first matching game file (ordinal order) that has the block wins. Variables `@name` defined as `@name = value` at
the top level of that same game file are replaced by their value. The row is skipped when the block is not found, or when
the key (or a `next_building` level) is already defined at the top level of a mod `.txt` file in the target's folder (the target included, outside the region).

### `blockEdit`: change a block where it stands

| Property | Type | Meaning |
|---|---|---|
| `files` | glob | Files that contain the block (mod files; with `createCopy` also game files of the same path). While it is empty the rule does nothing (`check` warns). |
| `blockKey` | text with placeholders, default `{key}` | Key of the top-level block to edit. |
| `followNext` | bool | Also edit the blocks reached through `next_building`. |
| `createCopy` | bool | If the block is only in a game file, first copy that whole game file into the mod. |
| `edits` | list of edits | Applied to the block. |

No region and no memory: the mod file itself is changed. Only statements that are still missing are inserted, so a
second run changes nothing. The row is skipped when the block is not found.

### Edits (`edits[]` of `blockCopy` and `blockEdit`)

| Property | Type | Meaning |
|---|---|---|
| `enabled` | bool, default true | false = the edit does not run. |
| `kind` | `remove`, `insertInto`, `insertAfter`, `insertAtStart`, `insertAtEnd` | What to do (this `kind` is the edit kind, not the rule kind). |
| `key` | string | A direct child of the block. Not filled with placeholders. |
| `value` | string or null | For `remove`: only statements `key = value` with this value. |
| `text` | text with placeholders | The text to insert, one or more statements. |

- `remove`: removes every direct child statement `key` (with `value`: only those with that value).
- `insertInto`: adds `text` at the end of the direct child block `key`; if the block has no such child, PMT creates
  `key = { text }` at the end. It never replaces anything.
- `insertAfter`: inserts `text` after the direct child statement `key`; if there is none, nothing happens.
- `insertAtStart` / `insertAtEnd`: at the start or end of the block (`key` is not used).
- Edits reach only direct children of the block, not deeper levels.
- If `text` renders to nothing (for example `{?barony}…{/barony}` for a row without barony), nothing is inserted.

### `template`: text per row in a generated region

| Property | Type | Meaning |
|---|---|---|
| `target` | path | Mod file (created if missing). A `.yml` target is localization: a new file gets the `l_<language>:` header from its name (`…_l_english.yml`). |
| `inside` | block path or null | `a/b`: the region goes at the end of block `b` inside top-level block `a`; the block must exist. Without it, the region goes at the end of the file. Not filled with placeholders. |
| `text` | text with placeholders | The text per row (one unit per row; per value with list columns). |

For a `.txt` target without `inside`, a row is skipped when a top-level key of its text is already defined in a
`.txt` file of the target's folder.

### `listEntry`: entry in a list of a shared file

| Property | Type | Meaning |
|---|---|---|
| `target` | path | Mod file with the list. |
| `listPath` | text with placeholders | Block path from the top level, for example `castle_holding/buildings` or `{holding}/buildings`. |
| `entry` | text with placeholders | The entry, for example `{key}` or `building = {key}`. |
| `createCopy` | bool | If the mod has no such file, first copy the game file of the same path. |

An entry that is already in the list is not added again. PMT remembers its own entries: entries that were there
before are never removed, its own entries go when their row goes, and an entry the modder deleted by hand is not
added again. A missing list skips the row.

## Placeholders and templates

Filled with the row's values only in: `lookup.match`, `lines.match`, `blockKey` (of `blockCopy` and `blockEdit`),
`edits[].text`, `template.text`, `listPath`, `entry`. Everything else is used as written.

| Syntax | Meaning |
|---|---|
| `{key}` | The row key. |
| `{col}` | The value of column `col` (own value, default or derived value). |
| `{?col}…{/col}` | The text only if `col` has a value. |
| `{!col}…{/col}` | The text only if `col` is empty. |
| `{*col}…{/col}` | The text once per value of list column `col`; inside it, `{col}` is the single value. |

- `{name}` that is not a column stays as literal text (Paradox script like `{castle_01}` is valid); PMT warns about it.
  A section without its `{/col}` is an error.
- List columns: in `lookup.match`, `lines.match`, `template.text`, `listPath` and `entry`, a list column used as
  `{col}` (outside `{*col}`) gives one instance per value: one search, one list entry, or one template unit per value.
  Several list columns give every combination. In `blockKey` and `edits[].text` there is no such expansion: `{col}`
  inserts the raw list ("a, b"). Use `{*col}…{/col}` there to repeat text per value.
- An empty value outside `{?…}`/`{!…}` skips the row in per-row `lines` searches, `listEntry` and `blockEdit` keys
  ("empty value for …"). In `template` and edit texts it is inserted as empty text, so guard it with `{?col}…{/col}`.

## Globs

Relative paths with `/`. `*` = any characters except `/`; `**/` = zero or more folders (`common/**/*.txt`); a `**`
without `/` after it does not cross folders. Several patterns are separated by `;`. Case does not matter. Folders whose
name starts with `.` are skipped.

## Generated regions and memory

`blockCopy` and `template` write their text between markers, one unit per row:

```
# PMT-Patch harbors/buildings begin
# PMT-Unit ex_harbor_north
ex_harbor_north = { … }
# PMT-Unit ex_harbor_south
ex_harbor_south = { … }
# PMT-Patch harbors/buildings end
```

`harbors` is the recipe id, `buildings` the rule id, the unit key is the row key (plus the list value for list
columns). PMT stores what it last generated in the memory file `<recipe id>.memory.json` next to the recipe, keyed by
rule id and unit key. On the next run: an untouched unit is replaced by the new text; a unit the modder edited is merged
with the new text (both kept); if both changed the same place, the file is a conflict and the modder decides; a unit
deleted by hand (with its marker line) is not generated again; a unit whose row is gone is removed (kept if edited).
So hand edits survive game updates, but only as long as recipe id and rule ids stay the same. Without the memory file
(for example in a dry run without it), units already in the mod are kept as they are and changes to the rule text do
not show.

`lines` rules have no memory: a line the modder takes back in the preview comes back on the next run.

New files are UTF-8 with BOM and CRLF line ends; copies of game files keep the game file's format. A copy made by
`createCopy` replaces the whole game file in the mod; after a game update the modder refreshes it with PMT's update
merge and applies the recipe again.

## Skipped rows and warnings

Skipped rows appear in the preview and in `pmt-patch plan` as `<rule id> <row key>: <reason>`:

| Reason | Meaning and fix |
|---|---|
| `<column>: not found` | The lookup found no hit: check `files`, `match` and `source`, or set `optional`. |
| `Duplicate key`, `Key missing` | Row keys must be unique and filled. |
| `Title "<x>" does not exist` | `titleKey` column with an unknown title. |
| `Vanilla block "<x>" not found` | `blockCopy`: no top-level block with this key in `sourceFiles`. |
| `Block "<x>" not found in <files>` | `blockEdit`: no top-level block with this key in the files (and no game copy with `createCopy`). |
| `already defined in <file>:<line>` | The key already exists in the mod outside the region: use `adoptExisting`, another target folder, or tell the modder. |
| `already in the generated region and also in <file>:<line>; remove one of the two` | `adoptExisting` found the key twice; the modder removes one. |
| `<key> is defined several times in the mod (…); not adopted` | `adoptExisting` cannot choose; the modder removes the extra definitions. |
| `List "<path>" not found in <file>` | `listEntry`: wrong `listPath`, or the target file is missing (set `createCopy`). |
| `empty value for <names>` | A placeholder is empty for this row: give a default, or guard with `{?col}`. |
| `left out as in rule <rule>: <reason>` | `wholeRows`: another per-row rule skipped the row. |
| `Path "<x>" is not allowed (…)` | Absolute path, drive, leading `/` or `..`: the whole rule does not run. |

Warnings (they do not change the exit code, but read them): `<file> skipped: <message>` (the file cannot be parsed, or
the `inside` block is missing), `Region in <file> is damaged …` (marker lines edited by hand), `@<name> is not defined
in <file>`, `Rule "<name>": "{<x>}" is not a column and stays as text`, `Rule "<name>": the replacement matches its own
search pattern …`, `Memory file … is unreadable …`, `"<glob>" matches no file in the mod or the game`.

## What the engine cannot do

Say so under **Limits** when the task needs one of these, and build only the rest:

- Only the five rule kinds above. No arithmetic, no value tables (01 → 2, 02 → 4 needs one rule per value), no
  conditions beyond `{?col}`/`{!col}`, no deleting or renaming files, no writing into game files.
- `blockCopy` resolves only plain `@name = value` variables of the same file: `@[ … ]` expressions and chained variables
  (`@a = @b`) stay as they are.
- `lines` searches do not follow upgrade levels: `{key}` matches only the row key, not later levels such as `…_02`
  (`followNext` works only for `removeBlock`, `blockCopy` and `blockEdit`). Add rows or searches for each level.
- Edits reach only direct children of a block; `inside` and `listPath` are paths from the top level of the file.
- Lookups find only `key = value` statements inside blocks and take a block key; they cannot read values by position
  or combine values.
- Paths are fixed per rule: no file per row.
- In the PMT preview the modder cannot take back single lines (U) in `.yml` files, and taken-back `lines` changes come
  back on the next run.
- PMT's editor reads every file as UTF-8; Europa Universalis IV files in Windows-1252 may show wrong special characters
  there.
- `pmt-patch` does not read `replace_path` or the game version. The PMT window uses them only for the watcher and the
  game-file snapshots, so the planned file changes are the same.

## Common errors

- A rule id or the recipe id changed while editing: old regions stay, rows are skipped as "already defined".
- A placeholder in a path (`common/{key}.txt`): paths are used as written.
- `{key}` or `{col}` in `replacement`: not filled.
- `comment` on a hit with `{` or `}` breaks the block; use `removeBlock` or a `blockEdit` with `remove`.
- A `text` search that is too short (`slot`) hits more than meant; match the whole statement (`special_building_slot = {key}`).
- Placeholders in a `global` rule.
- Row-specific line changes in a `global` rule while relying on `wholeRows`: global rules ignore it. When a line change
  must be left out together with a skipped row, make it a per-row rule (for example `match` with `{key}`).
- A list column in `blockKey` or `edits[].text` without `{*col}…{/col}`: the raw list is inserted.
- A literal `$` in `replacement` without doubling it (`$$`).
- `blockCopy` into a folder where the mod already defines the same keys.
- A derived column listed after the column that uses it.
- A lookup with `source` `mod` or `modOverVanilla` over files that the recipe's own `lines` rule comments out: after the
  first apply it finds nothing there. Use `source` `vanilla`.
