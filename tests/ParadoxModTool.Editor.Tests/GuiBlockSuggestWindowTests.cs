using System.Windows.Controls;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class GuiBlockSuggestWindowTests
{
    private const string Code =
        "window = {\n\tname = \"w\"\n\ttext = \"TITLE\"\n\ticon = { texture = \"gfx/a.dds\" }\n" +
        "\thbox = {\n\t\tbutton_main_tab_alt = { text = \"A_1\" }\n\t\tbutton_main_tab_alt = { text = \"A_2\" }\n\t}\n}\n";

    [Fact]
    public void SuggestWindow_SearchAndAllNone() => Sta.Run(() =>
    {
        var window = new GuiBlockSuggestWindow(GuiBlockSuggester.Suggest(Code));
        var total = window.Chosen.Count;
        Assert.True(total >= 4);

        window.SearchText = "tex";
        Assert.All(window.Shown, s => Assert.True(s.Key.Contains("tex", StringComparison.OrdinalIgnoreCase) || s.Label.Contains("tex", StringComparison.OrdinalIgnoreCase) || s.Found.Contains("tex", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(window.Shown, s => s.Key == "texture_1");

        // "Keine" gilt nur für die gezeigten Vorschläge.
        window.SetAll(false);
        Assert.Equal(total - window.Shown.Count, window.Chosen.Count);
        window.SearchText = string.Empty;
        window.SetAll(false);
        Assert.Empty(window.Chosen);
        window.SetAll(true);
        Assert.Equal(total, window.Chosen.Count);
        window.SetChecked("name", false);
        Assert.Equal(total - 1, window.Chosen.Count);
    });

    [Fact]
    public void SuggestWindow_CheckBoxesKeepUnderscores() => Sta.Run(() =>
    {
        var window = new GuiBlockSuggestWindow(GuiBlockSuggester.Suggest(Code)) { Left = -3000, Top = -3000, ShowActivated = false };
        window.Resources.MergedDictionaries.Add(WpfWindows.LoadTheme());
        window.Show();
        WpfWindows.Pump(3);

        var texts = WpfWindows.Descendants<TextBlock>(window).Select(t => t.Text).ToList();
        window.Close();

        Assert.Contains(texts, t => t.Contains("button_main_tab_alt", StringComparison.Ordinal));
    });

    [Fact]
    public void ApplySuggestions_FillsCodeAndRows() => Sta.Run(() =>
    {
        var dialog = new GuiSaveBlockWindow("window = {\n\tname = \"w\"\n}\n", Array.Empty<string>(), true, null);
        dialog.NameBox.Text = "A";
        var body = dialog.CodeEditor.Text;

        dialog.ApplySuggestions(GuiBlockSuggester.Apply(body, GuiBlockSuggester.Suggest(body)));

        Assert.Contains("$name$", dialog.CodeEditor.Text);
        var block = GuiBlockParser.Parse(dialog.ComposedText, GuiBlockLayer.Game, null);
        Assert.Empty(block.Errors);
        Assert.Equal("w", block.Parameters[0].Default);
        Assert.Equal("Name", block.Parameters[0].Label);
        Assert.True(dialog.CanSave);
    });

    [Fact]
    public void ApplySuggestions_WithRepeat_PresetsTheCount() => Sta.Run(() =>
    {
        var dialog = new GuiSaveBlockWindow(Code, Array.Empty<string>(), true, null);
        dialog.NameBox.Text = "A";
        var body = dialog.CodeEditor.Text;

        dialog.ApplySuggestions(GuiBlockSuggester.Apply(body, GuiBlockSuggester.Suggest(body)));

        var block = GuiBlockParser.Parse(dialog.ComposedText, GuiBlockLayer.Game, null);
        Assert.Empty(block.Errors);
        Assert.Equal(2, block.FindRepeat("button_main_tab_alt")!.DefaultCount);
        Assert.Equal(body, GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block)));
    });

    [Fact]
    public void ApplySuggestions_CanBeUndoneAtOnce() => Sta.Run(() =>
    {
        var dialog = new GuiSaveBlockWindow(Code, Array.Empty<string>(), true, null);
        var before = dialog.CodeEditor.Text;

        dialog.ApplySuggestions(GuiBlockSuggester.Apply(before, GuiBlockSuggester.Suggest(before)));
        Assert.NotEqual(before, dialog.CodeEditor.Text);
        dialog.CodeEditor.Undo();

        Assert.Equal(before, dialog.CodeEditor.Text);
    });

    [Fact]
    public void SuggestWindow_KeepsItsSize() => Sta.Run(() =>
        WpfWindows.AssertKeepsItsSizeWhenChoicesAreUsed(new GuiBlockSuggestWindow(GuiBlockSuggester.Suggest(Code))));
}
