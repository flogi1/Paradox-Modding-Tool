# Generator package reference

A package is a folder with `package.json`, a folder `templates/` with Scriban templates (`.sbn`) and optionally
`functions.sbn` (shared Scriban functions) and `assets/` (files the package reads, e.g. a JSON database).

## package.json — top level

| Key | Meaning |
|---|---|
| `id` | Package id = folder name: lowercase letters, digits, `_`, `-`, at most 40 characters. |
| `name` | Name in the package list. Text or language map (`{ "en": "...", "de": "..." }`). |
| `gameId` | Game of the package: `eu5`, `eu4` or `ck3` (the game profiles PMT ships). |
| `keyField` | Id of the head field whose value names a set (e.g. `tag`). |
| `keyFields` | Instead of `keyField`: several head field ids whose values joined by `keySeparator` (default `_`) name a set, e.g. `["category", "group"]` → `adm_18`. Every key field except the last needs a `pattern` or is a `choice` whose values do not contain the separator. |
| `markerId` | Optional id used in PMT's markers (`# PMT-Generator <markerId>/...`); defaults to `id`. Keep it when editing a copy. |
| `head` | List of fields asked once per set (the key field among them). |
| `root` | Optional first level (see Levels). |
| `constants` | Tables for fixed levels: `{ "<table>": [ { "<column>": value, ... }, ... ] }`. |
| `targets` | Allowed targets: `["mod", "output"]` (default both). |
| `languages` | Default language rules: `extendEnglish`, `copyEnglish`, `englishForMissing` (booleans). |
| `assets` | `{ "<asset id>": { "path": "assets/file.zip", "origin": "mod" } }`; `origin` is `game` or `mod`. |
| `externals` | `{ "<external id>": { "label": ..., "hint": ... } }`: files outside the mod that a `jsonEntry` output writes into. `label` names the file in the generator window, `hint` (optional) is its tooltip; the user chooses the path there, it is not part of the package. |
| `afterGenerate` | Optional hint (text or language map) that the generator window appends to its message after a successful "Generate" ("Erzeugen"), e.g. "Run iv2_build_ai_chooser.py". Not shown when generating fails. |
| `outputs` | List of outputs (see Output kinds). |
| `warnings` / `errors` | List of `{ "when": ..., "message": ... }`: `when` is a Scriban expression, `message` a text or language map; a true error blocks generating. |
| `read` | Optional: how to read sets back from a mod (see Reading back). |
| `lookups` | Optional: database sources for fields (see Lookups). |
| `candidates` | Optional: list of game modifiers the mod does not use yet, with batch creation of sets (see Candidates). |

Language rules: `extendEnglish` writes localization for every language of the game profile (English text where no
translation exists), `copyEnglish` fills empty languages with English, `englishForMissing` uses English where a
translation is missing.

## Fields

`{ "id": "name", "label": "Name", "type": "text", ... }`

| Key | Meaning |
|---|---|
| `id` | Name in templates; letters, digits, `_`, not starting with a digit. |
| `label` | Label in the form (text or language map). |
| `type` | `text`, `multilineText`, `number`, `bool` (yes/no), `choice`, `image`, `modifiers` (multi-line `key = value`; a line without a value blocks "Generate" ("Erzeugen") for every package). |
| `required` | `true` = must be filled before generating. |
| `pattern` | Regular expression the value must match, e.g. `^[A-Z]{3}$`. |
| `choices` | Values of a `choice` field. |
| `multilingual` | Text with one value per language (used by localization outputs). |
| `default` | Start value. |
| `asset` | For `image` fields: id of the asset library (zip or folder) to choose from. |
| `lookup` | "From database": `{ "sources": [ "<lookup id>" ], "fills": { "name": "<field>", "desc": "<field>" } }`. |
| `autoNumber` | `true` on the last key field of type `number`: "New" ("Neu", a new work state) prefills it with the highest number found for the other key parts plus 1 (gaps stay free); the user may overwrite it. Before the preview PMT hashes every localization key the plan adds (MurmurHash3) and compares them with the keys of the game and the mod; a new state whose keys collide moves to the next number and says so (only in a package with a `read` section), an existing state (its key is in the mod) only gets a warning. Not allowed on any other field. |

## Levels

A level is a list of elements with fields; it may have one `child` level.

| Key | Meaning |
|---|---|
| `id`, `label` | Name in templates (list name) and label. |
| `fields` | Fields of each element. |
| `child` | Optional deeper level. |
| `constantsTable` | Fixed level: one element per row of this table; row columns are readable like fields. |
| `min`, `max` | Countable level: allowed element count. |
| `initial` | Element count of a new set (default `min`). |
| `numberBaseField` | Column of the parent element whose value plus the index gives the element's number (IV2: `base`). |

Every element has `number`: base + index with `numberBaseField`, otherwise its index from 1.

## Templates (Scriban)

Templates see the head fields (`tag`), one list per level (`ideas`) with the fields of each element and also
`index` (from 1), `index0`, `first`, `last`, `number`, `suffix` (`""` for the first element, then `_1`, `_2` …),
`parent`, for every multilingual field `x` also `x_by_lang` (the raw text, not escaped for .yml, per language of the work state:
`name_by_lang.german`; for JSON templates), plus `item` (in outputs with `forEach`), `lang` (localization), `constants`, `languages`, `language_rules`.
Unknown names are errors. `{{ x -}}` and `{{ x ~}}` remove the whitespace after the tag, including the line break and
the indentation of the next line; `{{- x }}` / `{{~ x }}` remove the whitespace before it. So an indented line right
after such a tag loses its indentation: start it with an expression (`{{ "\t" + ... }}`) or keep the loop tag on the
same line as the text (see `templates/loc.sbn` in the example).

```
{{ for i in ideas ~}}
sw_modifier_{{ tag }}_{{ i.number }} = {
{{ if i.modifiers != "" ~}}
{{ "\t" + (i.modifiers | string.replace "\n" "\n\t") }}
{{ end ~}}
}
{{ end ~}}
```

## Output kinds

Every output has `id`, `kind` and `path` (a Scriban template, relative to the mod). `forEach: "<level>"` creates the
output once per element of that level (the element is `item`). `newLine` forces `lf` or `crlf`.

| `kind` | Keys | Effect |
|---|---|---|
| `ownFile` | `template` | Writes the whole file. |
| `localization` | `template`, `keys` | One file per language; `{{ lang }}` in the path; PMT adds `l_<language>:` and escapes `"` and line breaks. Without `keys` PMT writes the whole file. With `keys` (a regular expression on the localization key, e.g. `^iv2_x_{{ group }}(_|$)`) the file is shared: PMT replaces only the matching lines in place and keeps all others. With `keys`, `insertAfter` (a regular expression on the localization key) puts new lines after the last existing line whose key matches it, instead of at the end of the file. |
| `insertSection` | `template`, `block`, `skeleton`, `legacyBegin`, `legacyEnd`, `legacyPatterns` | Puts the text between PMT markers into the block (or at the file end); a second run replaces only the marked text. `skeleton` creates a missing file. |
| `ensureLine` | `block`, `line`, `skeleton` | Adds one line to a block if it is missing (e.g. `has_or_had_tag = {{ tag }}` in an `OR`). |
| `syncBlocks` | `template`, `keyPattern`, `keep`, `checkUnreferenced` | Syncs top-level blocks whose key matches `keyPattern` (`*` = name characters): adds missing, removes ones no longer rendered (after a confirmation), replaces only the sub-keys the template sets; `keep` lists sub-keys left alone. |
| `copyBinary` | `source` | Copies `<asset id>:<entry>` from an asset library, e.g. `coa:{{ coa }}`. |
| `syncMatching` | `template`, `match`, `block`, `skeleton`, `insertAfter`, `ifLimit` | For sets spread over shared files. The children of `block` (or of the file) are units: statements, GUI headers with their body (`template x { }`, `type x = widget { }`), list values, unnamed blocks like `if = { }` and comment lines. Units whose text matches the regular expression `match` are replaced in place by the rendered text; without a match the text goes to the end of the block or file, or, with `insertAfter` (a regular expression rendered like `match`, tested against each unit), right after the last unit it matches, with as many blank lines before it as stand above that unit (no such unit: to the end as before; `insertAfter` is ignored for units that already exist). No markers. Write boundaries into `match` (`adm_{{ group }}(?![0-9])`) and render the indentation the file uses. With `ifLimit` (a Scriban template, e.g. `{{ condition }}`) units can sit inside shared `if = { limit = { <condition> } … }` blocks: such an `if` is not a unit, its other children are. Rendered empty = the unit belongs to the top level; otherwise it belongs inside the `if` whose `limit` equals the rendered condition (compared like "Format only" in "Check mod" ("Nur Format" in "Mod prüfen"): comments and whitespace differences ignored, `{value=x}` equals `{ value = x }`). PMT replaces it in place when it already sits there; otherwise it moves it (removing an `if` left with only its `limit`) to the top level, to the end of the matching `if` (with `insertAfter` only one that still holds a unit matching it after the hits are removed), or into a new `if` after the `insertAfter` unit (or at the end). Neighbours in the same `if` stay. With a condition the template must indent one level deeper (inside the `if`). |
| `check` | `template`, `message` | Writes nothing. Each non-empty rendered line is `<regex> => <argument>`; after all other outputs PMT tests the regex against the planned text of `path` (else the file in the output folder, else in the mod; no file = nothing to test). A regex without a hit adds `message` (text per interface language, Scriban with the extra variable `arg` = the line's argument) as a warning to the preview. Use it to warn about other sets that must be generated again, e.g. a partner group that does not exclude this one. |
| `jsonEntry` | `external`, `jsonPath`, `entryKey`, `template` | Sets one entry in a JSON file outside the mod (an id from `externals`; write `"path": ""`). `jsonPath` is a dot path of objects in the file (`entries`; empty = root object), `entryKey` the key in it (Scriban), `template` the entry's value as JSON; string values must be JSON-escaped, e.g. `{{ name | object.to_json }}` (names can contain `"` or `\`). A key that exists is replaced in place, a new one is appended; a template that renders empty writes nothing (the entry stays as it is or is not added). PMT writes Python `json.dumps(ensure_ascii=False, indent=N)` style (indent and line ending of the file), so the rest stays byte-identical, and copies the old file to `backups/` next to it first. The only output that may write outside the mod; skipped with a warning when the file is not chosen. |

`block` is a path of keys separated by `.` (`sw_setup.sw_tags`); in `.gui` files also `types name` / `type name`.
`legacyBegin`/`legacyEnd` name markers of an older tool that PMT keeps; `legacyPatterns` are regular expressions for
unmarked old entries that PMT removes before inserting.

## Reading back (`read`)

```json
"read": {
  "keys": "^in_game/common/static_modifiers/sw_(?<key>[A-Z]{3})\\.txt$",
  "elements": { "file": "in_game/common/static_modifiers/sw_{{ tag }}.txt", "block": "sw_modifier_{{ tag }}_(?<number>\\d+)",
                "level": "ideas", "fields": { "modifiers": { "body": true, "except": [ "game_data" ] } } },
  "localization": [ { "key": "sw_{{ tag }}_(?<number>\\d+)", "field": "ideas.name" } ],
  "flags": [ { "file": "...", "block": "...(?<number>\\d+)", "contains": "expensive", "field": "ideas.expensive" } ],
  "assets": [ { "field": "coa", "file": "gfx/coat_of_arms/{{ tag }}.dds" } ]
}
```

- `keys`: regular expression on the mod's paths (with `/`); group `key` names a set. When sets live in shared files,
  `keys` is an object `{ "file": "<regex on paths>", "block": "<regex on top-level block keys>" }`: every top-level block
  key of a matching file that matches `block` names a set, by group `key` or by groups named like the key fields
  (`ideagroup_modifier_(?<category>adm|dip)_(?<group>\\d+)_1`).
- A `localization` row whose `field` has no level (e.g. `title`) fills a head field.
- `values`: `[{ "file", "block", "pattern", "field", "join" }]`: in the block whose key matches `block`, every hit of
  `pattern` adds its group `value`; the values without duplicates, joined by `join` (default a line break), fill
  `field` (head field or `level.field`). Example: religions from `religion = religion:(?<value>\\w+)`. With `body`
  (a dot path of keys such as `allow`; `.` = the matched block itself) instead of `pattern`, the whole content of that sub-block fills the field.
- `json`: `[{ "external", "jsonPath", "entryKey", "property", "field", "join" }]` reads a value from a JSON file declared under
  `externals` (the path the user chose in the generator window; none = the field stays, with a note). `jsonPath` is the dot path of
  objects holding the entries, `entryKey` (Scriban, rendered with the key's head values) names the entry, `property` the value in
  it. The value fills the head field `field`: an array is joined by `join` (default a line break), a string is taken as it is. A
  missing file, entry or property leaves the field unchanged and adds a note. Example:
  `{ "external": "aiTags", "jsonPath": "entries", "entryKey": "nat:{{ category }}_{{ number }}", "property": "tags", "field": "aiTags" }`.
- `elements`: every top-level block of `file` whose key matches `block` creates an element of `level`; group `number`
  is the element number (see Levels), group `suffix` (`_1`, `_2`) picks the element of the child level. With a countable top
  level (no `constantsTable`), a pattern with group `suffix` but without group `number` addresses the element of that level by
  the suffix alone (`""` = element 1, `_n` = element n+1); `flags` and `values` with a `level.field` target address the same way.
  `fields` with `body` = true take the block's lines except the `except` keys.
- `localization`: `key` pattern and target `field` as `level.field` (a multilingual text field); PMT reads the
  localization output's file per language. With several localization outputs, `localizationOutput` names the output
  id to read from (default: the first one). An entry with its own `output` (id of a localization output) reads from
  that output's files instead, e.g. texts kept in a second shared file.
- `flags`: a `bool` field (head field or `level.field`) becomes yes when the matched block in `file` contains `contains`, or
  when the regular expression `pattern` (Scriban rendered) finds a hit in it.
- Block keys: a GUI header without `=` (`types iv2_nat_ideas_setup { }`) is a block whose key is the header text
  (`types iv2_nat_ideas_setup`), so `flags` and `values` can read `.gui` files.
- `assets`: an `image` head field gets the library entry with the same bytes as `file`.
- All paths must stay inside the mod.

## Lookups

```json
"assets": { "cities": { "path": "assets/cities.json", "origin": "mod" } },
"lookups": [
  { "id": "advances", "kind": "gameKeys", "folder": "in_game/common/advances", "depth": 1, "namedOnly": false,
    "tagFromFile": "^country_([A-Za-z]{2,5})\\.txt$", "tagInBlock": [ "has_or_had_tag" ], "label": "Advance" },
  { "id": "cities", "kind": "json", "asset": "cities", "items": "cities[].arms[]",
    "fields": { "key": "id", "name": "title", "tag": "../tag" }, "label": "City" }
]
```

- `gameKeys`: blocks from every `.txt` in `folder` of the game; `depth` 1 or 2; `namedOnly` keeps blocks with a
  localization text; `tagFromFile` (group 1) or `tagInBlock` give the tag. Names and descriptions come from the game's
  localization.
- `json`: entries from the asset file; `items` is a path (`name[]` steps into each list element); `fields` maps
  `key`, `name`, `desc`, `tag`, `nation` to properties (`../x` reads from the parent).
- A field uses sources with `sources` in its `lookup`; `fills` names the fields of the same level that get `name` and
  `desc`.

## Candidates

Optional section `candidates`: lists the game modifiers that the mod does not use yet (not inside the `used` blocks of its files) and creates work
states from them. A modifier that is only in a work state is still listed; the window marks it (column "Work state" / "Arbeitsstand") and asks for
confirmation before creating a duplicate.

```json
"candidates": {
  "category": "country",
  "used": [ { "file": "^in_game/common/static_modifiers/.*[.]txt$", "block": "^iv2_.*$", "except": [ "game_data" ] } ],
  "groups": [ { "id": "goodsOutput", "label": { "de": "Warenausstoß", "en": "Goods output" }, "pattern": "(^|_)(output|production)(_|$)", "hidden": true } ],
  "create": { "ask": [ "category", "subTab" ], "name": "name", "modifiers": "levels.modifiers" }
}
```

| Key | Meaning |
|---|---|
| `category` | Source of the modifiers: the union of definition category and script-database scope with this name. Empty or `all` is not a category. |
| `used` | Required, not empty. Each entry: `file` (regex on the relative path), `block` (regex on the whole top-level key), optional `except` (child keys that do not count, e.g. `game_data`). Used = the direct child statements of each matching top-level block, minus `except`; comments never count. |
| `groups` | Optional list of `{ "id", "label", "pattern", "hidden" }`. `pattern` is a regex on the modifier name; groups are tried in package order, the first match wins, the rest is "General" ("Allgemein", shown first). `hidden` = hidden by default (the window can toggle it). Use name-part boundaries like `(^\|_)(word\|word2)(_\|$)`, not bare substrings. `id` non-empty and unique. |
| `create` | `{ "ask", "name", "modifiers" }`. `ask`: head fields of type `choice` the user picks once for the batch (not the `autoNumber` field); together with the `autoNumber` field they must cover every key field. `name`: head field of type `text`. `modifiers`: `<root level id>.<field id>` of a field of type `modifiers`. |

- The package must have an `autoNumber` key field (new states take the next number).
- Every regular expression (`file`, `block`, `pattern`) must compile; otherwise the package does not load.
- The window keeps its settings in `candidates.json`: the hide toggle per group (`HiddenGroups`), the "not as idea" ("nicht als Idee") list (`NotAsIdea`)
  and the extra hide regex (`CustomHide`). The `ask` values are not stored.
- Renaming a group `id` makes the user's hide toggle for it fall back to the package `hidden` default; the old id stays orphaned in `candidates.json`.

## Common errors

- An unknown name in a template (typo in a field id, or a field you forgot to define) — check every `{{ }}`.
  "Check" ("Prüfen") and `pmt-generator check` render each template once with sample values and report such names.
- A `.yml` file written by `insertSection` or `ownFile` instead of `localization`.
- `for` or `if` without `end`.
- Whitespace: `~}}` before an indented line removes its indentation.
- A `read` pattern without group `key` or `number`.
- Localization fields that are not `multilingual`.
- A `forEach` level that does not exist, or a `block` path that is missing in the base file and has no `skeleton`.
