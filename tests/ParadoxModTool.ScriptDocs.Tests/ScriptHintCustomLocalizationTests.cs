using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

/// <summary>Custom Localizations (Skript-Datenbank, Art CustomLocalization) stehen nur auf Wunsch in den Vorschlägen; der Hover-Tipp kennt sie weiter.</summary>
public class ScriptHintCustomLocalizationTests
{
    private static readonly DateTime T0 = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    private static ScriptHintIndex Index()
    {
        var db = new ScriptDocDatabase();
        db.ApplyImport(
            new[]
            {
                new ScriptDocEntry { Kind = ScriptDocKind.Effect, Name = "get_gold_effect" },
                new ScriptDocEntry { Kind = ScriptDocKind.CustomLocalization, Name = "GetGoldName" },
                new ScriptDocEntry { Kind = ScriptDocKind.Trigger, Name = "get_gold_trigger" },
                new ScriptDocEntry { Kind = ScriptDocKind.CustomLocalization, Name = "get_gold_text" },
            },
            Enum.GetValues<ScriptDocKind>().ToList(),
            T0,
            new Dictionary<string, long>());
        return new ScriptHintIndex(db);
    }

    [Fact]
    public void Completion_includes_custom_localizations_unless_they_are_excluded()
    {
        var index = Index();

        Assert.Contains(ScriptDocKind.CustomLocalization, index.Complete("get_gold", 50).Select(h => h.Kind));
        Assert.DoesNotContain(ScriptDocKind.CustomLocalization, index.Complete("get_gold", 50, includeCustomLocalizations: false).Select(h => h.Kind));
        Assert.Equal(new[] { "get_gold_effect", "get_gold_trigger" }, index.Complete("get_gold", 50, includeCustomLocalizations: false).Select(h => h.Name));
    }

    [Fact]
    public void Excluded_entries_do_not_use_up_the_limit()
    {
        var index = Index();

        Assert.Equal(2, index.Complete("get_gold", 2, includeCustomLocalizations: false).Count);
    }

    [Fact]
    public void Hover_still_knows_custom_localizations()
    {
        Assert.Single(Index().Lookup("GetGoldName"));
    }

}
