using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

public class ScriptHintTests
{
    private static readonly DateTime T0 = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    private static ScriptDocEntry E(ScriptDocKind kind, string name, string description = "", params string[] scopes) =>
        new() { Kind = kind, Name = name, Description = description, Scopes = scopes.ToList() };

    private static ScriptDocDatabase Database()
    {
        var db = new ScriptDocDatabase();
        var kinds = Enum.GetValues<ScriptDocKind>().ToList();
        db.ApplyImport(
            new[]
            {
                E(ScriptDocKind.Effect, "add_gold", "Adds gold", "country"),
                E(ScriptDocKind.Trigger, "add_gold_utility", "Utility"),
                E(ScriptDocKind.Effect, "add_prestige"),
                E(ScriptDocKind.Modifier, "gold_income", string.Empty, "Country"),
                E(ScriptDocKind.Trigger, "gold_income", "same name as the modifier"),
                E(ScriptDocKind.Effect, "add_gone", "later removed"),
            },
            kinds,
            T0,
            new Dictionary<string, long>());

        // Zweiter Import ohne "add_gone".
        db.ApplyImport(
            db.Entries.Where(e => e.Name != "add_gone").Select(e => E(e.Kind, e.Name, e.Description, e.Scopes.ToArray())).ToList(),
            kinds,
            T0.AddDays(1),
            new Dictionary<string, long>());
        return db;
    }

    [Fact]
    public void Lookup_ReturnsAllEntriesWithThatName_OrEmpty()
    {
        var index = new ScriptHintIndex(Database());

        Assert.Equal(2, index.Lookup("gold_income").Count);
        Assert.Equal(new[] { ScriptDocKind.Effect }, index.Lookup("add_gold").Select(h => h.Kind));
        Assert.Empty(index.Lookup("nope"));
        Assert.Empty(index.Lookup("ADD_GOLD"));
    }

    [Fact]
    public void Lookup_CarriesDescriptionScopesAndFlags()
    {
        var hint = new ScriptHintIndex(Database()).Lookup("add_gold").Single();

        Assert.Equal("Adds gold", hint.Description);
        Assert.Equal(new[] { "country" }, hint.Scopes);
        Assert.False(hint.IsOutdated);
        Assert.True(new ScriptHintIndex(Database()).Lookup("add_gone").Single().IsOutdated);
    }

    [Fact]
    public void Complete_MatchesPrefixIgnoringCase_CurrentBeforeOutdatedThenAlphabetical()
    {
        var names = new ScriptHintIndex(Database()).Complete("ADD_", 50).Select(h => h.Name).ToList();

        Assert.Equal(new[] { "add_gold", "add_gold_utility", "add_prestige", "add_gone" }, names);
    }

    [Fact]
    public void Complete_RespectsMaxAndEmptyPrefix()
    {
        var index = new ScriptHintIndex(Database());

        Assert.Single(index.Complete("add", 1));
        Assert.Empty(index.Complete(string.Empty, 10));
        Assert.Empty(index.Complete("add", 0));
        Assert.Empty(index.Complete("zzz", 10));
    }

    [Fact]
    public void SetDatabase_ReplacesTheIndex()
    {
        var index = new ScriptHintIndex();
        Assert.Empty(index.Complete("add", 10));

        index.SetDatabase(Database());

        Assert.NotEmpty(index.Complete("add", 10));
    }

    // ---- Wörter

    [Theory]
    [InlineData("    add_gold = 5", 6, 4, 8)]
    [InlineData("    add_gold = 5", 4, 4, 8)]
    [InlineData("    add_gold = 5", 11, 4, 8)]
    [InlineData("root.owner = { }", 7, 5, 5)]
    [InlineData("scope:target", 8, 6, 6)]
    public void WordAt_FindsTheWholeWordAroundTheColumn(string line, int column, int start, int length)
    {
        Assert.Equal((start, length), ScriptWords.WordAt(line, column));
    }

    [Theory]
    [InlineData("    add_gold = 5", 12)]
    [InlineData("    add_gold = 5", 15)]
    [InlineData("x = 1000", 6)]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("abc", -1)]
    public void WordAt_ReturnsNullOutsideWordsAndForNumbers(string line, int column)
    {
        Assert.Null(ScriptWords.WordAt(line, column));
    }

    [Theory]
    [InlineData("    add_g", 9, 4)]
    [InlineData("    add_g", 4, 4)]
    [InlineData("    x = ", 8, 8)]
    [InlineData("a.b", 3, 2)]
    public void PrefixStart_FindsWhereTheWordBeforeTheCaretBegins(string line, int caret, int expected)
    {
        Assert.Equal(expected, ScriptWords.PrefixStart(line, caret));
    }

    [Theory]
    [InlineData("    ", true)]
    [InlineData("    limit = { ", true)]
    [InlineData("    # comment ", false)]
    [InlineData("    x = 1 # comment ", false)]
    [InlineData("    name = \"unclosed ", false)]
    [InlineData("    name = \"closed\" ", true)]
    [InlineData("    name = \"a # b\" ", true)]
    public void IsCodeContext_RejectsCommentsAndOpenStrings(string before, bool expected)
    {
        Assert.Equal(expected, ScriptWords.IsCodeContext(before));
    }

    private static ScriptHintIndex TemplateIndex()
    {
        var db = new ScriptDocDatabase();
        db.ApplyImport(
            new[]
            {
                E(ScriptDocKind.Modifier, "$CULTURE$_opinion", "Culture opinion", "character"),
                E(ScriptDocKind.Modifier, "dynasty_opinion", "Dynasty opinion", "character"),
                E(ScriptDocKind.Modifier, "stationed_$MEN_AT_ARMS_TYPE$_pursuit_add", string.Empty, "province"),
            },
            Enum.GetValues<ScriptDocKind>().ToList(),
            T0,
            new Dictionary<string, long>());
        return new ScriptHintIndex(db);
    }

    [Fact]
    public void Template_matches_filled_word() =>
        Assert.Equal("$CULTURE$_opinion", Assert.Single(TemplateIndex().Lookup("french_opinion")).Name);

    [Fact]
    public void Exact_entry_wins_over_template() =>
        Assert.Equal("dynasty_opinion", Assert.Single(TemplateIndex().Lookup("dynasty_opinion")).Name);

    [Fact]
    public void Placeholder_needs_at_least_one_char() =>
        Assert.Empty(TemplateIndex().Lookup("_opinion"));

    [Fact]
    public void Template_in_the_middle() =>
        Assert.Equal("stationed_$MEN_AT_ARMS_TYPE$_pursuit_add", Assert.Single(TemplateIndex().Lookup("stationed_light_footmen_pursuit_add")).Name);
}
