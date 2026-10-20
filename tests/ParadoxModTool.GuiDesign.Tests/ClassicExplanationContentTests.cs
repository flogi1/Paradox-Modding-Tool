using System.Text.Json;
using System.Text.RegularExpressions;
using ParadoxModTool.GuiDesign.Classic;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Inhalt von <c>profiles/guiexplain/classic.json</c>: jeder Eintrag zweisprachig und belegt, jeder Elementtyp der Palette erklärt, die Fallstricke
/// der Spec vorhanden und (gegen die echten Installationen) jede Eigenschaft mit mehr als 20 Vanilla-Verwendungen erklärt.
/// </summary>
[Collection(nameof(RealClassicGuiTests))]
public sealed class ClassicExplanationContentTests
{
    private static readonly string[] AllowedSketches = { "anchors", "percent", "frames", "nineSlice" };
    private static readonly string[] AllowedSources = { "wiki", "measured", "vanilla", "unsure" };

    private readonly RealClassicWorkspaces _workspaces;
    private readonly ITestOutputHelper _output;

    public ClassicExplanationContentTests(RealClassicWorkspaces workspaces, ITestOutputHelper output)
    {
        _workspaces = workspaces;
        _output = output;
    }

    private static string ShippedPath => Path.Combine(AppContext.BaseDirectory, "profiles", "guiexplain", "classic.json");

    [Fact]
    public void Every_entry_has_de_en_and_a_source()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ShippedPath));
        var failures = new List<string>();
        var number = 0;
        foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            number++;
            var label = $"{number} {Text(entry, "kind")}:{Text(entry, "key")}";
            var text = entry.TryGetProperty("text", out var t) ? t : default;
            foreach (var language in new[] { "de", "en" })
            {
                if (text.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(Text(text, language)))
                {
                    failures.Add($"{label}: Text '{language}' fehlt");
                }
            }

            if (entry.TryGetProperty("pitfalls", out var pitfalls))
            {
                var de = pitfalls.TryGetProperty("de", out var d) ? d.GetArrayLength() : 0;
                var en = pitfalls.TryGetProperty("en", out var e) ? e.GetArrayLength() : 0;
                if (de != en)
                {
                    failures.Add($"{label}: {de} deutsche, {en} englische Fallstricke");
                }
            }

            var sketch = Text(entry, "sketch");
            if (!string.IsNullOrEmpty(sketch) && !AllowedSketches.Contains(sketch))
            {
                failures.Add($"{label}: Skizze '{sketch}' nicht erlaubt");
            }

            var sources = entry.TryGetProperty("sources", out var s) && s.ValueKind == JsonValueKind.Array ? s.EnumerateArray().ToList() : new List<JsonElement>();
            if (sources.Count == 0)
            {
                failures.Add($"{label}: keine Quelle");
            }
            else if (sources.Any(source => Text(source, "kind") == "unsure"))
            {
                // Für den Bericht: ganz unsicher (nur unsure) oder teilweise (ein Detail unbelegt).
                var whole = sources.All(source => Text(source, "kind") == "unsure");
                _output.WriteLine((whole ? "unsicher: " : "teilweise unsicher: ") + label);
            }

            foreach (var source in sources)
            {
                var kind = Text(source, "kind");
                if (!AllowedSources.Contains(kind))
                {
                    failures.Add($"{label}: Quelle '{kind}' unbekannt");
                    continue;
                }

                if (kind != "unsure" && string.IsNullOrWhiteSpace(Text(source, "note")))
                {
                    failures.Add($"{label}: Quelle '{kind}' ohne note");
                }

                if (kind == "measured" && !Regex.IsMatch(Text(source, "date"), @"^\d{4}-\d{2}-\d{2}$"))
                {
                    failures.Add($"{label}: gemessene Quelle ohne Datum der Form JJJJ-MM-TT ('{Text(source, "date")}')");
                }
            }
        }

        foreach (var game in new[] { "hoi4", "eu4" })
        {
            failures.AddRange(ClassicExplanations.Load(ShippedPath, game).Problems.Select(p => game + ": " + p));
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Every_element_type_has_an_entry()
    {
        var missing = new List<string>();
        foreach (var game in new[] { "hoi4", "eu4" })
        {
            var explanations = ClassicExplanations.Load(ShippedPath, game);
            missing.AddRange(ClassicPalette.ElementKeys.Where(k => explanations.Element(k, "de") is null).Select(k => game + ": " + k));
            // Spec: jede Elementerklärung nennt mindestens einen Fallstrick, Container nennen typische Kinder.
            missing.AddRange(ClassicPalette.ElementKeys
                .Where(k => explanations.Element(k, "de") is { Pitfalls.Count: 0 })
                .Select(k => game + ": " + k + " ohne Fallstrick"));
            missing.AddRange(new[] { "containerwindowtype", "windowtype" }
                .Where(k => explanations.Element(k, "de") is { Children.Count: 0 })
                .Select(k => game + ": " + k + " ohne Kinder"));
        }

        Assert.True(missing.Count == 0, "Ohne Erklärung: " + string.Join(", ", missing));
    }

    [Fact]
    public void Sketches_and_topics_as_specified()
    {
        foreach (var game in new[] { "hoi4", "eu4" })
        {
            var explanations = ClassicExplanations.Load(ShippedPath, game);
            var sketches = new[]
            {
                ("orientation", "anchors"), ("origo", "anchors"), ("size", "percent"), ("frame", "frames"), ("noOfFrames", "frames"),
                ("borderSize", "nineSlice"),
            };
            foreach (var (property, sketch) in sketches)
            {
                Assert.Equal(sketch, explanations.Property(property, "de")?.Sketch);
            }

            foreach (var topic in new[] { "anchor-formula", "percent", "frames", "nine-slice", "load-order" })
            {
                Assert.NotNull(explanations.Topic(topic, "de"));
            }
        }
    }

    [Fact]
    public void Container_types_differ_per_game()
    {
        var hoi4 = ClassicExplanations.Load(ShippedPath, "hoi4");
        var eu4 = ClassicExplanations.Load(ShippedPath, "eu4");

        Assert.NotEqual(hoi4.Element("containerWindowType", "de")!.Text, eu4.Element("containerWindowType", "de")!.Text);
        Assert.NotEqual(hoi4.Element("windowType", "de")!.Text, eu4.Element("windowType", "de")!.Text);
    }

    [Fact]
    public void Spec_pitfalls_present()
    {
        foreach (var game in new[] { "hoi4", "eu4" })
        {
            var explanations = ClassicExplanations.Load(ShippedPath, game);
            AssertPitfall(explanations, "orientation", "de", "Kinder erben orientation nicht");
            AssertPitfall(explanations, "orientation", "en", "Children do not inherit orientation");
            AssertPitfall(explanations, "frame", "de", "frame zählt ab 1");
            AssertPitfall(explanations, "frame", "en", "frame counts from 1");
            AssertPitfall(explanations, "size", "de", "N%% füllt bis zum Rand");
            AssertPitfall(explanations, "size", "en", "N%% fills to the edge");
            AssertPitfall(explanations, "orientation", "de", "ohne Rücksicht auf Groß-/Kleinschreibung", "Vanilla-Schreibweisen");
            AssertPitfall(explanations, "orientation", "en", "ignoring case", "vanilla spellings");
        }
    }

    [Fact]
    public void Hoi4_properties_over_20_have_entries() => AssertFrequentPropertiesExplained(_workspaces.Hoi4, "hoi4");

    [Fact]
    public void Eu4_properties_over_20_have_entries() => AssertFrequentPropertiesExplained(_workspaces.Eu4, "eu4");

    private void AssertFrequentPropertiesExplained(BuiltWorkspace? built, string game)
    {
        if (built is null)
        {
            return;
        }

        // Je Eigenschaft (Katalog: schon kleingeschrieben) die Verwendungen über alle Elementtypen.
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var widget in built.Workspace.Catalog.Widgets.Values)
        {
            foreach (var property in widget.Properties.Values)
            {
                counts[property.Name] = counts.GetValueOrDefault(property.Name) + property.Count;
            }
        }

        var frequent = counts.Where(p => p.Value > 20).OrderByDescending(p => p.Value).ToList();
        _output.WriteLine($"{game}: {frequent.Count} Eigenschaften mit mehr als 20 Verwendungen");
        var explanations = ClassicExplanations.Load(ShippedPath, game);
        var missing = frequent.Where(p => explanations.Property(p.Key, "de") is null).Select(p => $"{p.Key} ({p.Value})").ToList();

        Assert.True(missing.Count == 0, $"{game}: {missing.Count} von {frequent.Count} ohne Erklärung: " + string.Join(", ", missing));
    }

    private static void AssertPitfall(ClassicExplanations explanations, string property, string language, params string[] parts)
    {
        var entry = explanations.Property(property, language);
        Assert.NotNull(entry);
        Assert.True(
            entry!.Pitfalls.Any(p => parts.All(part => p.Contains(part, StringComparison.Ordinal))),
            $"{property} ({language}): kein Fallstrick mit '{string.Join("', '", parts)}'");
    }

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
