using ParadoxModTool.GuiDesign.Blocks;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiBlockSuggesterRunTests
{
    private static string Button(int i) =>
        $"\tbutton = {{\n\t\ttext = \"A_{i}\"\n\t\tonclick = \"[GetVariableSystem.Set( 'v', 'a_{i}' )]\"\n\t}}\n";

    private static string Buttons => "hbox = {\n" + Button(1) + Button(2) + Button(3) + "}\n";

    private static GuiBlock Compose(GuiBlockSuggestionResult result)
    {
        var block = GuiBlockParser.Parse(GuiBlockDraft.Compose("T", string.Empty, string.Empty, result.Parameters, result.Repeats, result.Body), GuiBlockLayer.Game, null);
        Assert.Empty(block.Errors);
        return block;
    }

    private static string Render(GuiBlock block) => GuiBlockRenderer.Render(block, GuiBlockValues.Defaults(block));

    [Fact]
    public void Suggest_FindsARunOfEqualSiblings()
    {
        var run = Assert.Single(GuiBlockSuggester.Suggest(Buttons), s => s.Kind == GuiBlockSuggestionKind.Repeat);

        Assert.Equal("button", run.Key);
        Assert.Equal(3, run.Count);
        Assert.Equal("3× button", run.Found);
        Assert.Equal(GuiTexts.SuggestRepeat("button"), run.Label);
    }

    [Fact]
    public void Apply_Run_UsesCountingPatternsAndMarkers()
    {
        var run = GuiBlockSuggester.Suggest(Buttons).Where(s => s.Kind == GuiBlockSuggestionKind.Repeat).ToList();

        var result = GuiBlockSuggester.Apply(Buttons, run);

        var lines = result.Body.Split('\n');
        Assert.Contains(lines, l => l.Trim() == "$repeat button$");
        Assert.Contains(lines, l => l.Trim() == "$end button$");
        Assert.Contains("text = \"$button.text$\"", result.Body);
        Assert.Contains(result.Parameters, p => p.Key == "button.text" && p.Default == "A_$index$");
        Assert.Contains(result.Parameters, p => p.Default == "a_$index$" && result.Body.Contains("'v', '$" + p.Key + "$'"));
        Assert.Equal(new[] { new GuiBlockDraftRepeat("button", GuiTexts.SuggestRepeat("button"), 3) }, result.Repeats);
        Assert.Equal(Buttons, Render(Compose(result)));
    }

    [Fact]
    public void Suggest_DifferentStructureOrOneElement_IsNoRun()
    {
        var different = "hbox = {\n" + Button(1) + "\tbutton = {\n\t\ttext = \"B\"\n\t\tsize = { 1 1 }\n\t}\n}\n";
        var single = "hbox = {\n" + Button(1) + "}\n";

        Assert.DoesNotContain(GuiBlockSuggester.Suggest(different), s => s.Kind == GuiBlockSuggestionKind.Repeat);
        Assert.DoesNotContain(GuiBlockSuggester.Suggest(single), s => s.Kind == GuiBlockSuggestionKind.Repeat);
    }

    [Fact]
    public void Suggest_NoRunsWhenMarkersExist()
    {
        var body = "$repeat x$\nwidget = { }\n$end x$\n" + Buttons;

        Assert.DoesNotContain(GuiBlockSuggester.Suggest(body), s => s.Kind == GuiBlockSuggestionKind.Repeat);
    }

    [Fact]
    public void Suggest_NoNestedRuns()
    {
        var inner = string.Concat(Enumerable.Range(1, 3).Select(i => "\t" + Button(i).Replace("\n\t", "\n\t\t", StringComparison.Ordinal)));
        var body = "w = {\n\tvbox = {\n" + inner + "\t}\n\tvbox = {\n" + inner + "\t}\n}\n";

        var run = Assert.Single(GuiBlockSuggester.Suggest(body), s => s.Kind == GuiBlockSuggestionKind.Repeat);

        Assert.Equal("vbox", run.Key);
        Assert.Equal(2, run.Count);
    }

    [Fact]
    public void Apply_UnevenBlankLines_AreDroppedInsideTheRun()
    {
        var body = "hbox = {\n" + Button(1) + "\n" + Button(2) + Button(3) + "}\n";
        var runs = GuiBlockSuggester.Suggest(body).Where(s => s.Kind == GuiBlockSuggestionKind.Repeat).ToList();

        var rendered = Render(Compose(GuiBlockSuggester.Apply(body, runs)));

        Assert.Equal(Buttons, rendered);
    }

    [Fact]
    public void Apply_CommentWithEntryValue_GetsThePlaceholder()
    {
        static string Box(int i) => $"\tvbox = {{\n\t\tvisible = \"[GetVariableSystem.HasValue( 'w', 'tab_{i}' )]\"\n\t\t# Inhalt von tab_{i}\n\t}}\n";
        var body = "w = {\n" + Box(1) + Box(2) + "}\n";
        var runs = GuiBlockSuggester.Suggest(body).Where(s => s.Kind == GuiBlockSuggestionKind.Repeat).ToList();

        var result = GuiBlockSuggester.Apply(body, runs);

        Assert.Contains("# Inhalt von $vbox.id$", result.Body);
        Assert.Equal(body, Render(Compose(result)));
    }

    [Fact]
    public void Suggest_RenderedWindowWithTabs_RoundTripsExactly()
    {
        var library = new GuiBlockLibrary(Path.Combine(AppContext.BaseDirectory, "profiles", "guiblocks", "eu5"), null, null);
        var tabs = library.Find("Fenster mit Tabs")!;
        var body = GuiBlockDraft.FromCode(GuiBlockRenderer.Render(tabs, GuiBlockValues.Defaults(tabs)));

        var suggestions = GuiBlockSuggester.Suggest(body);
        var result = GuiBlockSuggester.Apply(body, suggestions);

        var run = Assert.Single(suggestions, s => s.Kind == GuiBlockSuggestionKind.Repeat);
        Assert.Equal(2, result.Body.Split('\n').Count(l => l.Trim() == "$repeat " + run.Key + "$"));
        Assert.Equal(body, Render(Compose(result)));
    }

    [Fact]
    public void Suggest_VanillaUiLibraryTabs_FindsTheButtonRun()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var file = VanillaGui.Files().Single(f => f.EndsWith(Path.Combine("in_game", "gui", "ui_library.gui"), StringComparison.OrdinalIgnoreCase));
        var document = GuiParser.Parse(File.ReadAllText(file));
        var content = document.Root.Descendants()
            .First(n => n.Name == "header_main_tabs")
            .Children.Single(c => c.Kind == GuiNodeKind.BlockOverride);
        var body = GuiBlockDraft.FromCode(document.TextOf(content));

        var suggestions = GuiBlockSuggester.Suggest(body);

        var run = Assert.Single(suggestions, s => s.Kind == GuiBlockSuggestionKind.Repeat);
        Assert.Equal("7× button_main_tab_alt", run.Found);
        Compose(GuiBlockSuggester.Apply(body, suggestions));
    }
}
