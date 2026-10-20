using ParadoxModTool.GuiDesign.Classic;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Erklärtexte des klassischen Designers aus <c>profiles/guiexplain/classic.json</c>.</summary>
public sealed class ClassicExplanationsTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-guiexplain-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private string Write(string json)
    {
        Directory.CreateDirectory(_temp);
        var path = Path.Combine(_temp, "classic.json");
        File.WriteAllText(path, json);
        return path;
    }

    private const string Orientation =
        """
        { "version": 1, "entries": [ {
          "kind": "property", "key": "orientation", "games": [],
          "text": { "de": "Anker im Elternelement", "en": "Anchor in the parent" },
          "default": "UPPER_LEFT",
          "pitfalls": { "de": [ "Kinder erben orientation nicht." ], "en": [ "Children do not inherit orientation." ] },
          "sketch": "anchors",
          "sources": [ { "kind": "measured", "date": "2026-10-17", "note": "Probe pmt_order_probe_a" } ]
        } ] }
        """;

    [Fact]
    public void Picks_language_with_english_fallback()
    {
        var explanations = ClassicExplanations.Load(Write(Orientation), "hoi4");

        Assert.Empty(explanations.Problems);
        var german = explanations.Property("orientation", "de")!;
        Assert.Equal("Anker im Elternelement", german.Text);
        Assert.Equal("Kinder erben orientation nicht.", Assert.Single(german.Pitfalls));
        var french = explanations.Property("orientation", "fr")!;
        Assert.Equal("Anchor in the parent", french.Text);
        Assert.Equal("Children do not inherit orientation.", Assert.Single(french.Pitfalls));
    }

    [Fact]
    public void Falls_back_to_german_then_first()
    {
        var path = Write(
            """
            { "entries": [
              { "kind": "topic", "key": "a", "text": { "de": "nur deutsch", "ru": "ru" } },
              { "kind": "topic", "key": "b", "text": { "ru": "nur russisch" } }
            ] }
            """);
        var explanations = ClassicExplanations.Load(path, "hoi4");

        Assert.Equal("nur deutsch", explanations.Topic("a", "fr")!.Text);
        Assert.Equal("nur russisch", explanations.Topic("b", "fr")!.Text);
    }

    [Fact]
    public void Reads_all_fields()
    {
        var entry = ClassicExplanations.Load(Write(Orientation), "hoi4").Property("orientation", "en")!;

        Assert.Equal("property", entry.Kind);
        Assert.Equal("orientation", entry.Key);
        Assert.Equal("UPPER_LEFT", entry.Default);
        Assert.Equal("anchors", entry.Sketch);
        var source = Assert.Single(entry.Sources);
        Assert.Equal(new ClassicExplanationSource("measured", "2026-10-17", "Probe pmt_order_probe_a"), source);
        Assert.False(entry.IsUncertain);
        Assert.Empty(entry.Children);
    }

    [Fact]
    public void Game_specific_entry_wins()
    {
        var path = Write(
            """
            { "version": 1, "entries": [
              { "kind": "element", "key": "containerwindowtype", "games": [], "text": { "en": "general" }, "children": [ "iconType" ],
                "sources": [ { "kind": "wiki" } ] },
              { "kind": "element", "key": "containerWindowType", "games": [ "eu4" ], "text": { "en": "eu4 only" }, "children": [ "buttonType" ],
                "sources": [ { "kind": "vanilla" } ] }
            ] }
            """);

        var eu4 = ClassicExplanations.Load(path, "eu4").Element("containerWindowType", "en")!;
        Assert.Equal("eu4 only", eu4.Text);
        Assert.Equal("buttonType", Assert.Single(eu4.Children));
        Assert.Equal("general", ClassicExplanations.Load(path, "hoi4").Element("containerWindowType", "en")!.Text);
    }

    [Fact]
    public void Entry_for_other_game_is_ignored()
    {
        var path = Write("""{ "entries": [ { "kind": "topic", "key": "t", "games": [ "eu4" ], "text": { "en": "x" } } ] }""");

        Assert.Null(ClassicExplanations.Load(path, "hoi4").Topic("t", "en"));
        Assert.NotNull(ClassicExplanations.Load(path, "EU4").Topic("t", "en"));
    }

    [Fact]
    public void Key_lookup_ignores_case()
    {
        var explanations = ClassicExplanations.Load(Write(Orientation), "hoi4");

        Assert.NotNull(explanations.Property("Orientation", "en"));
        Assert.Null(explanations.Element("orientation", "en"));
        Assert.Null(explanations.Topic("orientation", "en"));
    }

    [Fact]
    public void Unsure_only_entry_is_uncertain()
    {
        var path = Write(
            """
            { "entries": [
              { "kind": "property", "key": "a", "text": { "en": "x" }, "sources": [ { "kind": "unsure" } ] },
              { "kind": "property", "key": "b", "text": { "en": "x" }, "sources": [ { "kind": "unsure" }, { "kind": "wiki" } ] },
              { "kind": "property", "key": "c", "text": { "en": "x" } }
            ] }
            """);
        var explanations = ClassicExplanations.Load(path, "hoi4");

        Assert.True(explanations.Property("a", "en")!.IsUncertain);
        Assert.False(explanations.Property("b", "en")!.IsUncertain);
        Assert.True(explanations.Property("c", "en")!.IsUncertain);
    }

    [Fact]
    public void Missing_file_gives_empty_with_problem()
    {
        var explanations = ClassicExplanations.Load(Path.Combine(_temp, "gibt-es-nicht.json"), "hoi4");

        Assert.Single(explanations.Problems);
        Assert.Null(explanations.Property("orientation", "en"));
    }

    [Fact]
    public void Invalid_json_gives_empty_with_problem()
    {
        var explanations = ClassicExplanations.Load(Write("{ not json"), "hoi4");

        Assert.Single(explanations.Problems);
        Assert.Null(explanations.Property("orientation", "en"));
    }

    [Fact]
    public void Invalid_entry_is_skipped_with_problem()
    {
        var path = Write(
            """
            { "entries": [
              { "kind": "nonsense", "key": "a", "text": { "en": "x" } },
              { "kind": "property", "key": "", "text": { "en": "x" } },
              { "kind": "property", "key": "ok", "text": { "en": "fine" }, "sketch": "bogus" }
            ] }
            """);
        var explanations = ClassicExplanations.Load(path, "hoi4");

        Assert.Equal(3, explanations.Problems.Count);
        var ok = explanations.Property("ok", "en")!;
        Assert.Equal("fine", ok.Text);
        Assert.Null(ok.Sketch);
    }

    [Fact]
    public void Empty_instance_has_no_entries()
    {
        Assert.Empty(ClassicExplanations.Empty.Problems);
        Assert.Null(ClassicExplanations.Empty.Property("orientation", "en"));
    }

    [Fact]
    public void Shipped_file_loads_without_problems()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "profiles", "guiexplain", "classic.json");
        foreach (var game in new[] { "hoi4", "eu4" })
        {
            var explanations = ClassicExplanations.Load(path, game);
            Assert.Empty(explanations.Problems);
            Assert.NotNull(explanations.Property("orientation", "de"));
        }
    }
}
