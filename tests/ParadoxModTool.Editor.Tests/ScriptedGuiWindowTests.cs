using System.IO;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>"Scripted GUI anlegen" (HOI IV, Plan 3 Task 6): Vorgaben aus der Probe im Spiel, Hinweise je Anzeigeart, Probleme sperren "Anlegen".</summary>
public sealed class ScriptedGuiWindowTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-sgui-window-" + Guid.NewGuid().ToString("N"));

    public ScriptedGuiWindowTests() => Directory.CreateDirectory(ModRoot);

    private string ModRoot => Path.Combine(_temp, "My Mod");

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private ScriptedGuiWindow Window(string? gameLanguage = null) =>
        new("my_window", new[] { "ok_button", "close_button" }, ModRoot, ScriptedGuiIndex.Empty, gameLanguage);

    [Fact]
    public void Dialog_DefaultsFromProbe()
    {
        Sta.Run(() =>
        {
            var window = Window();

            // Runde 1 der Probe (2026-10-30): player_context mit top_bar zeigt das Fenster, der _click-Effekt wirkt.
            Assert.Equal(ScriptedGuiPlacement.ParentToken, window.Placement);
            Assert.Equal("top_bar", window.ParentToken);
            Assert.Equal("my_mod_my_window", window.EntryName);
            Assert.Contains(GuiTexts.ScriptedGuiHintTopBar, window.HintText);
            Assert.Empty(window.ShownProblems);
            Assert.True(window.CreateButton.IsEnabled);

            Assert.True(window.TryCreate());
            Assert.Equal(new[] { "ok_button", "close_button" }, window.Request!.ButtonNames);
            Assert.Equal("top_bar", window.Request.ParentToken);
            Assert.True(window.Plan!.CanApply);
        });
    }

    [Fact]
    public void Dialog_OtherContext_ShowsHint()
    {
        Sta.Run(() =>
        {
            var window = Window();
            window.Placement = ScriptedGuiPlacement.OtherContext;

            Assert.Contains(GuiTexts.ScriptedGuiHintOther, window.HintText);
            Assert.DoesNotContain(GuiTexts.ScriptedGuiHintTopBar, window.HintText);
            Assert.Equal("selected_country_context", window.ContextType);
            Assert.True(window.TryCreate());
            Assert.Equal("selected_country_context", window.Request!.ContextType);
            Assert.Null(window.Request.ParentToken);
        });
    }

    [Fact]
    public void Dialog_Category_DefaultsAndLanguageHint()
    {
        Sta.Run(() =>
        {
            var window = Window();
            window.Placement = ScriptedGuiPlacement.DecisionCategory;

            Assert.Equal("my_mod_my_window_category", window.CategoryId);
            Assert.Equal("my_window", window.CategoryName);
            Assert.Contains(GuiTexts.ScriptedGuiHintCategory, window.HintText);
            Assert.Contains(GuiTexts.ScriptedGuiEnglishOnly, window.HintText);
            Assert.True(window.TryCreate());
            Assert.Equal(3, window.Plan!.Files.Count);

            var german = Window("l_german");
            german.Placement = ScriptedGuiPlacement.DecisionCategory;
            Assert.DoesNotContain(GuiTexts.ScriptedGuiEnglishOnly, german.HintText);
            Assert.Contains(GuiTexts.ScriptedGuiLanguageToo("l_german"), german.HintText);
            Assert.True(german.TryCreate());
            Assert.Equal(4, german.Plan!.Files.Count);
        });
    }

    [Fact]
    public void Dialog_InvalidName_DisablesCreate()
    {
        Sta.Run(() =>
        {
            var window = Window();
            window.EntryName = "bad name";

            Assert.Contains(window.ShownProblems, p => p.Contains("bad name", StringComparison.Ordinal));
            Assert.False(window.CreateButton.IsEnabled);
            Assert.False(window.TryCreate());

            window.EntryName = "good_name";

            Assert.Empty(window.ShownProblems);
            Assert.True(window.CreateButton.IsEnabled);
        });
    }
}
