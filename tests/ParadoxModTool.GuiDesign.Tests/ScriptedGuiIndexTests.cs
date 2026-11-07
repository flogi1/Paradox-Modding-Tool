using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Index der Scripted GUIs (HOI IV, Plan 3 Task 4): Einträge, Elementnamen nach bekannten Endungen, Einstiegsfenster, Kategorien.</summary>
public sealed class ScriptedGuiIndexTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-sgui-" + Guid.NewGuid().ToString("N"));

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

    /// <summary>Ein oberstes Fenster mit den gegebenen Kindern (je ein Element-Block).</summary>
    internal static string Window(string name, params string[] children) =>
        "\tcontainerWindowType = {\n\t\tname = \"" + name + "\"\n" + string.Concat(children.Select(c => "\t\t" + c + "\n")) + "\t}\n";

    internal static string Gui(params string[] windows) => "guiTypes = {\n" + string.Concat(windows) + "}\n";

    internal static string Button(string name) => "buttonType = { name = \"" + name + "\" }";

    internal static string Icon(string name) => "iconType = { name = \"" + name + "\" }";

    private ScriptedGuiIndex Build(params string[] guiTexts)
    {
        var rules = ClassicLayoutKit.Rules("hoi4");
        var index = GuiIndex.FromDocuments(
            guiTexts.Select((_, i) => GuiIndexTests.F($"interface/file{i}.gui")).ToList(),
            guiTexts.Select(GuiParser.Parse).ToList(),
            rules.Layout);
        Directory.CreateDirectory(Install);
        Directory.CreateDirectory(Mod);
        var roots = new[] { new GuiSourceRoot(Install, GuiLayer.Vanilla, "vanilla"), new GuiSourceRoot(Mod, GuiLayer.Mod, "mod") };
        return ScriptedGuiIndex.Build(roots, ClassicWindowIndexView.From(index), caseSensitive: true);
    }

    private static string Entry(string name, string body) =>
        "scripted_gui = {\n\t" + name + " = {\n" + body + "\t}\n}\n";

    [Fact]
    public void Index_ReadsEntry()
    {
        Put(Install, "common/scripted_guis/a_scripted_guis.txt", Entry("m_ui", "\t\tcontext_type = decision_category\n\t\twindow_name = \"vanilla_window\"\n"));
        Put(Install, "common/scripted_guis/_documentation.md", "scripted_gui = { doc_ui = { window_name = x } }");
        var path = Put(Mod, "common/scripted_guis/m_scripted_guis.txt",
            "scripted_gui = {\n" +
            "\tm_ui = {\n" +
            "\t\tcontext_type = player_context\n" +
            "\t\twindow_name = \"m_window\"\n" +
            "\t\tparent_window_token = top_bar\n" +
            "\t\teffects = {\n" +
            "\t\t\tm_button_click = { }\n" +
            "\t\t}\n" +
            "\t}\n" +
            "}\n");

        var index = Build(Gui(Window("m_window", Button("m_button")), Window("vanilla_window")));

        var entry = Assert.Single(index.Entries);
        Assert.Equal("m_ui", entry.Name);
        Assert.Equal("player_context", entry.ContextType);
        Assert.Equal("m_window", entry.WindowName);
        Assert.Equal("top_bar", entry.ParentToken);
        Assert.Equal(path, entry.FilePath);
        Assert.Equal(2, entry.Line);
        Assert.True(entry.IsMod);
        var binding = Assert.Single(entry.Bindings);
        Assert.Equal(new ScriptedGuiBinding("m_button_click", "m_button", "effects", 7), binding);
        Assert.Same(entry, Assert.Single(index.ForWindow("m_window")));
        Assert.Empty(index.ForWindow("vanilla_window"));
    }

    [Fact]
    public void Index_NestedElementName_Resolves()
    {
        Put(Mod, "common/scripted_guis/m.txt", Entry("m_ui",
            "\t\twindow_name = w\n\t\tproperties = {\n\t\t\tdeep_icon = { frame = 1 }\n\t\t}\n\t\ttriggers = {\n\t\t\tDeep_Icon_visible = { always = yes }\n\t\t}\n"));
        var inner = "containerWindowType = { name = \"inner\" containerWindowType = { name = \"inner2\" " + Icon("deep_icon") + " } }";

        var index = Build(Gui(Window("w", inner)));

        var entry = Assert.Single(index.Entries);
        Assert.Equal(new[] { "deep_icon", "deep_icon" }, entry.Bindings.Select(b => b.ElementName?.ToLowerInvariant()));
        Assert.Equal(new[] { "properties", "triggers" }, entry.Bindings.Select(b => b.Section));
        var found = index.BindingsFor("w", "deep_icon");
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void Index_EntryContainerName_Resolves()
    {
        Put(Mod, "common/scripted_guis/m.txt", Entry("mtg_ui",
            "\t\tcontext_type = decision_category\n" +
            "\t\twindow_name = \"mtg_window\"\n" +
            "\t\tdynamic_lists = {\n" +
            "\t\t\tmembers_grid = {\n" +
            "\t\t\t\tarray = global.members\n" +
            "\t\t\t\tcountry_scope_entry_container = \"mtg_member_entry\"\n" +
            "\t\t\t}\n" +
            "\t\t\tother_grid = {\n" +
            "\t\t\t\tentry_container = mtg_other_entry\n" +
            "\t\t\t}\n" +
            "\t\t}\n" +
            "\t\tproperties = {\n" +
            "\t\t\tmember_flag = { image = \"[This.GetFlag]\" }\n" +
            "\t\t}\n" +
            "\t\teffects = {\n" +
            "\t\t\tother_button_click = { }\n" +
            "\t\t}\n"));

        var index = Build(Gui(
            Window("mtg_window", "gridBoxType = { name = \"members_grid\" }", "gridBoxType = { name = \"other_grid\" }"),
            Window("mtg_member_entry", Icon("member_flag")),
            Window("mtg_other_entry", Button("other_button"))));

        var entry = Assert.Single(index.Entries);
        Assert.Equal(new[] { "mtg_member_entry", "mtg_other_entry" }, entry.EntryContainers);
        Assert.All(entry.Bindings, b => Assert.NotNull(b.ElementName));
        Assert.Equal(new[] { "members_grid", "other_grid", "member_flag", "other_button" }, entry.Bindings.Select(b => b.ElementName));
        Assert.Equal(new[] { "dynamic_lists", "dynamic_lists", "properties", "effects" }, entry.Bindings.Select(b => b.Section));
        Assert.Same(entry, Assert.Single(index.ForWindow("mtg_member_entry")));
        var (found, binding) = Assert.Single(index.BindingsFor("mtg_member_entry", "member_flag"));
        Assert.Same(entry, found);
        Assert.Equal("member_flag", binding.Key);
    }

    [Fact]
    public void Index_SuffixChain_RightAltClick()
    {
        Put(Mod, "common/scripted_guis/m.txt", Entry("m_ui",
            "\t\twindow_name = w\n" +
            "\t\teffects = {\n\t\t\tbtn_right_alt_click = { }\n\t\t\tbtn_shift_control_click = { }\n\t\t\tbtn_middle_click = { }\n\t\t}\n" +
            "\t\ttriggers = {\n\t\t\tbtn_click_enabled = { }\n\t\t\tbtn_visible = { }\n\t\t}\n"));

        var index = Build(Gui(Window("w", Button("btn"))));

        var entry = Assert.Single(index.Entries);
        Assert.Equal(new string?[] { "btn", "btn", null, "btn", "btn" }, entry.Bindings.Select(b => b.ElementName));
    }

    [Fact]
    public void Index_LongestNameFirst()
    {
        Put(Mod, "common/scripted_guis/m.txt", Entry("m_ui",
            "\t\twindow_name = w\n\t\teffects = {\n\t\t\ta_b_click = { }\n\t\t\ta_click = { }\n\t\t}\n"));

        var index = Build(Gui(Window("w", Button("a"), Button("a_b"))));

        Assert.Equal(new[] { "a_b", "a" }, Assert.Single(index.Entries).Bindings.Select(b => b.ElementName));
    }

    [Fact]
    public void Index_BindingValue_Unknown()
    {
        Put(Mod, "common/scripted_guis/m.txt", Entry("m_ui",
            "\t\twindow_name = w\n" +
            "\t\teffects = {\n\t\t\tghost_click = { }\n\t\t\tx_y_click = { }\n\t\t}\n" +
            "\t\tproperties = {\n\t\t\tghost = { frame = 1 }\n\t\t}\n"));

        var index = Build(Gui(Window("w", Button("x"))));

        var entry = Assert.Single(index.Entries);
        Assert.Equal(new[] { "ghost_click", "x_y_click", "ghost" }, entry.Bindings.Select(b => b.Key));
        Assert.All(entry.Bindings, b => Assert.Null(b.ElementName));
    }

    [Fact]
    public void Index_CategoryUsesEntryName()
    {
        Put(Mod, "common/scripted_guis/m.txt", Entry("m_ui", "\t\tcontext_type = decision_category\n\t\twindow_name = \"m_window\"\n"));
        var categories = Put(Mod, "common/decisions/categories/m_categories.txt",
            "# Kategorien\nm_category = {\n\ticon = generic\n\tscripted_gui = m_ui\n}\nother_category = {\n\ticon = generic\n}\n");

        var index = Build(Gui(Window("m_window")));

        var category = Assert.Single(index.Categories);
        Assert.Equal("m_ui", category.Key);
        Assert.Equal("m_category", category.Value.Name);
        Assert.Equal(categories, category.Value.FilePath);
        Assert.Equal(2, category.Value.Line);
        Assert.False(index.Categories.ContainsKey("m_window"));
    }

    [Fact]
    public void Index_LoadOrder_SortsByFileNameAcrossRoots()
    {
        // HOI IV lädt die Dateien aller Wurzeln gemeinsam nach Dateinamen: vanilla zz.txt kommt nach mod aa.txt und gewinnt.
        Put(Install, "common/scripted_guis/zz.txt", Entry("x_ui", "\t\twindow_name = vanilla_w\n"));
        Put(Mod, "common/scripted_guis/aa.txt", Entry("x_ui", "\t\twindow_name = mod_w\n"));

        var index = Build(Gui(Window("vanilla_w"), Window("mod_w")));

        var entry = Assert.Single(index.Entries);
        Assert.Equal("vanilla_w", entry.WindowName);
        Assert.False(entry.IsMod);
    }

    [Fact]
    public void Index_ContainerKey_IgnoresCase()
    {
        Put(Mod, "common/scripted_guis/m.txt", "Scripted_gui = {\n\tm_ui = {\n\t\twindow_name = w\n\t}\n}\n");

        var index = Build(Gui(Window("w")));

        Assert.Equal("m_ui", Assert.Single(index.Entries).Name);
    }

    [Fact]
    public async Task Assets_WithWindows_RebuildsAfterFailedBuild()
    {
        Put(Mod, "common/scripted_guis/m.txt", Entry("m_ui", "\t\twindow_name = w\n"));
        var roots = new[] { new GuiSourceRoot(Install, GuiLayer.Vanilla, "vanilla"), new GuiSourceRoot(Mod, GuiLayer.Mod, "mod") };
        Directory.CreateDirectory(Install);
        var assets = ClassicAssets.Build(roots, Array.Empty<GuiSourceFile>(), spriteFirstWins: false, caseSensitive: true,
            scriptedGuiWindows: () => throw new IOException("kaputt"));
        await Assert.ThrowsAsync<IOException>(() => assets.ScriptedGuis!);

        var view = ClassicWindowIndexView.From(GuiIndex.FromDocuments(
            new[] { GuiIndexTests.F("interface/file0.gui") }, new[] { GuiParser.Parse(Gui(Window("w"))) }, ClassicLayoutKit.Rules("hoi4").Layout));
        var rebuilt = await assets.WithWindows(() => view).ScriptedGuis!;

        Assert.Equal("m_ui", Assert.Single(rebuilt.Entries).Name);
    }
}
