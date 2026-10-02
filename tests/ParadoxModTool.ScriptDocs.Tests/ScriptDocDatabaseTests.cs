using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

public sealed class ScriptDocDatabaseTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-scriptdocs-").FullName;

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

    private static ScriptDocEntry E(ScriptDocKind kind, string name, string description = "", params string[] scopes) =>
        new() { Kind = kind, Name = name, Description = description, Scopes = scopes.ToList() };

    private static readonly Dictionary<string, long> NoTicks = new();

    [Fact]
    public void FirstImport_MarksEntriesSeen_NotOutdatedNotNew()
    {
        var db = new ScriptDocDatabase();

        var record = db.ApplyImport(new[] { E(ScriptDocKind.Trigger, "a") }, new[] { ScriptDocKind.Trigger }, T0, NoTicks);

        Assert.Equal(1, record.Id);
        var entry = Assert.Single(db.Entries);
        Assert.Equal(1, entry.FirstSeenImport);
        Assert.Equal(1, entry.LastSeenImport);
        Assert.False(db.IsOutdated(entry));
        Assert.False(db.IsNew(entry));
    }

    [Fact]
    public void SecondImport_EntryMissingBecomesOutdated_NewEntryIsNew_ExistingIsUpdated()
    {
        var db = new ScriptDocDatabase();
        db.ApplyImport(
            new[] { E(ScriptDocKind.Trigger, "stays", "old text"), E(ScriptDocKind.Trigger, "gone") },
            new[] { ScriptDocKind.Trigger },
            T0,
            NoTicks);

        db.ApplyImport(
            new[] { E(ScriptDocKind.Trigger, "stays", "new text", "country"), E(ScriptDocKind.Trigger, "fresh") },
            new[] { ScriptDocKind.Trigger },
            T0.AddDays(30),
            NoTicks);

        var stays = db.Entries.Single(e => e.Name == "stays");
        var gone = db.Entries.Single(e => e.Name == "gone");
        var fresh = db.Entries.Single(e => e.Name == "fresh");
        Assert.Equal("new text", stays.Description);
        Assert.Equal(new[] { "country" }, stays.Scopes);
        Assert.False(db.IsOutdated(stays));
        Assert.False(db.IsNew(stays));
        Assert.True(db.IsOutdated(gone));
        Assert.False(db.IsNew(gone));
        Assert.False(db.IsOutdated(fresh));
        Assert.True(db.IsNew(fresh));
        Assert.Equal(3, db.Entries.Count);
    }

    [Fact]
    public void OutdatedEntryThatReturnsIsCurrentAgain()
    {
        var db = new ScriptDocDatabase();
        var kinds = new[] { ScriptDocKind.Modifier };
        db.ApplyImport(new[] { E(ScriptDocKind.Modifier, "m") }, kinds, T0, NoTicks);
        db.ApplyImport(new[] { E(ScriptDocKind.Modifier, "other") }, kinds, T0.AddDays(1), NoTicks);
        Assert.True(db.IsOutdated(db.Entries.Single(e => e.Name == "m")));

        db.ApplyImport(new[] { E(ScriptDocKind.Modifier, "m") }, kinds, T0.AddDays(2), NoTicks);

        Assert.False(db.IsOutdated(db.Entries.Single(e => e.Name == "m")));
    }

    [Fact]
    public void KindNotReadInLaterImport_IsNotFlaggedOutdated()
    {
        var db = new ScriptDocDatabase();
        db.ApplyImport(
            new[] { E(ScriptDocKind.Trigger, "t"), E(ScriptDocKind.Effect, "e") },
            new[] { ScriptDocKind.Trigger, ScriptDocKind.Effect },
            T0,
            NoTicks);

        db.ApplyImport(new[] { E(ScriptDocKind.Trigger, "t") }, new[] { ScriptDocKind.Trigger }, T0.AddDays(1), NoTicks);

        Assert.False(db.IsOutdated(db.Entries.Single(e => e.Name == "e")));
    }

    [Fact]
    public void SameNameInDifferentKinds_AreSeparateEntries()
    {
        var db = new ScriptDocDatabase();

        db.ApplyImport(
            new[] { E(ScriptDocKind.Trigger, "x"), E(ScriptDocKind.Effect, "x") },
            new[] { ScriptDocKind.Trigger, ScriptDocKind.Effect },
            T0,
            NoTicks);

        Assert.Equal(2, db.Entries.Count);
    }

    [Fact]
    public void MergeDuplicates_KeepsPlainAndDataFormsOfAnEventTarget()
    {
        var data = new ScriptDocEntry { Kind = ScriptDocKind.EventTarget, Name = "culture", Targets = new() { "culture" } };
        data.Extra["Requires Data"] = "yes";
        var plain = new ScriptDocEntry { Kind = ScriptDocKind.EventTarget, Name = "culture", Scopes = new() { "country", "character" }, Targets = new() { "culture" } };

        var merged = Assert.Single(ScriptDocImporter.MergeDuplicates(new[] { data, plain }));

        Assert.Equal("country, character", merged.Extra["Plain Inputs"]);
        Assert.Equal(string.Empty, merged.Extra["Data Inputs"]);
    }

    [Fact]
    public void MergeDuplicates_SingleTargetGetsOnlyItsOwnForm()
    {
        var modifier = new ScriptDocEntry { Kind = ScriptDocKind.EventTarget, Name = "modifier", Scopes = new() { "country" }, Targets = new() { "value" } };
        modifier.Extra["Requires Data"] = "yes";

        var merged = Assert.Single(ScriptDocImporter.MergeDuplicates(new[] { modifier }));

        Assert.Equal("country", merged.Extra["Data Inputs"]);
        Assert.False(merged.Extra.ContainsKey("Plain Inputs"));
    }

    [Fact]
    public void NeedsImport_TrueWhenTheEventTargetImportHasAnOldFormat()
    {
        var db = new ScriptDocDatabase();
        var ticks = new Dictionary<ScriptDocKind, long> { [ScriptDocKind.EventTarget] = 100 };
        db.ApplyImport(Array.Empty<ScriptDocEntry>(), new[] { ScriptDocKind.EventTarget }, T0, new Dictionary<string, long> { [ScriptDocLogFiles.FileNameOf(ScriptDocKind.EventTarget)] = 100 });
        Assert.False(db.NeedsImport(ticks));

        db.Imports[^1].FormatVersion = 0;

        Assert.True(db.NeedsImport(ticks));
    }

    [Fact]
    public void FormatVersion_SurvivesSaveAndLoad_SoTheNextStartDoesNotReimport()
    {
        var storage = new ScriptDocStorage(Path.Combine(_dir, "script-docs-format"));
        var db = new ScriptDocDatabase();
        var ticks = new Dictionary<ScriptDocKind, long> { [ScriptDocKind.EventTarget] = 100 };
        db.ApplyImport(Array.Empty<ScriptDocEntry>(), new[] { ScriptDocKind.EventTarget }, T0, new Dictionary<string, long> { [ScriptDocLogFiles.FileNameOf(ScriptDocKind.EventTarget)] = 100 });

        storage.Save(db);

        Assert.False(storage.Load().NeedsImport(ticks));
    }

    [Fact]
    public void NeedsImport_TrueWithoutImportOrWhenWriteTimeDiffers()
    {
        var db = new ScriptDocDatabase();
        var ticks = new Dictionary<ScriptDocKind, long> { [ScriptDocKind.Trigger] = 100 };
        Assert.True(db.NeedsImport(ticks));

        db.ApplyImport(
            new[] { E(ScriptDocKind.Trigger, "t") },
            new[] { ScriptDocKind.Trigger },
            T0,
            new Dictionary<string, long> { ["triggers.log"] = 100 });

        Assert.False(db.NeedsImport(ticks));
        Assert.True(db.NeedsImport(new Dictionary<ScriptDocKind, long> { [ScriptDocKind.Trigger] = 101 }));
        Assert.True(db.NeedsImport(new Dictionary<ScriptDocKind, long> { [ScriptDocKind.Trigger] = 100, [ScriptDocKind.Effect] = 5 }));
        Assert.False(db.NeedsImport(new Dictionary<ScriptDocKind, long>()));
    }

    [Fact]
    public void Importer_ReadsPresentLogsAndSkipsEmptyOnes()
    {
        File.WriteAllText(Path.Combine(_dir, "modifiers.log"), "Printing Modifier Definitions:\nTag: a, Categories: Country, , All, \n");
        File.WriteAllText(Path.Combine(_dir, "triggers.log"), "# Trigger Documentation\n## t1\nText\n**Supported Scopes**: country  \n");
        File.WriteAllText(Path.Combine(_dir, "effects.log"), string.Empty);
        var db = new ScriptDocDatabase();

        var record = ScriptDocImporter.Import(db, _dir, T0);

        Assert.NotNull(record);
        Assert.Equal(new[] { ScriptDocKind.Modifier, ScriptDocKind.Trigger }, record!.Kinds);
        Assert.Equal(2, db.Entries.Count);
        Assert.Contains("modifiers.log", record.SourceWriteTicks.Keys);
        Assert.DoesNotContain("effects.log", record.SourceWriteTicks.Keys);
    }

    [Fact]
    public void Importer_ReturnsNullWithoutLogs_AndNeedsImportOnlyWhenChanged()
    {
        var db = new ScriptDocDatabase();
        Assert.Null(ScriptDocImporter.Import(db, _dir, T0));
        Assert.False(ScriptDocImporter.NeedsImport(db, _dir));

        var path = Path.Combine(_dir, "triggers.log");
        File.WriteAllText(path, "## t1\nText\n**Supported Scopes**: country  \n");
        Assert.True(ScriptDocImporter.NeedsImport(db, _dir));

        ScriptDocImporter.Import(db, _dir, T0);
        Assert.False(ScriptDocImporter.NeedsImport(db, _dir));

        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(5));
        Assert.True(ScriptDocImporter.NeedsImport(db, _dir));
    }

    [Fact]
    public void Importer_MergesDuplicateNamesInOneLog_UnitingScopes()
    {
        File.WriteAllText(Path.Combine(_dir, "modifiers.log"), "Tag: a, Categories: Country, , All, \nTag: a, Categories: Unit, , All, \n");
        var db = new ScriptDocDatabase();

        ScriptDocImporter.Import(db, _dir, T0);

        Assert.Equal(new[] { "Country", "Unit" }, Assert.Single(db.Entries).Scopes);
    }

    [Fact]
    public void MergeDuplicates_KeepsFirstDescriptionAndFlags_UnitesScopesAndTargets()
    {
        var first = new ScriptDocEntry { Kind = ScriptDocKind.EventTarget, Name = "area", Extra = { ["Requires Data"] = "yes" }, Targets = { "area" } };
        var second = new ScriptDocEntry
        {
            Kind = ScriptDocKind.EventTarget,
            Name = "area",
            Description = "second",
            Scopes = { "country" },
            Targets = { "area", "x" },
            Extra = { ["Requires Data"] = "no", ["Global Link"] = "yes" },
        };
        var other = new ScriptDocEntry { Kind = ScriptDocKind.Trigger, Name = "area" };

        var merged = ScriptDocImporter.MergeDuplicates(new[] { first, second, other });

        Assert.Equal(2, merged.Count);
        Assert.Equal("second", merged[0].Description);
        Assert.Equal(new[] { "country" }, merged[0].Scopes);
        Assert.Equal(new[] { "area", "x" }, merged[0].Targets);
        Assert.Equal("yes", merged[0].Extra["Requires Data"]);
        Assert.Equal("yes", merged[0].Extra["Global Link"]);
    }

    [Fact]
    public void Storage_RoundTripsEntriesImportsAndEnums()
    {
        var storage = new ScriptDocStorage(Path.Combine(_dir, "script-docs"));
        var db = new ScriptDocDatabase();
        db.ApplyImport(
            new[] { new ScriptDocEntry { Kind = ScriptDocKind.OnAction, Name = "x", Scopes = { "country" }, Extra = { ["From Code"] = "Yes" } } },
            new[] { ScriptDocKind.OnAction },
            T0,
            new Dictionary<string, long> { ["on_actions.log"] = 42 });

        storage.Save(db);
        var loaded = storage.Load();

        var entry = Assert.Single(loaded.Entries);
        Assert.Equal(ScriptDocKind.OnAction, entry.Kind);
        Assert.Equal("Yes", entry.Extra["From Code"]);
        Assert.Equal(new[] { "country" }, entry.Scopes);
        var import = Assert.Single(loaded.Imports);
        Assert.Equal(new[] { ScriptDocKind.OnAction }, import.Kinds);
        Assert.Equal(42, import.SourceWriteTicks["on_actions.log"]);
        Assert.Contains("\"OnAction\"", File.ReadAllText(Path.Combine(_dir, "script-docs", "entries.json")));
    }

    [Fact]
    public void Storage_MissingOrCorruptFiles_GiveEmptyDatabase()
    {
        var folder = Path.Combine(_dir, "script-docs");
        var storage = new ScriptDocStorage(folder);
        Assert.Empty(storage.Load().Entries);

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "entries.json"), "{ kaputt");
        File.WriteAllText(Path.Combine(folder, "imports.json"), "[1,");

        var loaded = storage.Load();
        Assert.Empty(loaded.Entries);
        Assert.Empty(loaded.Imports);
    }
}
