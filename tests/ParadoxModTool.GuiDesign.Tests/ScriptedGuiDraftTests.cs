using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>"Scripted GUI anlegen" und "Effekt anlegen" (HOI IV, Plan 3 Task 6): genaue Texte der neuen Blöcke, Anhängen ohne fremde Änderungen, Probleme.</summary>
public sealed class ScriptedGuiDraftTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-sgdraft-" + Guid.NewGuid().ToString("N"));

    public ScriptedGuiDraftTests()
    {
        Directory.CreateDirectory(Install);
        Directory.CreateDirectory(Mod);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Install => Path.Combine(_root, "install");

    private string Mod => Path.Combine(_root, "My Mod");

    private static string? ReadFile(string path) => File.Exists(path) ? GuiTextReader.Read(path).Text : null;

    private string Put(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private ScriptedGuiIndex Index() =>
        ScriptedGuiIndex.Build(
            new[] { new GuiSourceRoot(Install, GuiLayer.Vanilla, "vanilla"), new GuiSourceRoot(Mod, GuiLayer.Mod, "mod") },
            ClassicWindowIndexView.Empty);

    private static ScriptedGuiRequest TopBar(params string[] buttons) =>
        new("my_window", "my_mod_my_window", ScriptedGuiPlacement.ParentToken, "top_bar", null, null, null, buttons);

    private static ScriptedGuiRequest Category(params string[] buttons) =>
        new("my_window", "my_mod_my_window", ScriptedGuiPlacement.DecisionCategory, null, null, "my_mod_my_window_category", "My Window", buttons);

    private string ModPath(string relative) => Path.Combine(Mod, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Plan_ParentToken_NewFile()
    {
        var plan = ScriptedGuiDraft.Plan(TopBar("ok_button", "close_button"), Mod, Index(), ReadFile);

        Assert.Empty(plan.Problems);
        var file = Assert.Single(plan.Files);
        Assert.Equal(ModPath("common/scripted_guis/my_mod_scripted_guis.txt"), file.FullPath);
        Assert.True(file.IsNew);
        Assert.Equal(
            "scripted_gui = {\n" +
            "\tmy_mod_my_window = {\n" +
            "\t\tcontext_type = player_context\n" +
            "\t\tparent_window_token = top_bar\n" +
            "\t\twindow_name = \"my_window\"\n" +
            "\t\tvisible = { always = yes }\n" +
            "\t\teffects = {\n" +
            "\t\t\tok_button_click = { }\n" +
            "\t\t\tclose_button_click = { }\n" +
            "\t\t}\n" +
            "\t\ttriggers = { }\n" +
            "\t}\n" +
            "}\n",
            file.NewText);
        Assert.Equal(file.FullPath, plan.ScriptPath);
        Assert.Equal(2, plan.FocusLine);
    }

    [Fact]
    public void Plan_OtherContext_WithoutButtons()
    {
        var request = new ScriptedGuiRequest("my_window", "my_ui", ScriptedGuiPlacement.OtherContext, null, "selected_country_context", null, null, Array.Empty<string>());

        var plan = ScriptedGuiDraft.Plan(request, Mod, Index(), ReadFile);

        Assert.Empty(plan.Problems);
        Assert.Equal(
            "scripted_gui = {\n" +
            "\tmy_ui = {\n" +
            "\t\tcontext_type = selected_country_context\n" +
            "\t\twindow_name = \"my_window\"\n" +
            "\t\tvisible = { always = yes }\n" +
            "\t\teffects = { }\n" +
            "\t\ttriggers = { }\n" +
            "\t}\n" +
            "}\n",
            Assert.Single(plan.Files).NewText);
    }

    [Fact]
    public void Plan_DecisionCategory_WritesCategoryAndLoc()
    {
        var plan = ScriptedGuiDraft.Plan(Category("ok_button"), Mod, Index(), ReadFile);

        Assert.Empty(plan.Problems);
        Assert.Equal(3, plan.Files.Count);
        var script = plan.Files.Single(f => f.FullPath == ModPath("common/scripted_guis/my_mod_scripted_guis.txt"));
        Assert.Equal(
            "scripted_gui = {\n" +
            "\tmy_mod_my_window = {\n" +
            "\t\tcontext_type = decision_category\n" +
            "\t\twindow_name = \"my_window\"\n" +
            "\t\tvisible = { always = yes }\n" +
            "\t\teffects = {\n" +
            "\t\t\tok_button_click = { }\n" +
            "\t\t}\n" +
            "\t\ttriggers = { }\n" +
            "\t}\n" +
            "}\n",
            script.NewText);
        var category = plan.Files.Single(f => f.FullPath == ModPath("common/decisions/categories/my_mod_categories.txt"));
        Assert.Equal(
            "my_mod_my_window_category = {\n" +
            "\ticon = GFX_decision_category_generic\n" +
            "\tallowed = { always = yes }\n" +
            "\tvisible_when_empty = yes\n" +
            "\tscripted_gui = my_mod_my_window\n" +
            "}\n",
            category.NewText);
        var loc = plan.Files.Single(f => f.FullPath == ModPath("localisation/english/my_mod_l_english.yml"));
        Assert.Equal(
            "l_english:\n" +
            " my_mod_my_window_category: \"My Window\"\n" +
            " my_mod_my_window_category_desc: \"\"\n",
            loc.NewText);
        Assert.StartsWith("l_english:", loc.NewText);
        Assert.Equal(script.FullPath, plan.ScriptPath);
        Assert.Equal(2, plan.FocusLine);

        // Kodierung beim Schreiben (HOI-IV-Profil): .yml mit BOM wie alle vanilla Lokalisierungsdateien, .txt ohne BOM.
        var profile = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "hoi4.json"));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, ClassicOverridePlanner.WriteEncoding(loc, profile).GetPreamble());
        Assert.Empty(ClassicOverridePlanner.WriteEncoding(script, profile).GetPreamble());
        Assert.Empty(ClassicOverridePlanner.WriteEncoding(category, profile).GetPreamble());
    }

    [Fact]
    public void Plan_GameLanguage_WritesSecondLocFile()
    {
        var plan = ScriptedGuiDraft.Plan(Category(), Mod, Index(), ReadFile, "l_german");

        Assert.Equal(4, plan.Files.Count);
        var german = plan.Files.Single(f => f.FullPath == ModPath("localisation/german/my_mod_l_german.yml"));
        Assert.Equal(
            "l_german:\n" +
            " my_mod_my_window_category: \"My Window\"\n" +
            " my_mod_my_window_category_desc: \"\"\n",
            german.NewText);

        // Englisch als Spielsprache: keine zweite Datei.
        Assert.Equal(3, ScriptedGuiDraft.Plan(Category(), Mod, Index(), ReadFile, "l_english").Files.Count);
    }

    [Fact]
    public void Plan_AppendsToExistingFile_KeepsText()
    {
        const string head =
            "# Kopf\n" +
            "scripted_gui = {\n" +
            "\t# erster\n" +
            "\tother_ui = {\n" +
            "\t\tcontext_type = player_context\n" +
            "\t\twindow_name = \"other\"   # Fenster\n" +
            "\t}\n";
        const string tail = "}\n# Ende\n";
        Put(Mod, "common/scripted_guis/my_mod_scripted_guis.txt", head + tail);
        Put(Mod, "common/decisions/categories/my_mod_categories.txt", "old_category = {\n\tallowed = { always = yes }\n}");
        Put(Mod, "localisation/english/my_mod_l_english.yml", "﻿l_english:\r\n old_key: \"Alt\"\r\n");

        var plan = ScriptedGuiDraft.Plan(Category("ok_button"), Mod, Index(), ReadFile);

        Assert.Empty(plan.Problems);
        var script = plan.Files.Single(f => f.FullPath.EndsWith("my_mod_scripted_guis.txt", StringComparison.Ordinal));
        Assert.False(script.IsNew);
        Assert.Equal(
            head +
            "\tmy_mod_my_window = {\n" +
            "\t\tcontext_type = decision_category\n" +
            "\t\twindow_name = \"my_window\"\n" +
            "\t\tvisible = { always = yes }\n" +
            "\t\teffects = {\n" +
            "\t\t\tok_button_click = { }\n" +
            "\t\t}\n" +
            "\t\ttriggers = { }\n" +
            "\t}\n" +
            tail,
            script.NewText);
        Assert.Equal(8, plan.FocusLine);

        var category = plan.Files.Single(f => f.FullPath.EndsWith("my_mod_categories.txt", StringComparison.Ordinal));
        Assert.Equal(
            "old_category = {\n\tallowed = { always = yes }\n}\n" +
            "\n" +
            "my_mod_my_window_category = {\n" +
            "\ticon = GFX_decision_category_generic\n" +
            "\tallowed = { always = yes }\n" +
            "\tvisible_when_empty = yes\n" +
            "\tscripted_gui = my_mod_my_window\n" +
            "}\n",
            category.NewText);

        // Die Lokalisierung behält ihr Zeilenende (CRLF); das BOM liest GuiTextReader nicht mit.
        var loc = plan.Files.Single(f => f.FullPath.EndsWith("my_mod_l_english.yml", StringComparison.Ordinal));
        Assert.Equal(
            "l_english:\r\n old_key: \"Alt\"\r\n" +
            " my_mod_my_window_category: \"My Window\"\r\n" +
            " my_mod_my_window_category_desc: \"\"\r\n",
            loc.NewText);
    }

    [Fact]
    public void Plan_WithoutScriptedGuiBlock_AppendsNewBlock()
    {
        Put(Mod, "common/scripted_guis/my_mod_scripted_guis.txt", "# nur ein Kommentar");

        var plan = ScriptedGuiDraft.Plan(TopBar(), Mod, Index(), ReadFile);

        var file = Assert.Single(plan.Files);
        Assert.StartsWith("# nur ein Kommentar\n\nscripted_gui = {\n\tmy_mod_my_window = {\n", file.NewText);
        Assert.Equal(4, plan.FocusLine);
    }

    [Fact]
    public void Plan_NameTaken_Problem()
    {
        Put(Install, "common/scripted_guis/vanilla.txt", "scripted_gui = {\n\tmy_mod_my_window = {\n\t\twindow_name = \"vanilla_window\"\n\t}\n}\n");

        var plan = ScriptedGuiDraft.Plan(TopBar(), Mod, Index(), ReadFile);

        Assert.Empty(plan.Files);
        Assert.False(plan.CanApply);
        Assert.Contains(plan.Problems, p => p.Contains("my_mod_my_window", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_WindowHasModEntry_Problem()
    {
        Put(Mod, "common/scripted_guis/other.txt", "scripted_gui = {\n\tother_ui = {\n\t\twindow_name = \"my_window\"\n\t}\n}\n");

        var plan = ScriptedGuiDraft.Plan(TopBar(), Mod, Index(), ReadFile);

        Assert.Empty(plan.Files);
        Assert.Contains(plan.Problems, p => p.Contains("other_ui", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_InvalidIdentifiers_And_CategoryTaken_Problems()
    {
        Put(Install, "common/decisions/categories/vanilla.txt", "my_mod_my_window_category = {\n\ticon = x\n}\n");
        var request = Category() with { EntryName = "bad name" };

        var plan = ScriptedGuiDraft.Plan(request, Mod, Index(), ReadFile);

        Assert.Empty(plan.Files);
        Assert.Contains(plan.Problems, p => p.Contains("bad name", StringComparison.Ordinal));
        Assert.Contains(plan.Problems, p => p.Contains("my_mod_my_window_category", StringComparison.Ordinal));

        var badToken = ScriptedGuiDraft.Plan(TopBar() with { ParentToken = "" }, Mod, Index(), ReadFile);
        Assert.NotEmpty(badToken.Problems);
    }

    [Fact]
    public void DefaultEntryName_IsBaseAndWindow()
    {
        Assert.Equal("my_mod_my_window", ScriptedGuiDraft.DefaultEntryName(Mod, "my_window"));
        Assert.Equal("ParentToken", ScriptedGuiDraft.DefaultPlacement.ToString());
        Assert.Equal("top_bar", ScriptedGuiDraft.DefaultParentToken);
    }

    private ScriptedGuiEntry ModEntry(string body)
    {
        Put(Mod, "common/scripted_guis/my_mod_scripted_guis.txt", body);
        return Assert.Single(Index().Entries);
    }

    [Fact]
    public void AddEffect_InsertsIntoEffects()
    {
        const string text =
            "scripted_gui = {\n" +
            "\tmy_ui = {\n" +
            "\t\twindow_name = \"my_window\"\n" +
            "\t\teffects = {\n" +
            "\t\t\tok_button_click = { add_political_power = 1 }\n" +
            "\t\t}\n" +
            "\t}\n" +
            "}\n";
        var entry = ModEntry(text);

        var plan = ScriptedGuiDraft.AddEffect(entry, "close_button", ReadFile);

        Assert.Empty(plan.Problems);
        var file = Assert.Single(plan.Files);
        Assert.Equal(text.Replace("add_political_power = 1 }\n", "add_political_power = 1 }\n\t\t\tclose_button_click = { }\n"), file.NewText);
        Assert.Equal(entry.FilePath, plan.ScriptPath);
        Assert.Equal(6, plan.FocusLine);
    }

    [Fact]
    public void AddEffect_CreatesEffectsBlock()
    {
        const string text =
            "scripted_gui = {\n" +
            "\tmy_ui = {\n" +
            "\t\twindow_name = \"my_window\"\n" +
            "\t\ttriggers = { }\n" +
            "\t}\n" +
            "}\n";
        var entry = ModEntry(text);

        var plan = ScriptedGuiDraft.AddEffect(entry, "close_button", ReadFile);

        Assert.Empty(plan.Problems);
        Assert.Equal(
            "scripted_gui = {\n" +
            "\tmy_ui = {\n" +
            "\t\twindow_name = \"my_window\"\n" +
            "\t\teffects = {\n" +
            "\t\t\tclose_button_click = { }\n" +
            "\t\t}\n" +
            "\t\ttriggers = { }\n" +
            "\t}\n" +
            "}\n",
            Assert.Single(plan.Files).NewText);
        Assert.Equal(5, plan.FocusLine);
    }

    [Fact]
    public void AddEffect_IntoEmptyEffects()
    {
        var entry = ModEntry("scripted_gui = {\n\tmy_ui = {\n\t\twindow_name = \"my_window\"\n\t\teffects = { }\n\t}\n}\n");

        var plan = ScriptedGuiDraft.AddEffect(entry, "ok", ReadFile);

        Assert.Equal(
            "scripted_gui = {\n\tmy_ui = {\n\t\twindow_name = \"my_window\"\n\t\teffects = {\n\t\t\tok_click = { }\n\t\t}\n\t}\n}\n",
            Assert.Single(plan.Files).NewText);
        Assert.Equal(5, plan.FocusLine);
    }

    [Fact]
    public void AddEffect_ExistingKey_Or_VanillaEntry_Problem()
    {
        var entry = ModEntry("scripted_gui = {\n\tmy_ui = {\n\t\teffects = {\n\t\t\tOK_click = { }\n\t\t}\n\t}\n}\n");

        Assert.NotEmpty(ScriptedGuiDraft.AddEffect(entry, "ok", ReadFile).Problems);
        Assert.NotEmpty(ScriptedGuiDraft.AddEffect(entry with { IsMod = false }, "other", ReadFile).Problems);
        Assert.Empty(ScriptedGuiDraft.AddEffect(entry with { IsMod = false }, "other", ReadFile).Files);
    }

    /// <summary>Das Kategorie-Symbol aus der Probe (Runde 2 im Spiel getestet) gibt es in HOI IV vanilla.</summary>
    [Fact]
    public void CategoryIcon_Exists()
    {
        var game = VanillaGui.For("hoi4");
        if (!game.Exists)
        {
            return;
        }

        var needle = "name = \"" + ScriptedGuiDraft.CategoryIcon + "\"";
        Assert.Contains(
            Directory.EnumerateFiles(Path.Combine(game.InstallDirectory, "interface"), "*.gfx"),
            path => File.ReadAllText(path).Contains(needle, StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_EntryOnDiskButNotYetIndexed_IsTaken()
    {
        // Der Index liest im Hintergrund neu; ein zweiter Klick davor darf keinen doppelten Eintrag schreiben.
        var index = Index();
        var first = ScriptedGuiDraft.Plan(TopBar(), Mod, index, ReadFile);
        Put(Mod, "common/scripted_guis/my_mod_scripted_guis.txt", first.Files[0].NewText);
        Assert.Equal(first.Files[0].FullPath, ModPath("common/scripted_guis/my_mod_scripted_guis.txt"));

        var second = ScriptedGuiDraft.Plan(TopBar(), Mod, index, ReadFile);

        Assert.Empty(second.Files);
        Assert.Contains(GuiTexts.ScriptedGuiNameTaken("my_mod_my_window", "my_mod_scripted_guis.txt"), second.Problems);
        Assert.Contains(GuiTexts.ScriptedGuiWindowHasEntry("my_window", "my_mod_my_window"), second.Problems);
    }

    [Fact]
    public void Plan_CategoryOnDiskButNotYetIndexed_IsTaken()
    {
        var index = Index();
        Put(Mod, "common/decisions/categories/my_mod_categories.txt", "my_mod_my_window_category = {\n\ticon = x\n}\n");

        var plan = ScriptedGuiDraft.Plan(Category(), Mod, index, ReadFile);

        Assert.Empty(plan.Files);
        Assert.Equal(new[] { GuiTexts.ScriptedGuiCategoryTaken("my_mod_my_window_category") }, plan.Problems);
    }
}
