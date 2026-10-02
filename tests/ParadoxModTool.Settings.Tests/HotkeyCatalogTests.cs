using System.Text.RegularExpressions;
using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class HotkeyCatalogTests
{
    /// <summary>Vergleiche mit <c>e.Key</c> im Code: <c>==</c>, <c>!=</c>, <c>is X or Y</c>, auch voll qualifiziert.</summary>
    private static readonly Regex KeyComparison = new(
        @"e\.Key\s*(?:==|!=|is)\s*(?:System\.Windows\.Input\.)?Key\.(?<k>\w+)(?:\s+or\s+(?:System\.Windows\.Input\.)?Key\.(?<k>\w+))*");

    private static List<(string File, string Key)> ScanSource()
    {
        var root = FindRepositoryRoot();
        var separator = Path.DirectorySeparatorChar;
        var found = new List<(string, string)>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{separator}obj{separator}") || file.Contains($"{separator}bin{separator}"))
            {
                continue;
            }

            foreach (Match match in KeyComparison.Matches(File.ReadAllText(file)))
            {
                // Key.System ist kein Kürzel: Alt-Kombinationen kommen so an und werden auf SystemKey umgeleitet.
                foreach (Capture capture in match.Groups["k"].Captures.Where(capture => capture.Value != "System"))
                {
                    found.Add((Path.GetFileName(file), capture.Value));
                }
            }
        }

        return found.Distinct().ToList();
    }

    [Fact]
    public void ScanFindsTheKnownShortcuts()
    {
        var found = ScanSource();

        Assert.Contains(("MainWindow.xaml.cs", "F2"), found);
        Assert.Contains(("VanillaDiffWindow.xaml.cs", "H"), found);
        Assert.Contains(("ExplorerPanel.xaml.cs", "F"), found);
        Assert.True(found.Count >= 20, $"nur {found.Count} Tastenvergleiche gefunden");
    }

    [Fact]
    public void CatalogCoversEveryKeyComparisonInTheSource()
    {
        var missing = ScanSource()
            .Where(hit => !HotkeyCatalog.Entries.Any(entry => entry.SourceFile == hit.File && entry.Key == hit.Key))
            .Select(hit => $"{hit.File}: {hit.Key}")
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryEntryHasADescription()
    {
        Assert.All(HotkeyCatalog.Entries, entry => Assert.NotEqual(entry.TextId, SettingsTexts.HotkeyDescription(entry.TextId)));
        Assert.All(HotkeyCatalog.Entries.Select(entry => entry.Group).Distinct(), group => Assert.False(string.IsNullOrWhiteSpace(SettingsTexts.HotkeyGroupLabel(group))));
    }

    [Fact]
    public void GuiBlocksShortcutIsListed()
    {
        Assert.Contains(HotkeyCatalog.Entries, entry => entry.TextId == "main.guiBlocks"
            && entry.Group == HotkeyGroup.Main
            && entry.Modifiers == (HotkeyModifiers.Ctrl | HotkeyModifiers.Shift)
            && entry.Key == "B"
            && entry.SourceFile == "MainWindow.xaml.cs");
        Assert.Equal("GUI-Bausteine öffnen (nur mit eingeschalteten GUI-Werkzeugen)", SettingsTexts.HotkeyDescription("main.guiBlocks"));
    }

    [Fact]
    public void UndoAndRedoComeFirstInTheMainWindowGroup()
    {
        var main = HotkeyCatalog.Entries.Where(entry => entry.Group == HotkeyGroup.Main).ToList();

        Assert.Equal(("Z", HotkeyModifiers.Ctrl), (main[0].Key, main[0].Modifiers));
        Assert.Equal(("Y", HotkeyModifiers.Ctrl), (main[1].Key, main[1].Modifiers));
        Assert.Equal("Hauptfenster und Editor", SettingsTexts.HotkeyGroupLabel(HotkeyGroup.Main));
        Assert.DoesNotContain("Editor", Enum.GetNames<HotkeyGroup>());
    }

    [Fact]
    public void DesignerTreeShortcutsAreListed()
    {
        var designer = HotkeyCatalog.Entries.Where(entry => entry.Group == HotkeyGroup.GuiDesigner).Select(entry => (entry.Modifiers, entry.Key)).ToList();

        Assert.Contains((HotkeyModifiers.None, "Delete"), designer);
        Assert.Contains((HotkeyModifiers.Ctrl, "D"), designer);
        Assert.Contains((HotkeyModifiers.Alt, "Up"), designer);
        Assert.Contains((HotkeyModifiers.Alt, "Down"), designer);
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

        throw new DirectoryNotFoundException("Repository-Wurzel (ParadoxModTool.slnx) nicht gefunden.");
    }

    [Fact]
    public void SaveAllFollowsSave()
    {
        var ids = HotkeyCatalog.Entries.Select(entry => entry.TextId).ToList();
        Assert.Equal(ids.IndexOf("main.save") + 1, ids.IndexOf("main.saveAll"));

        var saveAll = HotkeyCatalog.Entries.Single(entry => entry.TextId == "main.saveAll");
        Assert.Equal(HotkeyGroup.Main, saveAll.Group);
        Assert.Equal(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, saveAll.Modifiers);
        Assert.Equal("S", saveAll.Key);
        Assert.Equal("MainWindow.xaml.cs", saveAll.SourceFile);
    }
}
