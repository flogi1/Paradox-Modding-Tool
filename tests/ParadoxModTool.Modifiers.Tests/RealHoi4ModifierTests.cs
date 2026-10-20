using ParadoxModTool.GameProfiles;
using ParadoxModTool.ScriptDocs;
using Xunit;

namespace ParadoxModTool.Modifiers.Tests;

/// <summary>Modifier-Hover mit dem Profil und den echten Dateien von Hearts of Iron IV; ohne Spiel läuft nichts.</summary>
public class RealHoi4ModifierTests
{
    private const string Game = @"C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV";

    [Fact]
    public void Formats_and_names_from_the_real_game()
    {
        var docs = Path.Combine(Game, "documentation", "modifiers_documentation.md");
        if (!File.Exists(docs))
        {
            return;
        }

        var settings = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "hoi4.json")).ModifierFormats!;
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Modifier, File.ReadAllText(docs));
        var decimals = entries.Where(e => e.Extra.ContainsKey("Decimals")).ToDictionary(e => e.Name, e => int.Parse(e.Extra["Decimals"]), StringComparer.OrdinalIgnoreCase);
        var index = ModifierFormatIndex.Build(
            settings, new[] { Game }, Array.Empty<string>(), entries.Select(e => (e.Name, e.Extra.GetValueOrDefault("Category") ?? string.Empty)), decimals);

        var org = index.Find("army_org_factor")!;
        Assert.True(org.Percent);
        Assert.Equal(1, org.Decimals);
        Assert.True(org.Estimated);
        var outcome = index.Find("operation_outcome")!;
        Assert.True(outcome.Percent);
        Assert.Equal(0, outcome.Decimals);
        Assert.False(outcome.Estimated);

        var names = ModifierNameIndex.Build(settings, index.AllNames, new[] { Game }, Array.Empty<string>(), new[] { "english" });
        Assert.False(string.IsNullOrWhiteSpace(names.Find("army_org_factor")));
        Assert.False(string.IsNullOrWhiteSpace(names.Find("army_defence_factor")));
    }
}
