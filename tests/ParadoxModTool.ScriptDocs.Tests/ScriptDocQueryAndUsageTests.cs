using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

public sealed class ScriptDocQueryAndUsageTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-usage-").FullName;

    private static readonly DateTime T0 = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    private static ScriptDocDatabase SampleDatabase()
    {
        var db = new ScriptDocDatabase();
        db.ApplyImport(
            new[]
            {
                new ScriptDocEntry { Kind = ScriptDocKind.Trigger, Name = "add_gold_utility", Description = "Utility of gold", Scopes = { "country" } },
                new ScriptDocEntry { Kind = ScriptDocKind.Effect, Name = "add_gold", Description = "Adds gold", Scopes = { "country" } },
                new ScriptDocEntry { Kind = ScriptDocKind.Effect, Name = "set_gold", Description = "Sets the amount of gold", Scopes = { "country", "international_organization" } },
                new ScriptDocEntry { Kind = ScriptDocKind.Modifier, Name = "gold_income", Description = string.Empty, Scopes = { "Country", "InternationalOrganization" } },
                new ScriptDocEntry { Kind = ScriptDocKind.Effect, Name = "add_prestige", Description = "Adds prestige, not gold related", Scopes = { "character" } },
                new ScriptDocEntry { Kind = ScriptDocKind.Effect, Name = "old_effect", Description = "Removed later", Scopes = { "country" } },
            },
            Enum.GetValues<ScriptDocKind>().ToList(),
            T0,
            new Dictionary<string, long>());

        // Zweiter Import ohne "old_effect".
        db.ApplyImport(
            db.Entries.Where(e => e.Name != "old_effect").Select(e => new ScriptDocEntry { Kind = e.Kind, Name = e.Name, Description = e.Description, Scopes = e.Scopes }).ToList(),
            Enum.GetValues<ScriptDocKind>().ToList(),
            T0.AddDays(1),
            new Dictionary<string, long>());
        return db;
    }

    private static IReadOnlyList<string> Names(ScriptDocQuery query, ScriptDocDatabase db) =>
        query.Run(db).Select(e => e.Name).ToList();

    [Fact]
    public void EmptyText_ListsEverythingAlphabetically_HidingOutdatedByDefault()
    {
        var names = Names(new ScriptDocQuery(), SampleDatabase());

        Assert.DoesNotContain("old_effect", names);
        Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);
    }

    [Fact]
    public void HideOutdatedOff_IncludesOutdated()
    {
        Assert.Contains("old_effect", Names(new ScriptDocQuery { HideOutdated = false }, SampleDatabase()));
    }

    [Fact]
    public void Substring_RanksPrefixThenNameThenDescription()
    {
        var names = Names(new ScriptDocQuery { Text = "gold" }, SampleDatabase());

        // "gold_income" beginnt mit gold; "add_gold", "add_gold_utility", "set_gold" enthalten es; "add_prestige" nur in der Beschreibung.
        Assert.Equal(new[] { "gold_income", "add_gold", "add_gold_utility", "set_gold", "add_prestige" }, names);
    }

    [Fact]
    public void Wildcards_MatchWholeNameOnly()
    {
        var db = SampleDatabase();

        Assert.Equal(new[] { "add_gold", "add_gold_utility", "add_prestige" }, Names(new ScriptDocQuery { Text = "add_*" }, db));
        Assert.Equal(new[] { "add_gold" }, Names(new ScriptDocQuery { Text = "add_g?ld" }, db));
        Assert.Equal(new[] { "add_gold", "set_gold" }, Names(new ScriptDocQuery { Text = "*gold" }, db));
    }

    [Fact]
    public void KindFilter_LimitsToChosenKinds()
    {
        var names = Names(new ScriptDocQuery { Text = "gold", Kinds = new HashSet<ScriptDocKind> { ScriptDocKind.Effect } }, SampleDatabase());

        Assert.Equal(new[] { "add_gold", "set_gold", "add_prestige" }, names);
    }

    [Fact]
    public void ScopeFilter_IgnoresCaseAndUnderscores()
    {
        var names = Names(new ScriptDocQuery { Scope = "InternationalOrganization" }, SampleDatabase());

        Assert.Equal(new[] { "gold_income", "set_gold" }, names);
    }

    [Fact]
    public void AllScopes_ListsEachScopeOnce()
    {
        var scopes = ScriptDocQuery.AllScopes(SampleDatabase());

        Assert.Equal(3, scopes.Count);
        Assert.Contains("character", scopes);
    }

    // ---- Beispielsuche

    private string WriteFile(string relative, string content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void UsageSearch_FindsWholeWordsWithLineNumbers_InScriptFilesOnly()
    {
        var file = WriteFile("mod\\common\\a.txt", "first\n  add_gold = 5\nadd_gold_utility = 1\nx = add_gold\n");
        WriteFile("mod\\localization\\a.yml", "add_gold: \"x\"\n");

        var result = UsageSearch.Run(ScriptDocKind.Effect, "add_gold", new[] { Path.Combine(_dir, "mod") }, T0);

        Assert.Equal(new[] { 2, 4 }, result.Hits.Select(h => h.Line));
        Assert.All(result.Hits, h => Assert.Equal(file, h.FilePath));
        Assert.Equal("add_gold = 5", result.Hits[0].Text);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void UsageSearch_CustomLocalization_AlsoSearchesLocalizationFiles()
    {
        WriteFile("mod\\localization\\a.yml", "key: \"[GetHeathenInsult]\"\n");

        var result = UsageSearch.Run(ScriptDocKind.CustomLocalization, "GetHeathenInsult", new[] { Path.Combine(_dir, "mod") }, T0);

        Assert.Single(result.Hits);
    }

    [Fact]
    public void UsageSearch_StopsAtMaxHits_AndReportsTruncation()
    {
        WriteFile("mod\\a.txt", string.Concat(Enumerable.Repeat("add_gold = 1\n", UsageSearch.MaxHits + 50)));

        var result = UsageSearch.Run(ScriptDocKind.Effect, "add_gold", new[] { Path.Combine(_dir, "mod") }, T0);

        Assert.Equal(UsageSearch.MaxHits, result.Hits.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public void UsageSearch_SkipsMissingRootsAndSearchesSeveral()
    {
        WriteFile("vanilla\\a.txt", "add_gold = 1\n");
        WriteFile("mod\\b.txt", "add_gold = 2\n");

        var result = UsageSearch.Run(
            ScriptDocKind.Effect,
            "add_gold",
            new[] { Path.Combine(_dir, "vanilla"), Path.Combine(_dir, "gibt-es-nicht"), Path.Combine(_dir, "mod") },
            T0);

        Assert.Equal(2, result.Hits.Count);
        Assert.Equal(2, result.Roots.Count);
    }

    [Fact]
    public void UsageSearch_CanBeCancelled()
    {
        WriteFile("mod\\a.txt", "add_gold = 1\n");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            UsageSearch.Run(ScriptDocKind.Effect, "add_gold", new[] { Path.Combine(_dir, "mod") }, T0, cancellation.Token));
    }

    [Fact]
    public void UsageSearch_LongLinesAreShortened()
    {
        WriteFile("mod\\a.txt", "add_gold " + new string('x', 500) + "\n");

        var result = UsageSearch.Run(ScriptDocKind.Effect, "add_gold", new[] { Path.Combine(_dir, "mod") }, T0);

        Assert.Equal(200, result.Hits.Single().Text.Length);
    }

    [Fact]
    public void UsageSearchStore_RoundTrips_AndKeepsEntriesWithOddNamesApart()
    {
        var store = new UsageSearchStore(Path.Combine(_dir, "searches"));
        var one = new UsageSearchResult { Kind = ScriptDocKind.Effect, Name = "a:b", SearchedUtc = T0, Hits = { new UsageHit { FilePath = "f", Line = 3, Text = "t" } } };
        var two = new UsageSearchResult { Kind = ScriptDocKind.Effect, Name = "a?b", SearchedUtc = T0 };

        store.Save(one);
        store.Save(two);

        var loaded = store.TryLoad(ScriptDocKind.Effect, "a:b");
        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.Hits.Single().Line);
        Assert.Empty(store.TryLoad(ScriptDocKind.Effect, "a?b")!.Hits);
        Assert.Null(store.TryLoad(ScriptDocKind.Trigger, "a:b"));
        Assert.Null(store.TryLoad(ScriptDocKind.Effect, "unbekannt"));
    }
}
