using System.Text.RegularExpressions;
using ParadoxModTool.GuiDesign.Blocks;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiShippedBlocksTests
{
    private static string ShippedDirectory(string game = "eu5") => Path.Combine(AppContext.BaseDirectory, "profiles", "guiblocks", game);

    private static IReadOnlyList<GuiBlock> Shipped(string game = "eu5") =>
        new GuiBlockLibrary(ShippedDirectory(game), null, null).Effective.Where(b => !b.IsBuiltIn).ToList();

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    [InlineData("hoi4")]
    [InlineData("eu4")]
    public void ShippedBlocks_LoadRenderAndParse(string game)
    {
        foreach (var block in Shipped(game))
        {
            Assert.True(block.IsValid, $"{block.Name}: {string.Join("; ", block.Errors.Select(e => $"{e.Line}: {e.Message}"))}");
            Assert.Contains(block.Group, GuiBlockLibrary.StandardGroups);
            Assert.Contains("Vorbild: ", block.Description);
            AssertRendersCleanly(block, GuiBlockValues.Defaults(block));
        }
    }

    [Theory]
    [InlineData("eu5", "Fenster", "Fenster", "Fenster mit Tabs", "Scrollbare Liste", "Tab-Leiste")]
    [InlineData("eu5", "Bedienelemente", "Checkbox", "Dropdown", "Knopf mit Icon", "Standard-Knopf", "Suchfeld")]
    [InlineData("eu5", "Anzeige", "Charakterporträt", "Flagge", "Fortschrittsbalken", "Icon mit Tooltip", "Modifier-Tooltip", "Trennlinie", "Überschrift und Text")]
    [InlineData("ck3", "Fenster", "Fenster", "Fenster mit Tabs", "Scrollbare Liste", "Tab-Leiste")]
    [InlineData("ck3", "Bedienelemente", "Checkbox", "Dropdown", "Knopf mit Icon", "Standard-Knopf", "Suchfeld")]
    [InlineData("ck3", "Anzeige", "Charakterporträt", "Fortschrittsbalken", "Icon mit Tooltip", "Modifier-Tooltip", "Trennlinie", "Überschrift und Text", "Wappen")]
    [InlineData("vic3", "Fenster", "Fenster", "Fenster mit Tabs", "Scrollbare Liste", "Tab-Leiste")]
    [InlineData("vic3", "Bedienelemente", "Checkbox", "Dropdown", "Knopf mit Icon", "Standard-Knopf", "Suchfeld")]
    [InlineData("vic3", "Anzeige", "Charakterporträt", "Flagge", "Fortschrittsbalken", "Icon mit Tooltip", "Modifier-Tooltip", "Trennlinie", "Überschrift und Text")]
    [InlineData("hoi4", "Fenster", "Fenster", "Liste", "Scrollbereich")]
    [InlineData("hoi4", "Bedienelemente", "Checkbox", "Knopf mit Text")]
    [InlineData("hoi4", "Anzeige", "Fortschrittsbalken", "Icon mit Tooltip", "Text mit Lokalisierungsschlüssel", "Text mit Variable")]
    [InlineData("eu4", "Fenster", "Fenster", "Liste", "Scrollbereich")]
    [InlineData("eu4", "Bedienelemente", "Checkbox", "Knopf mit Text")]
    [InlineData("eu4", "Anzeige", "Fortschrittsbalken", "Icon mit Tooltip", "Text mit Lokalisierungsschlüssel")]
    public void Group_HasExactlyTheseBlocks(string game, string group, params string[] names)
    {
        Assert.Equal(
            names.OrderBy(n => n, StringComparer.Ordinal),
            Shipped(game).Where(b => b.Group == group).Select(b => b.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    public void ShippedBlocks_AreSixteenPlusEmptyFile(string game)
    {
        Assert.Equal(16, Shipped(game).Count);
        Assert.Equal(1, new GuiBlockLibrary(ShippedDirectory(game), null, null).Effective.Count(b => b.IsBuiltIn));
    }

    [Fact]
    public void ModifierTooltip_OneLinePerModifier()
    {
        var block = Shipped().Single(b => b.Name == "Modifier-Tooltip");
        var mods = block.FindRepeat("mods")!;
        Assert.Equal(1, mods.DefaultCount);
        var key = Assert.Single(mods.Parameters);
        Assert.Equal(GuiBlockParameterKind.Modifier, key.Kind);
        Assert.Equal("discipline", key.Default);

        var values = GuiBlockValues.Defaults(block);
        values.Resize(block, "mods", 2);
        var text = GuiBlockRenderer.Render(block, values);

        Assert.Equal(2, Count(text, "GetModifierTooltipContext"));
        Assert.Contains("[Player.GetModifierTooltipContext('discipline')]", text);
        Assert.Contains("[Player.GetModifierValue('discipline')]", text);
        Assert.Contains("using = CountryModifier_tooltip", text);
    }

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    [InlineData("hoi4")]
    [InlineData("eu4")]
    public void ShippedBlocks_RenderWithZeroAndTwoEntries(string game)
    {
        foreach (var block in Shipped(game))
        {
            foreach (var repeat in block.Repeats)
            {
                foreach (var count in new[] { 0, 2 })
                {
                    var values = GuiBlockValues.Defaults(block);
                    values.Resize(block, repeat.Key, count);
                    AssertRendersCleanly(block, values, $"{repeat.Key} = {count}");
                }
            }
        }
    }

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    public void ShippedBlocks_InsertAsChild(string game)
    {
        foreach (var block in Shipped(game))
        {
            var document = GuiParser.Parse("window = {\n\tvbox = {\n\t}\n}\n");
            var vbox = document.Root.Children[0].Children.Single(c => c.Name == "vbox");
            var code = GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block)) + "\n";

            var edit = Designer.GuiDesignerEdits.Insert(document, new Designer.GuiInsertion(vbox, Edit.InsertPosition.LastChild, null), code);
            var result = GuiParser.Parse(Edit.TextEdit.Apply(document.Text, edit.Edits));

            Assert.True(result.Diagnostics.Count == 0, $"{block.Name}: {string.Join("; ", result.Diagnostics)}");
            Assert.NotEmpty(result.Root.Children[0].Children.Single(c => c.Name == "vbox").Children);
        }
    }

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    [InlineData("hoi4")]
    [InlineData("eu4")]
    public void Requires_AreUsedInBody(string game)
    {
        foreach (var block in Shipped(game))
        {
            // Der ganze Rumpf zählt, auch Abschnitte, die mit den Vorgaben entfallen (Untertabs mit Anzahl 0).
            var text = string.Join("\n", File.ReadAllLines(block.SourcePath!).Where(l => !l.TrimStart().StartsWith("# requires:", StringComparison.Ordinal)));
            foreach (var name in block.Requires)
            {
                Assert.True(Regex.IsMatch(text, @"\b" + Regex.Escape(name) + @"\b"), $"{block.Name}: {name} kommt im Rumpf nicht vor");
            }
        }
    }

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    public void Textures_ExistInVanilla(string game)
    {
        var vanilla = VanillaGui.For(game);
        if (!vanilla.Exists)
        {
            return;
        }

        foreach (var block in Shipped(game))
        {
            var text = GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block));
            foreach (Match match in Regex.Matches(text, "\"(gfx/[^\"]+\\.dds)\""))
            {
                Assert.True(vanilla.GameFileExists(match.Groups[1].Value), $"{block.Name}: {match.Groups[1].Value} fehlt");
            }
        }
    }

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    [InlineData("hoi4")]
    [InlineData("eu4")]
    public void Models_ExistInVanilla(string game)
    {
        var vanilla = VanillaGui.For(game);
        if (!vanilla.Exists)
        {
            return;
        }

        foreach (var block in Shipped(game))
        {
            var models = block.Description[(block.Description.LastIndexOf("Vorbild: ", StringComparison.Ordinal) + "Vorbild: ".Length)..];
            foreach (var model in models.TrimEnd('.').Split(", "))
            {
                // HOI IV und EU IV haben keinen Ordner game/: die Vorbilder liegen direkt im Installationsordner.
                var root = Directory.Exists(vanilla.GameDirectory) ? vanilla.GameDirectory : vanilla.InstallDirectory;
                Assert.True(File.Exists(Path.Combine(root, model)), $"{block.Name}: {model} fehlt");
            }
        }
    }

    private static void AssertRendersCleanly(GuiBlock block, GuiBlockValues values, string what = "Vorgaben")
    {
        var text = GuiBlockRenderer.Render(block, values);
        Assert.False(string.IsNullOrWhiteSpace(text), $"{block.Name} ({what}): leer");
        var diagnostics = GuiParser.Parse(text).Diagnostics;
        Assert.True(diagnostics.Count == 0, $"{block.Name} ({what}): {string.Join("; ", diagnostics)}");
        Assert.DoesNotContain("$", text);
    }

    [Fact]
    public void TabsWithSubtabBar_RendersAndParses()
    {
        var block = Shipped().Single(b => b.Name == "Fenster mit Tabs");
        var values = GuiBlockValues.Defaults(block);
        values.Repeats["tabs"][0].Resize(block, "tabs.sub", 1);

        var text = GuiBlockRenderer.Render(block, values);

        Assert.Empty(GuiParser.Parse(text).Diagnostics);
        Assert.Equal(1, Count(text, "header_secondary_tabs"));
        Assert.Equal(3, Count(text, "button_secondary_tab_alt"));
        Assert.Equal(3, Count(text, "button_main_tab_alt"));
        Assert.Contains("onclick = \"[GetVariableSystem.Set( 'my_window_tab_1', 'tab_1_sub_1' )]\"", text);
        Assert.Contains("on_start = \"[GetVariableSystem.Set( 'my_window_tab', 'tab_1' )]\"", text);
        // 3 Tabs, dazu im ersten Tab das Zurücksetzen des Untertabs und 3 Untertab-Knöpfe.
        Assert.Equal(7, Count(text, "onclick"));

        var plain = GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block));
        Assert.DoesNotContain("header_secondary_tabs", plain);
        Assert.Equal(3, Count(plain, "onclick"));
    }

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    public void TabsWithSubtabBar_SetsTheStartSubtabWhenTheWindowOpens(string game)
    {
        var block = Shipped(game).Single(b => b.Name == "Fenster mit Tabs");
        var values = GuiBlockValues.Defaults(block);
        values.Repeats["tabs"][0].Resize(block, "tabs.sub", 1);

        var text = GuiBlockRenderer.Render(block, values);

        Assert.Contains("on_start = \"[GetVariableSystem.Set( 'my_window_tab_1', 'tab_1_sub_1' )]\"", text);
        Assert.Empty(GuiParser.Parse(text).Diagnostics);
    }

    /// <summary>Victoria 3: divider_clean ist schon 100 % breit; eine Breite 0 ließe die Linie außerhalb von vbox/hbox verschwinden.</summary>
    [Fact]
    public void Vic3_divider_keeps_its_full_width()
    {
        var block = Shipped("vic3").Single(b => b.Name == "Trennlinie");

        Assert.DoesNotContain("size = { 0 2 }", GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block)));
    }

    [Fact]
    public void Insert_RenderedWindowWithTabs_ParsesAndIndents()
    {
        var block = Shipped().Single(b => b.Name == "Fenster mit Tabs");
        var values = GuiBlockValues.Defaults(block);
        values.Repeats["tabs"][0].Resize(block, "tabs.sub", 1);
        var code = GuiBlockRenderer.Render(block, values) + "\nwidget = { }\n";
        var document = GuiParser.Parse("window = {\n\tvbox = {\n\t}\n}\n");
        var vbox = document.Root.Children[0].Children.Single(c => c.Name == "vbox");

        var edit = Designer.GuiDesignerEdits.Insert(document, new Designer.GuiInsertion(vbox, Edit.InsertPosition.LastChild, null), code);
        var text = Edit.TextEdit.Apply(document.Text, edit.Edits);

        Assert.Empty(GuiParser.Parse(text).Diagnostics);
        Assert.Contains("\n\t\twindow = {\n", text);
        Assert.Contains("\n\t\t\t\theader_main_tabs = {\n", text);
        Assert.Contains("\n\t\twidget = { }\n", text);
        var inserted = GuiParser.Parse(text).Root.Children[0].Children.Single(c => c.Name == "vbox").Children.Select(c => c.Name);
        Assert.Equal(new[] { "window", "widget" }, inserted);
    }

    [Theory]
    [InlineData("eu5")]
    [InlineData("ck3")]
    [InlineData("vic3")]
    public void Requires_ExistInVanilla(string game)
    {
        var vanilla = VanillaGui.For(game);
        if (!vanilla.Exists)
        {
            return;
        }

        var texts = vanilla.Files().Select(File.ReadAllText).ToList();
        foreach (var name in Shipped(game).SelectMany(b => b.Requires).Distinct())
        {
            var pattern = new Regex(@"\b(type|template)\s+" + Regex.Escape(name) + @"\b");
            Assert.True(texts.Any(pattern.IsMatch), $"{name} fehlt in Vanilla");
        }
    }

    /// <summary>Victoria 3: Ein Mod-Fenster erscheint über gui/scripted_widgets; sichtbar, solange die Variable besteht, der Schließen-Knopf löscht sie.</summary>
    [Theory]
    [InlineData("Fenster")]
    [InlineData("Fenster mit Tabs")]
    public void Vic3_windows_open_and_close_through_their_variable(string name)
    {
        var block = Shipped("vic3").Single(b => b.Name == name);

        var text = GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block));

        Assert.Contains("visible = \"[GetVariableSystem.Exists( 'my_window' )]\"", text);
        Assert.Matches(@"(?m)^\s*onclick = ""\[GetVariableSystem\.Clear\( 'my_window' \)\]""", text);
        Assert.Contains("scripted_widgets", block.Description);
    }

    /// <summary>Klassisch: HOI IV neun Bausteine (mit "Text mit Variable"), EU IV acht, dazu je "Leere Datei".</summary>
    [Theory]
    [InlineData("hoi4", 9)]
    [InlineData("eu4", 8)]
    public void ClassicShippedBlocks_Count(string game, int count)
    {
        Assert.Equal(count, Shipped(game).Count);
        Assert.Equal(1, new GuiBlockLibrary(ShippedDirectory(game), null, null).Effective.Count(b => b.IsBuiltIn));
    }

    /// <summary>Klassisch: jeder Baustein lässt sich als letztes Kind in ein Fenster unter <c>guiTypes</c> einfügen und parst ohne Diagnose.</summary>
    [Theory]
    [InlineData("hoi4")]
    [InlineData("eu4")]
    public void ClassicShippedBlocks_InsertIntoWindow(string game)
    {
        Assert.NotEmpty(Shipped(game));
        foreach (var block in Shipped(game))
        {
            var document = GuiParser.Parse("guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"host\"\n\t}\n}\n");
            var window = document.Root.Descendants().Single(c => c.Name == "containerWindowType");
            var code = GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block)) + "\n";

            var edit = Designer.GuiDesignerEdits.Insert(document, new Designer.GuiInsertion(window, Edit.InsertPosition.LastChild, null), code);
            var result = GuiParser.Parse(Edit.TextEdit.Apply(document.Text, edit.Edits));

            Assert.True(result.Diagnostics.Count == 0, $"{block.Name}: {string.Join("; ", result.Diagnostics)}");
            Assert.True(result.Root.Descendants().Single(c => c.Name == "containerWindowType" && c.Parent?.Name == "guiTypes").Children.Count > 1, block.Name);
        }
    }

    /// <summary>
    /// Klassisch: Knöpfe nennen den Weg zum Skript (HOI IV "Scripted GUI anlegen", EU IV "Custom GUI anlegen"); EU IV setzt an Knopf, Text und Icon
    /// <c>scripted = yes</c>. Kein Baustein schreibt <c>backGround</c> mit einem Wert (EU IV stürzte mit einem Sprite-Namen dort ab, Probe 2026-10-30).
    /// </summary>
    [Theory]
    [InlineData("hoi4", "Scripted GUI anlegen")]
    [InlineData("eu4", "Custom GUI anlegen")]
    public void ClassicShippedBlocks_NameTheScriptWay(string game, string action)
    {
        Assert.Contains(action, Shipped(game).Single(b => b.Name == "Knopf mit Text").Description);
        var icon = Shipped(game).Single(b => b.Name == "Icon mit Tooltip");
        Assert.Contains("pdx_tooltip = \"MY_ICON_TT\"", GuiBlockRenderer.Render(icon, GuiBlockValues.Defaults(icon)));
        foreach (var block in Shipped(game))
        {
            var text = GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block));
            // Jeder Wert außer einem Block (HOI IV background = { … }), auch ungequotet; leeres "" ist ebenfalls unnötig.
            Assert.DoesNotMatch(@"(?i)\bbackground\s*=\s*(?!\{)\S", text);
            if (game == "eu4" && block.Name is "Knopf mit Text" or "Text mit Lokalisierungsschlüssel" or "Icon mit Tooltip")
            {
                Assert.Matches(@"(?m)^\s*scripted = yes\s*$", text);
            }
        }
    }

    private static int Count(string text, string word) => Regex.Matches(text, @"\b" + Regex.Escape(word) + @"\b").Count;
}
