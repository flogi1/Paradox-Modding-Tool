PARADOX MODDING TOOL (PMT) - INSTALLATION

Requirements
  - Windows 10 or 11, 64-bit.
  - At least one supported game installed: Europa Universalis V, Crusader Kings III or Europa Universalis IV.
  - Nothing else. The download includes everything PMT needs, so you do not have to install .NET.

Install
  1. Download the latest ZIP from the Releases page:
     https://github.com/flogi1/Paradox-Modding-Tool/releases
     The file is named ParadoxModTool-<version>-win-x64.zip.
  2. Extract the ZIP to a folder you can write to, for example
     C:\Users\<you>\Documents\ParadoxModTool
     Do not use C:\Program Files: PMT updates itself by replacing the files in its own folder.
  3. Start ParadoxModTool.exe.
     If Windows SmartScreen shows a warning, click "More info" and then "Run anyway".
  4. Choose your game from the game selection menu. Each game opens in its own window.

Update
  Open Help > "Nach Updates suchen" (check for updates). PMT downloads the new version from GitHub, checks its signature and installs it. "Help > Changelog" lists what changed in each version.

Where PMT keeps your data
  Settings, notes, saved work states and caches are stored under %APPDATA%\ParadoxModTool. PMT only writes into your mod folders when you save a file or run an action, and most actions can be undone.

Uninstall
  Close PMT and delete its folder. To remove your settings as well, delete %APPDATA%\ParadoxModTool.

Problems
  Use Help >  (report a bug). It opens a new GitHub issue with your program version and system details already filled in.
