using System.Text.RegularExpressions;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class MenuTextsTests
{
    [Fact]
    public void EveryMenuTextKeyExistsInGermanAndEnglishWithMatchingGermanText()
    {
        var root = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "ParadoxModTool.Settings", "MenuTexts.cs"));
        var german = UiTextTable.Read(Path.Combine(root, "locales", "de.json"));
        var english = UiTextTable.Read(Path.Combine(root, "locales", "en.json"));
        var matches = Regex.Matches(code, "\\b[TF]\\(\"(?<key>menu\\.[^\"]+)\",\\s*\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"");

        Assert.True(matches.Count >= 65, $"nur {matches.Count} Texte gefunden");
        foreach (Match match in matches)
        {
            var key = match.Groups["key"].Value;
            var text = match.Groups["text"].Value.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");
            Assert.True(german.TryGetValue(key, out var de), $"{key} fehlt in locales/de.json");
            Assert.Equal(text, de);
            Assert.True(english.TryGetValue(key, out var en) && en.Length > 0, $"{key} fehlt in locales/en.json");
        }
    }

    [Fact]
    public void NoMenuTextUsesAnEllipsis()
    {
        var root = FindRepositoryRoot();
        foreach (var language in new[] { "de", "en" })
        {
            var table = UiTextTable.Read(Path.Combine(root, "locales", language + ".json"));
            var bad = table.Where(pair => pair.Key.StartsWith("menu.", StringComparison.Ordinal) && (pair.Value.Contains('…') || pair.Value.Contains("...")))
                .Select(pair => $"{language}: {pair.Key}")
                .ToList();
            Assert.Empty(bad);
        }
    }

    [Fact]
    public void MenuNamesFollowTheSpec()
    {
        Assert.Equal("Mod-Projekt", MenuTexts.Project);
        Assert.Equal("Meine Mods", MenuTexts.MyMods);
        Assert.Equal("Compare und Update Tool", MenuTexts.CuTool);
        Assert.StartsWith("Compare und Update Tool: zwei Dateien oder Ordner vergleichen", MenuTexts.CuToolTip);
        Assert.Equal("Patch-Rezepte", MenuTexts.PatchRecipes);
        Assert.StartsWith("Patch-Rezepte: ", MenuTexts.PatchRecipesTip);
        Assert.Equal("Mod-Kompatibilität prüfen", MenuTexts.ModCompat);
        Assert.Equal("Komp-Patch Tool", MenuTexts.Compatch);
        Assert.Equal("Template Generator", MenuTexts.Templates);
    }

    [Fact]
    public void ToolbarLabelsComeFromTheSettingsLabelsWithEmojis()
    {
        Assert.Equal("Template Generator", MenuTexts.ToolbarLabel(ToolbarItemIds.Templates));
        Assert.Equal("Suchen & Ersetzen", MenuTexts.ToolbarLabel(ToolbarItemIds.Search));
        Assert.Equal("🔖 Lesezeichen", MenuTexts.ToolbarLabel(ToolbarItemIds.Bookmark));
        Assert.Equal("🐞 Bug", MenuTexts.ToolbarLabel(ToolbarItemIds.Bug));
        Assert.Equal("📋 Bugtracker", MenuTexts.ToolbarLabel(ToolbarItemIds.BugTracker));
        Assert.Equal("▶ Spiel starten", MenuTexts.ToolbarLabel(ToolbarItemIds.LaunchGame));
    }

    [Fact]
    public void OverviewAndGameModsTexts()
    {
        Assert.Equal("Mod-Übersicht", MenuTexts.Overview);
        Assert.Equal(MenuTexts.MyModsTip, MenuTexts.OverviewTip);
        Assert.Equal("Meine EU V Mods", MenuTexts.MyGameMods("EU V"));
    }

    [Fact]
    public void TileLabelsReuseMenuTexts()
    {
        Assert.Equal("Projekt verwalten", MenuTexts.TileLabel(TileIds.Project));
        Assert.Equal(MenuTexts.ProjectTip, MenuTexts.TileTip(TileIds.Project));
        Assert.Equal(MenuTexts.CuTool, MenuTexts.TileLabel(TileIds.CuTool));
        Assert.Equal(MenuTexts.CompatchTip, MenuTexts.TileTip(TileIds.Compatch));
        Assert.Equal(SettingsTexts.ToolbarItemLabel(ToolbarItemIds.ScopeFinder), MenuTexts.TileLabel(TileIds.ScopeFinder));
        Assert.Equal("GUI", MenuTexts.TileLabel(TileIds.Gui));
        Assert.Equal("Fehlerprotokoll", MenuTexts.TileLabel(TileIds.ErrorLog, id => id == TileIds.ErrorLog ? "Fehlerprotokoll" : null));
        Assert.Equal(TileIds.Desync, MenuTexts.TileLabel(TileIds.Desync));
    }

    [Fact]
    public void EveryTileHasALabelAndATip()
    {
        foreach (var tile in TileCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(MenuTexts.TileLabel(tile.Id, _ => "x")), tile.Id);
            Assert.False(string.IsNullOrWhiteSpace(MenuTexts.TileTip(tile.Id, _ => "x")), tile.Id);
        }
    }

    [Fact]
    public void WorkspaceItemsReuseTheWorkspaceTexts()
    {
        Assert.Equal(MenuTexts.WorkspaceSaveAll, SettingsTexts.ToolbarItemLabel(ToolbarItemIds.SaveAll));
        Assert.Equal("In anderen Arbeitsbereich", SettingsTexts.ToolbarItemLabel(ToolbarItemIds.Move));
        Assert.Equal(MenuTexts.WorkspaceMoveTip, MenuTexts.ToolbarTip(ToolbarItemIds.Move));
        Assert.Equal(MenuTexts.WorkspaceNewTip, MenuTexts.ToolbarTip(ToolbarItemIds.New));
        Assert.Equal("Dateien", SettingsTexts.ToolbarGroupLabel(ToolbarGroup.Files));
    }

    [Fact]
    public void EveryToolbarItemHasItsOwnTip()
    {
        var tips = ToolbarCatalog.All.Select(item => MenuTexts.ToolbarTip(item.Id)).ToList();
        Assert.All(tips, tip => Assert.False(string.IsNullOrWhiteSpace(tip)));
        Assert.Equal(tips.Count, tips.Distinct().Count());
        Assert.Equal(MenuTexts.TemplatesTip, MenuTexts.ToolbarTip(ToolbarItemIds.Templates));
    }

    [Fact]
    public void UndoAndRedoTipsNameTheNextAction()
    {
        Assert.Equal("Rückgängig: Datei gespeichert", MenuTexts.UndoTip("Datei gespeichert"));
        Assert.Equal("Wiederholen: Datei gespeichert", MenuTexts.RedoTip("Datei gespeichert"));
        Assert.Equal(MenuTexts.ToolbarTip(ToolbarItemIds.Undo), MenuTexts.UndoTip(null));
        Assert.Equal(MenuTexts.ToolbarTip(ToolbarItemIds.Redo), MenuTexts.RedoTip(null));
    }

    [Fact]
    public void GameEntriesNameTheGame()
    {
        Assert.Equal("EU5 (dieses Fenster)", MenuTexts.GameThisWindow("EU5"));
        Assert.Equal("EU5 (schon geöffnet)", MenuTexts.GameAlreadyOpen("EU5"));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ParadoxModTool.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository-Wurzel nicht gefunden");
    }
}
