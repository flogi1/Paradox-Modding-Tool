# Translation prompt for PMT UI locales

You translate the UI of Paradox Modding Tool (PMT), a desktop tool for people who write mods for Paradox Interactive games (Europa
Universalis V, Crusader Kings III, Hearts of Iron IV and others). Its users are modders. They know the games and the English modding
terms.

You will be given a **language code** (for example `fr`), the path of the **LocaleTool** executable, the **repository path** and a
**scratch folder** for temporary files.

## Loop

Repeat until `missing` returns `{}`:

1. Run `LocaleTool missing <code> --max 300 --locales <repo>/locales`. The output is JSON: for each key, the German source text (`de`) and
   the English text (`en`).
2. Translate every entry. Write the result as flat JSON `{"key": "translated text", ...}` to a new file in the scratch folder. Save it as
   UTF-8.
3. Run `LocaleTool merge <code> <chunk file> --locales <repo>/locales`. Exit code 2 means the chunk contains a key that does not exist:
   you changed or invented a key. Fix the chunk and merge again.
4. Run `LocaleTool check <code> --locales <repo>/locales`. For every line starting with `error:`, write a new chunk with only the
   corrected keys and merge it. Lines starting with `hint: long text` are not errors, but shorten those texts if you can do so without
   losing meaning.

Write only through `merge`. Never edit `locales/*.json` or `_glossary.json` directly, and never touch files of other languages.

## Rules

- **Meaning comes from German, wording from English.** German is the source language. Where German is ambiguous, the English text tells
  you what is meant.
- **Placeholders stay exact.** Copy `{0}`, `{1:g}`, `{2:N0}` and so on unchanged. You may move them within the sentence. Keep every `\n`.
- **Never change or invent keys.**
- **Glossary.** Read `locales/_glossary.json` first. Terms under `keep` stay in English. Use the translations under `terms.<code>`. Follow
  `style.<code>` for tone and form of address.
- **Keep it short.** Aim for the length of the English text. Buttons, menu items and column headers have fixed widths.
- **UI conventions.** Use the usual software terms of your language (for example the standard words for Open, Save, Cancel, Settings).
  Keep keyboard key names consistent with how Windows shows them in your language.
- **Consistency.** Translate a recurring term the same way everywhere in the file.

## When you are done

Report:

- the number of keys you translated,
- the final output of `LocaleTool check <code>`,
- terms you translated the same way throughout and that should become fixed entries in `terms.<code>` of the glossary. Only propose them;
  do not edit the glossary.
