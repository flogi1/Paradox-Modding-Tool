using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// "Custom GUI anlegen" und "In Vanilla-Fenster einbauen" (EU IV, Plan 3 Task 7): Einträge in <c>common/custom_gui</c>, <c>scripted = yes</c> als
/// kleinste Änderung, Lokalisierung flach in <c>localisation/</c>, Übernahme des Gastfensters mit dem klassischen Planer.
/// </summary>
public sealed class CustomGuiDraftTests
{
    private const string ModRoot = @"C:\test\Mod";

    private const string GuiPath = @"C:\test\Mod\interface\mymod.gui";

    private const string CustomPath = @"C:\test\Mod\common\custom_gui\mod_custom_gui.txt";

    private const string LocPath = @"C:\test\Mod\localisation\mod_l_english.yml";

    private const string OverridesPath = @"C:\test\Mod\interface\00_mod_overrides.gui";

    private const string VanillaHost =
        "guiTypes = {\n" +
        "\twindowType = {\n" +
        "\t\tname = \"countrydecisionsview\"\n" +
        "\t\tposition = { x = 0 y = 0 }\n" +
        "\t}\n" +
        "}\n";

    private static readonly SupportedWindow Host = new("countrydecisionsview", "countrydecisionsview", "countrydecisionsview.gui", "root", "from", "countrydecisionsview");

    private static CustomGuiIndex Index() =>
        CustomGuiIndex.Empty.WithWindows(
            CustomGuiIndexTests.Windows(("interface/countrydecisionsview.gui", VanillaHost)),
            new[] { new SupportedWindowSpec("countrydecisionsview", "countrydecisionsview.gui", false, "root", "from") });

    private static Func<string, string?> Files(params (string Path, string Text)[] files) =>
        path => files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)).Text;

    private static GuiNode Named(GuiDocument document, string name) =>
        document.Root.Descendants().First(n => n.Kind == GuiNodeKind.KeyedBlock && ClassicWindows.NameOf(n) == name);

    private const string ButtonGui =
        "guiTypes = {\n" +
        "\twindowType = {\n" +
        "\t\tname = \"countrydecisionsview\"\n" +
        "\t\tguiButtonType = {\n" +
        "\t\t\tname = \"my_button\"\n" +
        "\t\t\tposition = { x = 1 y = 2 }\n" +
        "\t\t}\n" +
        "\t}\n" +
        "}\n";

    private const string ButtonEntry =
        "custom_button = {\n" +
        "\tname = my_button\n" +
        "\tpotential = { always = yes }\n" +
        "\ttrigger = { }\n" +
        "\teffect = { }\n" +
        "\ttooltip = my_button_tt\n" +
        "}\n";

    [Fact]
    public void ForElement_Button_WritesEntryAndScripted()
    {
        var document = GuiParser.Parse(ButtonGui);

        var plan = CustomGuiDraft.ForElement(Named(document, "my_button"), document, GuiPath, ModRoot, Index(), Files());

        Assert.Empty(plan.Problems);
        Assert.True(plan.CanApply);
        Assert.Equal(new[] { GuiPath, CustomPath, LocPath }, plan.Files.Select(f => f.FullPath));
        Assert.Equal(ButtonGui, plan.Files[0].OldText);
        Assert.Equal(ButtonGui.Replace("\t\t\tname = \"my_button\"\n", "\t\t\tname = \"my_button\"\n\t\t\tscripted = yes\n"), plan.Files[0].NewText);
        Assert.True(plan.Files[1].IsNew);
        Assert.Equal(ButtonEntry, plan.Files[1].NewText);
        Assert.True(plan.Files[2].IsNew);
        Assert.Equal("l_english:\n my_button_tt: \"\"\n", plan.Files[2].NewText);
        Assert.Equal(CustomPath, plan.FocusPath);
        Assert.Equal(1, plan.FocusLine);
    }

    [Fact]
    public void ForElement_GameLanguage_WritesFlatLanguageFile()
    {
        var document = GuiParser.Parse(ButtonGui);

        var plan = CustomGuiDraft.ForElement(Named(document, "my_button"), document, GuiPath, ModRoot, Index(), Files(), "l_german");

        // EU IV hat keine Sprachordner: alle Sprachen direkt in localisation/ (vanilla 1.37: 145 *_l_german.yml dort).
        var german = plan.Files.Single(f => f.FullPath == @"C:\test\Mod\localisation\mod_l_german.yml");
        Assert.Equal("l_german:\n my_button_tt: \"\"\n", german.NewText);
    }

    [Fact]
    public void ForElement_Window_WritesChildren()
    {
        var gui =
            "guiTypes = {\n" +
            "\twindowType = {\n" +
            "\t\tname = \"countrydecisionsview\"\n" +
            "\t\twindowType = {\n" +
            "\t\t\tname = \"my_window\"\n" +
            "\t\t\tguiButtonType = {\n" +
            "\t\t\t\tname = \"a_button\"\n" +
            "\t\t\t\tscripted = yes\n" +
            "\t\t\t}\n" +
            "\t\t\tinstantTextBoxType = {\n" +
            "\t\t\t\tname = \"plain_text\"\n" +
            "\t\t\t}\n" +
            "\t\t\ticonType = {\n" +
            "\t\t\t\tname = \"an_icon\"\n" +
            "\t\t\t\tscripted = yes\n" +
            "\t\t\t}\n" +
            "\t\t}\n" +
            "\t}\n" +
            "}\n";
        var document = GuiParser.Parse(gui);

        var plan = CustomGuiDraft.ForElement(Named(document, "my_window"), document, GuiPath, ModRoot, Index(), Files());

        Assert.Empty(plan.Problems);
        Assert.Equal(gui.Replace("\t\t\tname = \"my_window\"\n", "\t\t\tname = \"my_window\"\n\t\t\tscripted = yes\n"), plan.Files[0].NewText);
        Assert.Equal(
            "custom_window = {\n" +
            "\tname = my_window\n" +
            "\tpotential = { always = yes }\n" +
            "}\n" +
            "\n" +
            "custom_button = {\n" +
            "\tname = a_button\n" +
            "\tpotential = { always = yes }\n" +
            "\ttrigger = { }\n" +
            "\teffect = { }\n" +
            "\ttooltip = a_button_tt\n" +
            "}\n" +
            "\n" +
            "custom_icon = {\n" +
            "\tname = an_icon\n" +
            "\tpotential = { always = yes }\n" +
            "\ttooltip = an_icon_tt\n" +
            "}\n",
            plan.Files.Single(f => f.FullPath == CustomPath).NewText);
        Assert.Equal("l_english:\n a_button_tt: \"\"\n an_icon_tt: \"\"\n", plan.Files.Single(f => f.FullPath == LocPath).NewText);
    }

    [Fact]
    public void ForElement_AlreadyScripted_NoTextChange()
    {
        var gui = ButtonGui.Replace("\t\t\tname = \"my_button\"\n", "\t\t\tname = \"my_button\"\n\t\t\tScripted = yes\n");
        var document = GuiParser.Parse(gui);

        var plan = CustomGuiDraft.ForElement(Named(document, "my_button"), document, GuiPath, ModRoot, Index(), Files());

        Assert.Empty(plan.Problems);
        Assert.DoesNotContain(plan.Files, f => f.FullPath == GuiPath);
        Assert.Equal(ButtonEntry, plan.Files.Single(f => f.FullPath == CustomPath).NewText);
    }

    [Fact]
    public void CustomGuiDraft_AppendsToExistingFile_KeepsText()
    {
        var existing = "# mine\r\ncustom_button = {\r\n\tname = old_button\r\n}";
        var loc = "l_english:\r\n old_button_tt: \"Old\"\r\n";
        var document = GuiParser.Parse(ButtonGui);

        var plan = CustomGuiDraft.ForElement(Named(document, "my_button"), document, GuiPath, ModRoot, Index(), Files((CustomPath, existing), (LocPath, loc)));

        Assert.Empty(plan.Problems);
        var custom = plan.Files.Single(f => f.FullPath == CustomPath);
        Assert.Equal(existing, custom.OldText);
        Assert.Equal(existing + "\r\n\r\n" + ButtonEntry.Replace("\n", "\r\n"), custom.NewText);
        Assert.Equal(6, plan.FocusLine);
        Assert.Equal(loc + " my_button_tt: \"\"\r\n", plan.Files.Single(f => f.FullPath == LocPath).NewText);
    }

    [Fact]
    public void ForElement_NameAlreadyInTargetFile_Problem()
    {
        // Der Index liest im Hintergrund neu: Ein zweiter Klick davor darf keinen doppelten Eintrag schreiben.
        var document = GuiParser.Parse(ButtonGui);

        var plan = CustomGuiDraft.ForElement(Named(document, "my_button"), document, GuiPath, ModRoot, Index(), Files((CustomPath, ButtonEntry)));

        Assert.False(plan.CanApply);
        Assert.Contains(GuiTexts.CustomGuiNameTaken("my_button", "mod_custom_gui.txt"), plan.Problems);
        Assert.Empty(plan.Files);
    }

    [Fact]
    public void ForElement_OutsideSupportedWindow_Problem()
    {
        var document = GuiParser.Parse(ButtonGui.Replace("countrydecisionsview", "my_own_view"));

        var plan = CustomGuiDraft.ForElement(Named(document, "my_button"), document, GuiPath, ModRoot, Index(), Files());

        Assert.Contains(GuiTexts.CustomGuiNotInSupportedWindow("my_button"), plan.Problems);
        Assert.Empty(plan.Files);
    }

    [Fact]
    public void ForElement_FileOutsideMod_Problem()
    {
        var document = GuiParser.Parse(ButtonGui);

        var plan = CustomGuiDraft.ForElement(Named(document, "my_button"), document, @"C:\test\Vanilla\interface\x.gui", ModRoot, Index(), Files());

        Assert.Contains(GuiTexts.CustomGuiFileNotMod("x.gui"), plan.Problems);
    }

    private const string ModWindowGui =
        "guiTypes = {\n" +
        "\twindowType = {\n" +
        "\t\tname = \"my_window\"\n" +
        "\t\tposition = { x = 600 y = 300 }\n" +
        "\t\tguiButtonType = {\n" +
        "\t\t\tname = \"my_button\"\n" +
        "\t\t\tscripted = yes\n" +
        "\t\t}\n" +
        "\t}\n" +
        "}\n";

    private const string InsertedChild =
        "\t\twindowType = {\n" +
        "\t\t\tname = \"my_window\"\n" +
        "\t\t\tscripted = yes\n" +
        "\t\t\tposition = { x = 600 y = 300 }\n" +
        "\t\t\tguiButtonType = {\n" +
        "\t\t\t\tname = \"my_button\"\n" +
        "\t\t\t\tscripted = yes\n" +
        "\t\t\t}\n" +
        "\t\t}\n";

    private const string WindowEntries =
        "custom_window = {\n" +
        "\tname = my_window\n" +
        "\tpotential = { always = yes }\n" +
        "}\n" +
        "\n" +
        ButtonEntry;

    private static (GuiIndex Index, GuiModTargets Targets, Func<string, string?> Read) Workspace(params (string Relative, string Text)[] mod)
    {
        var layout = ClassicWindowsTests.LayoutOf("eu4");
        var vanilla = GuiIndexTests.F("interface/countrydecisionsview.gui");
        var files = mod.Select(m => (GuiIndexTests.F(m.Relative, GuiLayer.Mod), m.Text)).Prepend((vanilla, VanillaHost)).ToArray();
        var index = ClassicWindowsTests.Build(layout, files);
        var targets = GuiModTargets.For(ModRoot, firstWins: layout.FirstWins, classic: true);
        return (index, targets, Files(files.Select(f => (f.Item1.FullPath, f.Item2)).ToArray()));
    }

    [Fact]
    public void BuildInto_TakesOverHostIntoZeroZeroFile()
    {
        var (index, targets, read) = Workspace(("interface/mymod.gui", ModWindowGui));
        var document = GuiParser.Parse(ModWindowGui);

        var plan = CustomGuiDraft.BuildInto(Named(document, "my_window"), Host, document, GuiPath, false, index, targets, Index(), read);

        Assert.Empty(plan.Problems);
        Assert.True(plan.CanApply);
        Assert.Empty(plan.Moves);
        Assert.DoesNotContain(plan.Files, f => f.FullPath == GuiPath);
        var overrides = plan.Files.Single(f => f.FullPath == OverridesPath);
        Assert.True(overrides.IsNew);
        Assert.Equal(VanillaHost.Replace("\t\tposition = { x = 0 y = 0 }\n", "\t\tposition = { x = 0 y = 0 }\n" + InsertedChild), overrides.NewText);
        Assert.Equal(WindowEntries, plan.Files.Single(f => f.FullPath == CustomPath).NewText);
        Assert.Equal("l_english:\n my_button_tt: \"\"\n", plan.Files.Single(f => f.FullPath == LocPath).NewText);
        Assert.Equal(CustomPath, plan.FocusPath);
        Assert.Equal(1, plan.FocusLine);
    }

    [Fact]
    public void BuildInto_RemoveOriginal_RemovesTopLevelCopy()
    {
        var (index, targets, read) = Workspace(("interface/mymod.gui", ModWindowGui));
        var document = GuiParser.Parse(ModWindowGui);

        var plan = CustomGuiDraft.BuildInto(Named(document, "my_window"), Host, document, GuiPath, true, index, targets, Index(), read);

        Assert.Empty(plan.Problems);
        Assert.Equal("guiTypes = {\n}\n", plan.Files.Single(f => f.FullPath == GuiPath).NewText);
    }

    [Fact]
    public void BuildInto_HostAlreadyInMod_InsertsOnly()
    {
        var modCopy = "# my copy\n" + VanillaHost.Replace("x = 0 y = 0", "x = 5 y = 0");
        var (index, targets, read) = Workspace(("interface/00_mod_overrides.gui", modCopy), ("interface/mymod.gui", ModWindowGui));
        var document = GuiParser.Parse(ModWindowGui);

        var plan = CustomGuiDraft.BuildInto(Named(document, "my_window"), Host, document, GuiPath, false, index, targets, Index(), read);

        Assert.Empty(plan.Problems);
        var overrides = plan.Files.Single(f => f.FullPath == OverridesPath);
        Assert.Equal(modCopy, overrides.OldText);
        Assert.Equal(modCopy.Replace("\t\tposition = { x = 5 y = 0 }\n", "\t\tposition = { x = 5 y = 0 }\n" + InsertedChild), overrides.NewText);
    }

    [Fact]
    public void BuildInto_RemoveOriginal_SameFileAsHost_OneFile()
    {
        // Mod-Fenster und übernommenes Gastfenster in derselben Datei: ein Plan-Eintrag, beide Änderungen darin.
        var modCopy = VanillaHost + ModWindowGui;
        var (index, targets, read) = Workspace(("interface/00_mod_overrides.gui", modCopy));
        var document = GuiParser.Parse(modCopy);

        var plan = CustomGuiDraft.BuildInto(Named(document, "my_window"), Host, document, OverridesPath, true, index, targets, Index(), read);

        Assert.Empty(plan.Problems);
        var overrides = Assert.Single(plan.Files, f => f.FullPath == OverridesPath);
        Assert.Equal(VanillaHost.Replace("\t\tposition = { x = 0 y = 0 }\n", "\t\tposition = { x = 0 y = 0 }\n" + InsertedChild) + "guiTypes = {\n}\n", overrides.NewText);
    }

    [Fact]
    public void BuildInto_DependencyWins_Warns()
    {
        // Eine Abhängigkeit mit 000_dep.gui lädt vor 00_mod_overrides.gui und gewinnt in EU IV: Das eingebaute Fenster erschiene nicht.
        var layout = ClassicWindowsTests.LayoutOf("eu4");
        var dependency = new GuiSourceFile(@"C:\test\Dep\interface\000_dep.gui", "interface/000_dep.gui", GuiLayer.Dependency, "Dep", 0, 0);
        var files = new[]
        {
            (GuiIndexTests.F("interface/countrydecisionsview.gui"), VanillaHost),
            (dependency, VanillaHost),
            (GuiIndexTests.F("interface/mymod.gui", GuiLayer.Mod), ModWindowGui),
        };
        var index = ClassicWindowsTests.Build(layout, files);
        var targets = GuiModTargets.For(ModRoot, firstWins: layout.FirstWins, classic: true);
        var document = GuiParser.Parse(ModWindowGui);

        var plan = CustomGuiDraft.BuildInto(
            Named(document, "my_window"), Host, document, GuiPath, false, index, targets, Index(), Files(files.Select(f => (f.Item1.FullPath, f.Item2)).ToArray()));

        Assert.Empty(plan.Problems);
        var warning = Assert.Single(plan.Warnings);
        Assert.Contains("000_dep.gui", warning);
    }

    [Fact]
    public void BuildInto_Windows1252Host_NewFileStays1252()
    {
        // countrytechnologyview.gui hat text = "4322 ¤" und ist Windows-1252: als UTF-8 zeigte EU IV "Â¤".
        var host = VanillaHost.Replace("\t\tposition = { x = 0 y = 0 }\n", "\t\tposition = { x = 0 y = 0 }\n\t\ttext = \"4322 ¤\"\n");
        var layout = ClassicWindowsTests.LayoutOf("eu4");
        var vanilla = GuiIndexTests.F("interface/countrydecisionsview.gui");
        var files = new[] { (vanilla, host), (GuiIndexTests.F("interface/mymod.gui", GuiLayer.Mod), ModWindowGui) };
        var index = ClassicWindowsTests.Build(layout, files);
        var targets = GuiModTargets.For(ModRoot, firstWins: layout.FirstWins, classic: true);
        var document = GuiParser.Parse(ModWindowGui);

        var plan = CustomGuiDraft.BuildInto(
            Named(document, "my_window"), Host, document, GuiPath, false, index, targets, Index(), Files(files.Select(f => (f.Item1.FullPath, f.Item2)).ToArray()),
            readEncoding: path => path == vanilla.FullPath ? GuiTextReader.Windows1252 : "utf-8");

        Assert.Empty(plan.Problems);
        var overrides = plan.Files.Single(f => f.FullPath == OverridesPath);
        Assert.Contains("4322 ¤", overrides.NewText);
        Assert.Equal(GuiTextReader.Windows1252, overrides.EncodingName);
    }
    [Fact]
    public void BuildInto_BackgroundSprite_Problem()
    {
        // Runde 1 der Probe stürzte mit backGround = <Sprite> ab; vanilla nennt dort ein Kindelement.
        var gui = ModWindowGui.Replace("\t\tposition = { x = 600 y = 300 }\n", "\t\tposition = { x = 600 y = 300 }\n\t\tbackGround = \"gfx_message_bg\"\n");
        var (index, targets, read) = Workspace(("interface/mymod.gui", gui));
        var document = GuiParser.Parse(gui);

        var plan = CustomGuiDraft.BuildInto(Named(document, "my_window"), Host, document, GuiPath, false, index, targets, Index(), read);

        Assert.Contains(GuiTexts.CustomGuiBackgroundSprite("my_window", "gfx_message_bg"), plan.Problems);
        Assert.Empty(plan.Files);
    }
}
