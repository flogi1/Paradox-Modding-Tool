using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Index der Custom GUI (EU IV, Plan 3 Task 5): Einträge aller Arten, Art ↔ Elementtyp, unterstützte Fenster.</summary>
public sealed class CustomGuiIndexTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-cgui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Install => Path.Combine(_root, "install");

    private string Mod => Path.Combine(_root, "mod");

    private string Put(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private IReadOnlyList<GuiSourceRoot> Roots()
    {
        Directory.CreateDirectory(Install);
        Directory.CreateDirectory(Mod);
        return new[] { new GuiSourceRoot(Install, GuiLayer.Vanilla, "vanilla"), new GuiSourceRoot(Mod, GuiLayer.Mod, "mod") };
    }

    /// <summary>Fenster-Sicht aus (relativer Pfad, Text)-Paaren im EU-IV-Format.</summary>
    internal static ClassicWindowIndexView Windows(params (string Path, string Text)[] files) =>
        Windows(files.Select(f => (f.Path, f.Text, GuiLayer.Vanilla)).ToArray());

    /// <summary>Fenster-Sicht aus (relativer Pfad, Text, Ebene).</summary>
    internal static ClassicWindowIndexView Windows(params (string Path, string Text, GuiLayer Layer)[] files)
    {
        var rules = ClassicLayoutKit.Rules("eu4");
        var index = GuiIndex.FromDocuments(
            files.Select(f => GuiIndexTests.F(f.Path, f.Layer)).ToList(),
            files.Select(f => GuiParser.Parse(f.Text)).ToList(),
            rules.Layout);
        return ClassicWindowIndexView.From(index);
    }

    internal static string Gui(params string[] windows) => "guiTypes = {\n" + string.Concat(windows) + "}\n";

    internal static string Window(string name, params string[] children) =>
        "\twindowType = {\n\t\tname = \"" + name + "\"\n" + string.Concat(children.Select(c => "\t\t" + c + "\n")) + "\t}\n";

    private static SupportedWindowSpec Spec(string doc, string file, bool all = false) => new(doc, file, all, "root", "from");

    [Fact]
    public void Index_ReadsAllKinds()
    {
        Put(Install, "common/custom_gui/z_vanilla.txt", "custom_button = {\n\tname = vanilla_button\n}\n");
        Put(Install, "common/custom_gui/example.txt", "#custom_button = {\n#    name = custom_button_testing\n#}\n");
        var path = Put(Mod, "common/custom_gui/a_mod.txt",
            "Custom_Button = {\n" +
            "\tname = m_button\n" +
            "\tframe = {\n\t\tnumber = 2\n\t\tname = inner\n\t}\n" +
            "}\n" +
            "custom_text_box = { name = \"m_text\" }\n" +
            "custom_icon = { name = m_icon }\n" +
            "custom_shield = { name = m_shield }\n" +
            "custom_window = {\n\tname = m_window\n}\n" +
            "something_else = { name = other }\n");

        var index = CustomGuiIndex.Build(Roots());

        Assert.Equal(
            new[] { "m_button", "m_text", "m_icon", "m_shield", "m_window", "vanilla_button" },
            index.Entries.Select(e => e.Name));
        Assert.Equal(
            new[] { CustomGuiKind.Button, CustomGuiKind.TextBox, CustomGuiKind.Icon, CustomGuiKind.Shield, CustomGuiKind.Window, CustomGuiKind.Button },
            index.Entries.Select(e => e.Kind));
        var button = index.Find("M_BUTTON");
        Assert.NotNull(button);
        Assert.Equal(path, button!.FilePath);
        Assert.Equal(1, button.Line);
        Assert.True(button.IsMod);
        Assert.Equal(11, index.Find("m_window")!.Line);
        Assert.False(index.Find("vanilla_button")!.IsMod);
        Assert.Null(index.Find("inner"));
        Assert.Null(index.Find("custom_button_testing"));
    }

    [Fact]
    public void Index_SameFileInModReplacesVanilla()
    {
        Put(Install, "common/custom_gui/shared.txt", "custom_button = { name = old_button }\n");
        Put(Mod, "common/custom_gui/shared.txt", "custom_button = { name = new_button }\n");

        var index = CustomGuiIndex.Build(Roots());

        var entry = Assert.Single(index.Entries);
        Assert.Equal("new_button", entry.Name);
        Assert.True(entry.IsMod);
    }

    [Fact]
    public void Accepts_Mapping()
    {
        Assert.True(CustomGuiWindows.Accepts(CustomGuiKind.Button, "guiButtonType"));
        Assert.True(CustomGuiWindows.Accepts(CustomGuiKind.Button, "GUIBUTTONTYPE"));
        Assert.False(CustomGuiWindows.Accepts(CustomGuiKind.Button, "buttonType"));
        Assert.False(CustomGuiWindows.Accepts(CustomGuiKind.Button, "iconType"));
        Assert.True(CustomGuiWindows.Accepts(CustomGuiKind.TextBox, "instantTextBoxType"));
        Assert.False(CustomGuiWindows.Accepts(CustomGuiKind.TextBox, "textBoxType"));
        Assert.True(CustomGuiWindows.Accepts(CustomGuiKind.Icon, "iconType"));
        Assert.True(CustomGuiWindows.Accepts(CustomGuiKind.Window, "windowType"));
        Assert.False(CustomGuiWindows.Accepts(CustomGuiKind.Window, "containerWindowType"));
        Assert.True(CustomGuiWindows.Accepts(CustomGuiKind.Shield, "guiButtonType"));
        Assert.True(CustomGuiWindows.Accepts(CustomGuiKind.Shield, "shieldtype"));
        Assert.False(CustomGuiWindows.Accepts(CustomGuiKind.Shield, "iconType"));
    }

    [Fact]
    public void Resolve_TypoMapsToRealWindow()
    {
        var windows = Windows(
            ("interface/countrydiplomacyview.gui", Gui(
                "\tpositionType = { name = \"countrydiploview_scaling\" position = { x = 1 y = 1 } }\n",
                Window("countrydiplomacyview"),
                Window("countrydiplomacyview_shielditem"))),
            ("interface/other.gui", Gui(Window("countrydiplomacyviev"))));

        var resolved = CustomGuiWindows.Resolve(windows, new[] { Spec("countrydiplimacyview", "countrydiplomacyview.gui") });

        var window = Assert.Single(resolved);
        Assert.Equal("countrydiplimacyview", window.DocName);
        Assert.Equal("countrydiplomacyview", window.WindowName);
        Assert.Equal("countrydiplomacyview", window.TopLevelWindow);
        Assert.Equal("countrydiplomacyview.gui", window.File);
    }

    [Fact]
    public void Resolve_ExactBeforeTypoAndNested()
    {
        var windows = Windows(
            ("interface/provinceview.gui", Gui(
                Window("provinceview", "windowType = { name = \"province_window\" }", "windowType = { name = \"state_window\" }"),
                Window("province_windov"))),
            ("interface/countrymissionsview.gui", Gui(Window("CountryMissionsView"), Window("countrymissionsviev"))));

        var resolved = CustomGuiWindows.Resolve(windows, new[]
        {
            Spec("province_window", "provinceview.gui"),
            Spec("countrymissionsview", "countrymissionsview.gui"),
            Spec("missing_window", "countrymissionsview.gui"),
        });

        Assert.Equal(2, resolved.Count);
        Assert.Equal("province_window", resolved[0].WindowName);
        Assert.Equal("provinceview", resolved[0].TopLevelWindow);
        Assert.Equal("CountryMissionsView", resolved[1].WindowName);
    }

    [Fact]
    public void Resolve_FactionExpands()
    {
        var windows = Windows(
            ("interface/faction.gui", Gui(Window("default_faction_view"), Window("merchant_republic_faction_view"), Window("faction_entry"))),
            ("interface/other.gui", Gui(Window("other_faction_view"))));

        var resolved = CustomGuiWindows.Resolve(windows, new[] { Spec("All faction windows", "faction.gui", all: true) });

        Assert.Equal(new[] { "default_faction_view", "merchant_republic_faction_view", "faction_entry" }, resolved.Select(w => w.WindowName));
        Assert.All(resolved, w => Assert.Equal("All faction windows", w.DocName));
    }

    [Fact]
    public void Resolve_AllWindowsAndTypoOnlyFromVanilla()
    {
        var windows = Windows(
            ("interface/faction.gui", Gui(Window("default_faction_view")), GuiLayer.Vanilla),
            ("interface/faction.gui", Gui(Window("mod_faction_view")), GuiLayer.Mod),
            ("interface/countrydiplomacyview.gui", Gui(Window("countrydiplomacyview")), GuiLayer.Vanilla),
            ("interface/countrydiplomacyview.gui", Gui(Window("countrydiplomacyviev")), GuiLayer.Mod));

        var resolved = CustomGuiWindows.Resolve(windows, new[]
        {
            Spec("All faction windows", "faction.gui", all: true),
            Spec("countrydiplimacyview", "countrydiplomacyview.gui"),
        });

        Assert.Equal(new[] { "default_faction_view", "countrydiplomacyview" }, resolved.Select(w => w.WindowName));
    }

    [Fact]
    public void Specs_ShippedFileHas26Entries()
    {
        var specs = CustomGuiWindows.Load(CustomGuiWindows.ShippedPath, "en");

        Assert.Equal(26, specs.Count);
        Assert.Contains(specs, s => s.DocName == "countrydiplimacyview" && s.File == "countrydiplomacyview.gui");
        Assert.Contains(specs, s => s.DocName == "All faction windows" && s.AllWindows && s.File == "faction.gui");
        Assert.All(specs, s => Assert.False(string.IsNullOrWhiteSpace(s.Root)));
        Assert.All(specs, s => Assert.False(string.IsNullOrWhiteSpace(s.From)));
        Assert.Equal(specs.Single(s => s.DocName == "hre_window").Root, CustomGuiWindows.Load(CustomGuiWindows.ShippedPath, "xx").Single(s => s.DocName == "hre_window").Root);
        Assert.NotEqual(specs.Single(s => s.DocName == "hre_window").Root, CustomGuiWindows.Load(CustomGuiWindows.ShippedPath, "de").Single(s => s.DocName == "hre_window").Root);
    }

    [Fact]
    public void Specs_MissingFileGivesEmpty()
    {
        Assert.Empty(CustomGuiWindows.Load(Path.Combine(_root, "nope.json"), "en"));
    }
}
