# Paradox Modding Tool (PMT)

A Windows tool for modders of **Europa Universalis V**, **Crusader Kings III**, **Victoria 3**, **Europa Universalis IV**, **Stellaris** and **Hearts of Iron IV**. It combines a script editor with game knowledge, search and replace across whole mods, compatibility patches between mods, an error log analyzer, and tools for publishing.

PMT is not affiliated with Paradox Interactive. It reads the game files and your mods; it only writes into a mod folder when you save a file or run an action, and most actions can be undone.

## What PMT does

### Editor
- Two side-by-side workspaces with tabs, syntax highlighting for the Paradox script dialect, bookmarks, notes per file that stay outside your mod files, and a directory overview with name search and bug marks.
- Script suggestions and hover hints from the game's own data: effects, triggers, scopes, modifiers, script values and constants. Hovering a modifier value shows the number the game displays.
- Scope tracking: PMT shows the scope at the cursor and the path that leads there, and warns about scope mistakes. The Scope Finder builds a chain link by link or searches a path from a start scope to a target, with conditions and a final value.
- Whitespace cleanup and script formatting, folder filters, a session that restores your open files, and a start button for the game.
- A template generator that turns an existing game object into a template: you fill in the variable parts, and PMT writes the new objects (for example a series of buildings) together with their localization keys, using counting rules for numbered names and localization profiles learned from the game files.

### Generator packages
- A package turns a form into many mod files at once, for example a whole national idea set: own files, localization in every language, sections in shared files and single entries inside existing blocks. A preview shows every change side by side before anything is written, and existing sets can be read back from a mod and edited.
- Packages are built in a package editor with masks instead of JSON, or handed to an AI of your choice with "Create with AI". Packages for The Idea Variation 2 (national ideas, idea groups, dynamic ideas) ship with PMT.

### Find & Replace
- Find in the current document or in whole folders, with whole-word, case, multi-line and format-insensitive search. Replacing a multi-line block works the same way as replacing a word.
- Result lists with multi-select, copying of single matches, several matches or the whole list, and a box to select parts of a match.
- After a replacement PMT warns when an `OR`, `NOR` or `AND` block is left with one entry or none, and updates the warnings when you save.
- Quick access buttons for your favorite folders, search favorites, and replace lists that apply ordered word replacements to a folder after every game patch.

### Compare and update after a game patch
- Compare two files or two folders side by side, WinMerge style, with an overview bar, difference navigation and taking exactly the marked lines into the other side.
- Old vanilla versions are recorded automatically. After a game update the Update tab shows which of your overriding files changed in vanilla and merges the new vanilla version into your mod: places only vanilla changed are taken, places only you changed stay, places both changed become review cards.
- Patch recipes: repeatable rule patches for a mod (line rules, block copies from vanilla, templates, edits of existing blocks). A preview shows every file change before anything is written, hand edits are kept, and a watcher lists vanilla and mod files your recipe does not cover yet. An AI of your choice can build a recipe from your description ("Create with AI"), and the command-line program pmt-patch.exe lets an AI with file access check and dry-run it.

### Mod compatibility and compatch
- The compatibility check finds file and key conflicts between mods in your playset by the game's real override rules, including `INJECT:` and `REPLACE:` operators, `replace_path`, load order, and files that are overridden too coarsely. It also tells you where a smaller change would do (`INJECT:` instead of a whole file) and warns about `replace_path` entries that cannot work.
- The compatch creator merges two mods against vanilla: changes that do not overlap are taken automatically, real conflicts become cards with the differences side by side, and PMT writes the smallest possible patch (`INJECT:`, `REPLACE:`, a separate patch file or a merged file). Work states are saved, every decision can be undone, and nothing is written until you confirm.
- Playsets can be imported from the Paradox Launcher database into the compatibility set.

### Playsets
- Create, copy, rename and delete the playsets of a game, edit their mods (keyword search, reorder, switch on and off) and DLCs, and start the game with one, for Crusader Kings III, Victoria 3, Europa Universalis IV, Europa Universalis V and Stellaris. Existing launcher playsets can be edited too.
- Right-click a test mod in the mod list to start the game with the current playset plus that mod, or to create a new playset from it.
- Before every change PMT backs up the playset files (the last 10 backups stay, one click restores) and refuses to write while the game, or the Paradox Launcher for launcher games, is running.

### Error log
- Reads the game's `error.log` and groups errors by mod and file, with explanations, filters, jump to the code, suppression and export. Errors are marked in editor tabs and in the explorer, and the game log is watched live while you play.
- Fixes where PMT can do them safely: replace in the editor, rename across your own mods with backup and undo, add a missing mod to the playset.
- Scope traces for runtime errors: PMT inserts a marked debug trace, reads `debug.log` and shows which country, character or location triggered the error.
- Works with the save explorer: evidence from a save for variable and flag messages.

### GUI editor
- Code, designer and split view for `.gui` files, with the game's override order for types and templates, a data helper for GUI data types and expressions such as `[Country.GetCapital.GetName]`, and a condition editor that shows `visible`, `enabled` and `checked` expressions in plain language.
- Building blocks: ready-made GUI parts (windows with tabs, lists, buttons, checkboxes, dropdowns, portraits, flags, progress bars and more) with parameters and repeated parts. You can save your own blocks and share them as packages.
- Jomini preview (Crusader Kings III, Victoria 3, Europa Universalis V): the designer and the inspector lay out `hbox`, `vbox`, `flowcontainer`, grids and lists by rules measured in game (layout policies, spacing, margins, minimum and maximum sizes, invisible children, backgrounds, scroll areas). The toolbar picks the resolution and the UI scale (preset from your game settings), "Show bounds" outlines every element and "Mark overflow" outlines in red what sticks out of its parent or the screen. Elements shown only under a condition (`visible = "[…]"`) are marked and can be hidden one by one from the context menu of the element tree, remembered per file, until "Show all" brings them back. Places where the game reports an error or that were not measured carry a note.
- GUI inspector: "Choose element…" opens the search for types, templates and widgets, which also filters by file path and lists the top-level windows of each file, unnamed ones under their file name, so a whole window such as the one in `gui/window_county_view.gui` opens in the inspector with its preview.
- Jomini texts in game look: the preview draws texts with the game's fonts, localisation, text formats and text icons, measured against the game in all three titles (widths, line heights, word wrap, `fontsize_min` with elide, alignment). "Language" in the toolbar picks the language of the preview texts, preset from your game settings; missing texts come in English. Data bindings such as `[GetPlayer.GetName]` show as marked placeholders.
- Jomini preview with game textures: the designer and the inspector draw the game's images, with 9-slice frames, tiling, mirror, `fittype`, colour and alpha, loaded in the background (DDS including BC7, PNG, JPG). Boxes whose texture is chosen by a data binding, is missing or cannot be decoded show a labelled placeholder; borders, tiling, `fittype`, colour, alpha and frames follow rules measured in all three games; tiling with a border (`corneredtiled` with `spriteborder`), `fittype` on a background, `upframe` on framed buttons, fractional frame sizes from half a pixel and colour or alpha values above 1 (Crusader Kings III shows colours above 1 differently) carry a note. The preview applies the `_show` state (followed through `next`) and `scale` of elements and sits on a dark background in light and dark theme.
- Classic interface files of Hearts of Iron IV and Europa Universalis IV (`interface/*.gui` with sprites from `.gfx`): the designer shows a window in game look, with textures, the game's bitmap fonts and English texts, at your game's resolution and UI scale, following the window and sprite override order measured in each game. Approximations are marked. For editing, a property panel explains each property with a sketch and vanilla examples and edits only the changed value in the text, "Edit for mod" and "Take over into mod" bring a vanilla window into the mod (only changed windows, in an override file that wins by the measured load order) and "Create sprite…" adds sprites to a `.gfx` file of the mod. Checks, scripted GUIs, building blocks, new GUI files and mod compatibility for this format follow later.

### Saves and multiplayer
- Save explorer for debug saves (Europa Universalis V and the text saves of Crusader Kings III, also compressed ones): variables, flags, characters, titles, houses, dynasties, faiths, cultures and more with their names, search, filters, and a comparison of two saves.
- Desync analysis (Europa Universalis V): reads OOS dumps, pairs host and client, compares game versions, DLCs, mods and state, ranks suspects, checks your mods for script that can cause an out-of-sync in multiplayer, and prepares a report for the Paradox bug form and for mod authors.

### Your mods and publishing
- "My Mods" shows all your mods of all games with thumbnails, Steam subscribers and trend, open bug reports, releases, version and dependency warnings and links.
- Version manager: versions, version locks, snapshots as ZIP files, and a changelog editor. Releases for Steam Workshop and GitHub, with a readiness check before you publish.
- Bug tracker for your mods with GitHub issues as a source.

### Games and languages
- Europa Universalis V, Crusader Kings III, Victoria 3, Europa Universalis IV, Stellaris and Hearts of Iron IV. Each game is described by a profile file, so further games can be added without changing the program. Victoria 3 and Stellaris do not have the save explorer yet; Stellaris has no GUI editor yet. Hearts of Iron IV and Europa Universalis IV have the GUI preview and editing for their classic interface format (see GUI editor); the building blocks and the data and condition helpers are made for the Jomini format of the newer games.
- The interface is English by default and available in German, French, Spanish, Russian, Polish, Brazilian Portuguese and Simplified Chinese. See `TRANSLATING.md` if you want to help translate.

## Installation

### Requirements
- Windows 10 or 11, 64-bit.
- At least one supported game installed: Europa Universalis V, Crusader Kings III, Victoria 3, Europa Universalis IV, Stellaris or Hearts of Iron IV.
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
