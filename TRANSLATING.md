# Translating Paradox Modding Tool

Paradox Modding Tool (PMT) reads its UI texts from one JSON file per language. You can add a new language or improve an existing one
without touching any code. Test your file in your own copy of PMT, then send it in.

## 1. Get a starting file

- **New language:** copy `locales/en.json` from this repository or from the `locales` folder next to `ParadoxModTool.exe`. Name the
  copy after the language code, for example `fr.json`, `pt-br.json` or `zh-hans.json`.
- **Existing language:** start from its current file in `locales/`.

German (`de.json`) is the source language and is only used when German is chosen. English is the recommended template and the fallback language: missing texts are shown in English.

## 2. Put the file in the translation folder

Open PMT and go to **Settings → Language → Open translation folder**. This opens `%APPDATA%\ParadoxModTool\locales`. Put your file there.

Files in this folder override the bundled ones and survive PMT updates. The bundled `locales` folder next to the exe is replaced on every
update, so do not keep your work there.

## 3. Translate

Each line is a key and a text:

```json
"menu.games.thisWindow": "{0} (this window)",
```

- Translate only the text on the right. Never change the key on the left.
- Keep every placeholder exactly as it is: `{0}`, `{1:g}`, `{2:N0}` and so on. You may move them within the sentence.
- Keep `\n` (line breaks) where they are.
- Keys starting with `_` are comments. Leave them alone, except `_machineTranslated` (see step 5).
- The file must be plain JSON: no comments, and no comma after the last entry. Save it as UTF-8.
- Keep texts short. Many buttons and columns have a fixed width, and long texts get cut off.
- Check `locales/_glossary.json`. Terms under `keep` stay in English (modders use them that way), and `terms` and `style` list agreed
  translations and the tone for your language.
- Key groups for the main window: `menu.*` (menu bar, `menu.tile.*` for the tile bar, `menu.myGameMods` takes the game's short name as
  `{0}`), `settings.category.*` (the categories of the settings window, including `tiles` and `toolbars`),
  `settings.toolbarItem.*` (one entry per button of the small toolbar; the file buttons New, Open, Save, Save all and Close reuse
  `menu.workspace.*`, only `settings.toolbarItem.move` is separate), `settings.toolbarGroup.*` (the groups of the classic toolbar),
  `settings.layout.*` (window layout and toolbar position), `settings.tiles.*` (the tile bar page), `settings.toolbars.*` (the
  toolbars page), `settings.reorder.*` (the reorder list control), `menu.workspace.sort*` (the tab sort menu of a workspace) and
  `editor.workspace.name` (the workspace name in its bar).

## 4. Check your file

**Settings → Language** shows each language with its progress, for example `87 % (312 missing) · user folder`. If the file has problems,
a list below it names them:

- a JSON error with line and position,
- unknown keys (usually a typo in a key),
- placeholders that do not match the original text.

Click **Check again** after saving to update the list. To see your texts in the UI, select your language and restart PMT.

## 5. Machine translations

Some languages ship as machine translations. Their files contain `"_machineTranslated": true`, and Settings shows them as
"machine-translated, corrections welcome". Corrections to these files are very welcome. Remove the line once you have reviewed the whole
file.

## 6. Send it in

Open an issue on GitHub with the **Translation** template and attach your file, or post it on the PMT Discord:
https://discord.gg/EYXpahbFEy. Partial translations are welcome too.
