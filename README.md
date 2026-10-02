# Paradox Modding Tool (PMT)

A Windows tool for modders of **Europa Universalis V**, **Crusader Kings III** and **Europa Universalis IV**. It combines a script editor with game knowledge, search and replace across whole mods, compatibility patches between mods, an error log analyzer, and tools for publishing.

PMT is not affiliated with Paradox Interactive. It reads the game files and your mods; it only writes into a mod folder when you save a file or run an action, and most actions can be undone.

## What PMT does

### Editor
- Two side-by-side workspaces with tabs, syntax highlighting for the Paradox script dialect, bookmarks, notes per file that stay outside your mod files, and a directory overview with name search and bug marks.
- Script suggestions and hover hints from the game's own data: effects, triggers, scopes, modifiers, script values and constants. Hovering a modifier value shows the number the game displays.
- Scope tracking: PMT shows the scope at the cursor and the path that leads there, and warns about scope mistakes. The Scope Finder builds a chain link by link or searches a path from a start scope to a target, with conditions and a final value.
- Whitespace cleanup and script formatting, folder filters, a session that restores your open files, and a start button for the game.
- A template generator that turns an existing game object into a template: you fill in the variable parts, and PMT writes the new objects (for example a series of buildings) together with their localization keys, using counting rules for numbered names and localization profiles learned from the game files.

### Find & Replace
- Find in the current document or in whole folders, with whole-word, case, multi-line and format-insensitive search. Replacing a multi-line block works the same way as replacing a word.
- Result lists with multi-select, copying of single matches, several matches or the whole list, and a box to select parts of a match.
- After a replacement PMT warns when an `OR`, `NOR` or `AND` block is left with one entry or none, and updates the warnings when you save.
- Quick access buttons for your favorite folders, search favorites, and replace lists that apply ordered word replacements to a folder after every game patch.

### Compare and update after a game patch
- Compare two files or two folders side by side, WinMerge style, with an overview bar, difference navigation and taking exactly the marked lines into the other side.
- Old vanilla versions are recorded automatically. After a game update the Update tab shows which of your overriding files changed in vanilla and merges the new vanilla version into your mod: places only vanilla changed are taken, places only you changed stay, places both changed become review cards.
- Patch recipes: repeatable rule patches for a mod (line rules, block copies from vanilla, templates, edits of existing blocks). A preview shows every file change before anything is written, hand edits are kept, and a watcher lists vanilla and mod files your recipe does not cover yet.

### Mod compatibility and compatch
- The compatibility check finds file and key conflicts between mods in your playset by the game's real override rules, including `INJECT:` and `REPLACE:` operators, `replace_path`, load order, and files that are overridden too coarsely. It also tells you where a smaller change would do (`INJECT:` instead of a whole file) and warns about `replace_path` entries that cannot work.
- The compatch creator merges two mods against vanilla: changes that do not overlap are taken automatically, real conflicts become cards with the differences side by side, and PMT writes the smallest possible patch (`INJECT:`, `REPLACE:`, a separate patch file or a merged file). Work states are saved, every decision can be undone, and nothing is written until you confirm.
- Playsets can be imported from the Paradox Launcher database (read only).

### Error log
- Reads the game's `error.log` and groups errors by mod and file, with explanations, filters, jump to the code, suppression and export. Errors are marked in editor tabs and in the explorer, and the game log is watched live while you play.
- Fixes where PMT can do them safely: replace in the editor, rename across your own mods with backup and undo, add a missing mod to the playset.
- Scope traces for runtime errors: PMT inserts a marked debug trace, reads `debug.log` and shows which country, character or location triggered the error.
- Works with the save explorer: evidence from a save for variable and flag messages.

### GUI editor
- Code, designer and split view for `.gui` files, with the game's override order for types and templates, a data helper for GUI data types and expressions such as `[Country.GetCapital.GetName]`, and a condition editor that shows `visible`, `enabled` and `checked` expressions in plain language.
- Building blocks: ready-made GUI parts (windows with tabs, lists, buttons, checkboxes, dropdowns, portraits, flags, progress bars and more) with parameters and repeated parts. You can save your own blocks and share them as packages.

### Saves and multiplayer
- Save explorer for debug saves (Europa Universalis V and the text saves of Crusader Kings III, also compressed ones): variables, flags, characters, titles, houses, dynasties, faiths, cultures and more with their names, search, filters, and a comparison of two saves.
- Desync analysis (Europa Universalis V): reads OOS dumps, pairs host and client, compares game versions, DLCs, mods and state, ranks suspects, checks your mods for script that can cause an out-of-sync in multiplayer, and prepares a report for the Paradox bug form and for mod authors.

### Your mods and publishing
- "My Mods" shows all your mods of all games with thumbnails, Steam subscribers and trend, open bug reports, releases, version and dependency warnings and links.
- Version manager: versions, version locks, snapshots as ZIP files, and a changelog editor. Releases for Steam Workshop and GitHub, with a readiness check before you publish.
- Bug tracker for your mods with GitHub issues as a source.

### Games and languages
- Europa Universalis V, Crusader Kings III and Europa Universalis IV. Each game is described by a profile file, so further games can be added without changing the program.
- Planned: compatibility for more Paradox games, namely Victoria 3, Stellaris and Hearts of Iron IV.
- The interface is English by default and available in German, French, Spanish, Russian, Polish, Brazilian Portuguese and Simplified Chinese. See `TRANSLATING.md` if you want to help translate.

## Installation

### Requirements
- Windows 10 or 11, 64-bit.
- At least one supported game installed: Europa Universalis V, Crusader Kings III or Europa Universalis IV.
- Nothing else. The download includes everything PMT needs, so you do not have to install .NET.

### Install
1. Download the latest ZIP from the [Releases page](https://github.com/flogi1/Paradox-Modding-Tool/releases). The file is named `ParadoxModTool-<version>-win-x64.zip`.
2. Extract the ZIP to a folder you can write to, for example `C:\Users\<you>\Documents\ParadoxModTool`. Do not use `C:\Program Files`: PMT updates itself by replacing the files in its own folder.
3. Start `ParadoxModTool.exe`. If Windows SmartScreen shows a warning, click "More info" and then "Run anyway".
4. Choose your game from the game selection menu. Each game opens in its own window.

### Update
Open Help > "Check for updates". PMT downloads the new version from GitHub, checks its signature and installs it. Help > "Changelog" lists what changed in each version.

### Where PMT keeps your data
Settings, notes, saved work states and caches are stored under `%APPDATA%\ParadoxModTool`. PMT only writes into your mod folders when you save a file or run an action, and most actions can be undone.

### Uninstall
Close PMT and delete its folder. To remove your settings as well, delete `%APPDATA%\ParadoxModTool`.

### Problems
Use Help > "Report a bug in the tool". It opens a new GitHub issue with your program version and system details already filled in.

## Support and license
If PMT saves you time, the Support menu links to GitHub Sponsors and Ko-fi. The license is in the `LICENSE` file.
