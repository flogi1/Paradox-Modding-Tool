using ParadoxModTool.ScriptDocs;
using Xunit;

namespace ParadoxModTool.Scopes.Tests;

public sealed class ScopeLearnerTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _root = Directory.CreateTempSubdirectory("pmt-scopes-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_root, "in_game", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private ScopeRules Learn() => ScopeRuleLearner.Learn(new[] { _root }, TestData.Db(), 1, T0).Rules;

    [Fact]
    public void FieldsWithOneScope_GetAConfidentRule()
    {
        Write("common\\people\\a.txt", string.Concat(Enumerable.Range(0, 3).Select(i => $"p{i} = {{ potential = {{ is_female = yes is_ruler = yes }} }}\n")));

        var rule = Learn().Fields["people/*/potential"];

        Assert.Equal("character", rule.Scope);
        Assert.Equal(1.0, rule.Support);
        Assert.Equal(6, rule.Total);
    }

    [Fact]
    public void TiedOrMixedFields_GetNoScope_ButAreKeptForDiagnosis()
    {
        Write("common\\mixed\\a.txt", "m = { potential = { add_gold = 1 add_prestige = 1 is_female = yes is_ruler = yes add_gold = 2 is_female = no } }\n");

        var rule = Learn().Fields["mixed/*/potential"];

        Assert.Null(rule.Scope);
        Assert.Equal(6, rule.Total);
        Assert.True(rule.Support < 0.9);
    }

    [Fact]
    public void TooFewStatements_GiveNoRule()
    {
        Write("common\\few\\a.txt", "f = { potential = { is_female = yes } }\n");

        Assert.False(Learn().Fields.ContainsKey("few/*/potential"));
    }

    [Fact]
    public void ScopeChangersAreNotCounted_TransparentBlocksAre()
    {
        // Direkt im Feld: nur is_female/is_ruler (character). Der Iterator wechselt den Scope, seine Effects zählen nicht; OR ist durchsichtig.
        Write(
            "common\\nest\\a.txt",
            "n = { potential = { OR = { is_female = yes is_ruler = yes } every_neighbor_country = { add_gold = 1 add_prestige = 1 add_gold = 3 } is_female = no is_ruler = no is_female = yes } }\n");

        var rule = Learn().Fields["nest/*/potential"];

        Assert.Equal("character", rule.Scope);
        Assert.Equal(5, rule.Total);
    }

    [Fact]
    public void Events_UseTheEventTypeAsObjectPart_AndReadmesAreIgnored()
    {
        Write("events\\e.txt", "mod.1 = { type = country_event id = a immediate = { add_gold = 1 add_prestige = 1 add_gold = 2 add_prestige = 2 add_gold = 3 } }\n");
        Write("common\\people\\readme.txt", "r = { potential = { is_female = yes is_ruler = yes is_female = no is_ruler = no is_female = no } }\n");

        var rules = Learn();

        Assert.Equal("country", rules.Fields["events/country_event/immediate"].Scope);
        Assert.False(rules.Fields.ContainsKey("people/*/potential"));
    }

    [Fact]
    public void ASiblingValue_IsLearnedAsDiscriminator_WhenItExplainsTheScope()
    {
        // Je zwei Objekte mit type = character (Trigger für character) und type = country (Effects für country): ohne Unterscheider 50 %.
        Write(
            "common\\cl\\a.txt",
            "a1 = { type = character text = { trigger = { is_female = yes is_ruler = yes } } }\n" +
            "a2 = { type = character text = { trigger = { is_ruler = yes is_female = no } } }\n" +
            "b1 = { type = country text = { trigger = { add_gold = 1 add_prestige = 1 } } }\n" +
            "b2 = { type = country text = { trigger = { add_prestige = 2 add_gold = 2 } } }\n");

        var rules = Learn();

        Assert.Null(rules.Fields["cl/*/text/trigger"].Scope);
        var discriminator = rules.Discriminators["cl/*/text/trigger"];
        Assert.Equal("type", discriminator.Key);
        Assert.Equal("character", discriminator.Map["character"]);
        Assert.Equal("country", discriminator.Map["country"]);
    }

    [Fact]
    public void UniqueSiblingValues_AreNotADiscriminator()
    {
        Write(
            "common\\ids\\a.txt",
            "a1 = { id = one text = { trigger = { is_female = yes add_gold = 1 } } }\n" +
            "a2 = { id = two text = { trigger = { is_ruler = yes add_prestige = 1 } } }\n" +
            "a3 = { id = three text = { trigger = { is_female = no add_gold = 2 } } }\n");

        Assert.DoesNotContain("ids/*/text/trigger", Learn().Discriminators.Keys);
    }

    [Fact]
    public void SavedScopes_GetTheirTypeFromTheStatementsInsideTheirBlock()
    {
        Write(
            "common\\ci\\a.txt",
            string.Concat(Enumerable.Range(0, 3).Select(i =>
                $"i{i} = {{ potential = {{ scope:actor = {{ add_gold = 1 add_prestige = 1 }} }} allow = {{ scope:target = {{ is_female = yes }} }} }}\n")));

        var rules = Learn();

        Assert.Equal("country", rules.SavedFields["ci/*/potential"]["actor"]);
        Assert.Equal("country", rules.SavedNames["actor"]);
    }

    [Fact]
    public void SavedScopes_NeedEnoughEvidenceAndAgreement()
    {
        // target: nur 3 Statements (zu wenig); recipient: 3 x character, 3 x country (uneindeutig).
        Write(
            "common\\ci\\a.txt",
            string.Concat(Enumerable.Range(0, 3).Select(i =>
                $"i{i} = {{ allow = {{ scope:target = {{ is_female = yes }} scope:recipient = {{ is_female = yes add_gold = 1 }} }} }}\n")));

        var rules = Learn();

        Assert.DoesNotContain("target", rules.SavedNames.Keys);
        Assert.DoesNotContain("recipient", rules.SavedNames.Keys);
        Assert.False(rules.SavedFields.TryGetValue("ci/*/allow", out var names) && names.ContainsKey("recipient"));
    }

    [Fact]
    public void SavedScopes_SameNameCanHaveDifferentTypesInDifferentFields()
    {
        Write(
            "common\\ci\\a.txt",
            string.Concat(Enumerable.Range(0, 3).Select(i =>
                $"i{i} = {{ potential = {{ scope:target = {{ add_gold = 1 add_prestige = 1 }} }} allow = {{ scope:target = {{ is_female = yes is_ruler = yes }} }} }}\n")));

        var rules = Learn();

        Assert.Equal("country", rules.SavedFields["ci/*/potential"]["target"]);
        Assert.Equal("character", rules.SavedFields["ci/*/allow"]["target"]);
        Assert.DoesNotContain("target", rules.SavedNames.Keys);
    }

    [Fact]
    public void ChainsAndNestedBlocks_AreHandledWhenLearningSavedScopes()
    {
        // scope:actor.owner ist eine Kette, kein gespeicherter Scope; ein verschachteltes scope:actor zählt mit seinem eigenen Block.
        Write(
            "common\\ci\\a.txt",
            string.Concat(Enumerable.Range(0, 6).Select(i =>
                $"i{i} = {{ potential = {{ scope:actor.owner = {{ is_female = yes }} if = {{ limit = {{ has_dlc = x }} scope:actor = {{ add_gold = 1 }} }} }} }}\n")));

        var rules = Learn();

        Assert.Equal("country", rules.SavedFields["ci/*/potential"]["actor"]);
        Assert.DoesNotContain("actor.owner", rules.SavedFields["ci/*/potential"].Keys);
    }

    [Fact]
    public void Fingerprint_ChangesWithFiles_AndIsStableOtherwise()
    {
        var path = Write("common\\people\\a.txt", "p = { potential = { is_female = yes } }\n");
        var first = ScopeRuleLearner.ComputeFingerprint(ScopeRuleLearner.EnumerateFiles(new[] { _root }));
        var again = ScopeRuleLearner.ComputeFingerprint(ScopeRuleLearner.EnumerateFiles(new[] { _root }));

        File.AppendAllText(path, "# mehr\n");
        var changed = ScopeRuleLearner.ComputeFingerprint(ScopeRuleLearner.EnumerateFiles(new[] { _root }));
        Write("common\\people\\b.txt", "q = { }\n");
        var added = ScopeRuleLearner.ComputeFingerprint(ScopeRuleLearner.EnumerateFiles(new[] { _root }));

        Assert.Equal(first, again);
        Assert.NotEqual(first, changed);
        Assert.NotEqual(changed, added);
    }

    [Fact]
    public void Learn_RecordsMetadata_AndSupportsRootsWithOrWithoutInGame()
    {
        Write("common\\people\\a.txt", "p = { potential = { is_female = yes is_ruler = yes is_female = no is_ruler = no is_female = yes } }\n");

        var rules = ScopeRuleLearner.Learn(new[] { Path.Combine(_root, "in_game") }, TestData.Db(), 7, T0).Rules;

        Assert.Equal(7, rules.DocsImportId);
        Assert.Equal(1, rules.FilesScanned);
        Assert.Equal(T0, rules.LearnedUtc);
        Assert.NotEmpty(rules.Fingerprint);
        Assert.Equal(1, rules.ConfidentCount);
    }

    [Fact]
    public void Learn_CanBeCancelled()
    {
        Write("common\\people\\a.txt", "p = { }\n");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => ScopeRuleLearner.Learn(new[] { _root }, TestData.Db(), 1, T0, cancellation.Token));
    }

    [Fact]
    public void Store_RoundTripsRules_AndIgnoresMissingOrCorruptFiles()
    {
        var path = Path.Combine(_root, "games", "eu5", "scopes", "rules.json");
        var store = new ScopeRulesStore(path);
        Assert.Null(store.TryLoad());

        var rules = TestData.Rules();
        rules.Fingerprint = "abc";
        rules.DocsImportId = 3;
        store.Save(rules);
        var loaded = store.TryLoad();

        Assert.NotNull(loaded);
        Assert.Equal("abc", loaded!.Fingerprint);
        Assert.Equal(3, loaded.DocsImportId);
        Assert.Equal("country", loaded.Fields["buildings/*/potential"].Scope);
        Assert.Equal("character", loaded.Discriminators["cl/*/text/trigger"].Map["character"]);

        File.WriteAllText(path, "{ kaputt");
        Assert.Null(store.TryLoad());
    }

    [Fact]
    public void Store_IgnoresRulesOfAnotherVersion()
    {
        var path = Path.Combine(_root, "rules.json");
        var store = new ScopeRulesStore(path);
        store.Save(new ScopeRules { Version = 99 });

        Assert.Null(store.TryLoad());
    }

    [Fact]
    public void ScopeNames_AreNormalized_AndTheDatabaseVotesWithModifierCategories()
    {
        Assert.Equal("international_organization", ScopeName.Normalize("InternationalOrganization"));
        Assert.Equal("international_organization", ScopeName.Normalize("international organization"));
        Assert.Equal("country", ScopeName.Normalize(" Country "));

        var db = TestData.Db();
        Assert.Equal(new[] { "country" }, db.VoteScopes("country_income"));
        Assert.Null(db.VoteScopes("has_dlc"));
        Assert.Null(db.VoteScopes("unknown"));
    }

    [Fact]
    public void Learn_ReportsProgressPerFile()
    {
        Write("common\\people\\a.txt", "p = { }\n");
        Write("common\\people\\b.txt", "q = { }\n");
        var progress = new Collect();

        var rules = ScopeRuleLearner.Learn(new[] { _root }, TestData.Db(), 1, T0, CancellationToken.None, progress).Rules;

        Assert.Equal(rules.FilesScanned, progress.Items.Count);
        Assert.Equal((rules.FilesScanned, rules.FilesScanned), progress.Items[^1]);
    }

    private sealed class Collect : IProgress<(int Done, int Total)>
    {
        public List<(int Done, int Total)> Items { get; } = new();

        public void Report((int Done, int Total) value) => Items.Add(value);
    }
}
