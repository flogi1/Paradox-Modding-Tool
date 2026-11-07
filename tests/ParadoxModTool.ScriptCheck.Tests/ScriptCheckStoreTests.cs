using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class ScriptCheckStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scs_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static StoredCheck Sample(int formatVersion = ScriptCheckStore.FormatVersion)
    {
        var stamp = new FileStamp("C:/mod", "events/a.txt", 123, 456);
        var scan = new CheckScan();
        scan.Definitions.Add(new CheckDefinition("a.1", CheckKind.Event, 3, true));
        scan.EventCalls.Add(new CheckRef("a.2", CheckKind.Event, 7, "a.1", CheckKind.Event, true, true));
        scan.Templates.Add(new CheckTemplate("my_$X$_fx", "a.1", CheckKind.Event));
        var mention = new StoredMention(stamp, new CheckRef("my_effect", CheckKind.Effect, 9, "a.1", CheckKind.Event, false, false));
        return new StoredCheck(formatVersion, new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc), 4, "fp1",
            new[] { (stamp, scan) }, new[] { "my_effect", "other" }, new[] { mention });
    }

    [Fact]
    public void RoundTrip()
    {
        var store = new ScriptCheckStore(_dir);
        var saved = Sample();
        store.Save("k", saved);

        var loaded = store.Load("k");

        Assert.NotNull(loaded);
        Assert.Equal(saved.BuiltUtc, loaded!.BuiltUtc);
        Assert.Equal(4, loaded.ReaderVersion);
        Assert.Equal("fp1", loaded.RulesFingerprint);
        Assert.Single(loaded.Files);
        Assert.Equal(saved.Files[0].Stamp, loaded.Files[0].Stamp);
        Assert.Equal(saved.Files[0].Scan.Definitions, loaded.Files[0].Scan.Definitions);
        Assert.Equal(saved.Files[0].Scan.EventCalls, loaded.Files[0].Scan.EventCalls);
        Assert.Equal(saved.Files[0].Scan.Templates, loaded.Files[0].Scan.Templates);
        Assert.Equal(new[] { "my_effect", "other" }, loaded.MentionNames);
        Assert.Equal(saved.Mentions, loaded.Mentions);
    }

    [Fact]
    public void CorruptFileLoadsNull()
    {
        var store = new ScriptCheckStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FileFor("k"), "kein gzip");

        Assert.Null(store.Load("k"));
        Assert.Null(store.Load("fehlt"));
    }

    [Fact]
    public void OtherFormatVersionLoadsNull()
    {
        var store = new ScriptCheckStore(_dir);
        store.Save("k", Sample(ScriptCheckStore.FormatVersion + 1));

        Assert.Null(store.Load("k"));
    }

    [Fact]
    public void StoreRoundTripsPerformanceFields()
    {
        var store = new ScriptCheckStore(_dir);
        var stamp = new FileStamp("C:/mod", "common/on_action/a.txt", 1, 2);
        var scan = new CheckScan();
        scan.Definitions.Add(new CheckDefinition("a.1", CheckKind.Event, 3, true, EndLine: 5, Polled: true));
        scan.Iterators.Add(new IteratorSite("every_country", 4, "a.1", CheckKind.Event, "trigger", "any_country", true));
        scan.OnActionCalls.Add(new CheckRef("my_pulse", CheckKind.OnAction, 6, "yearly_pulse", CheckKind.OnAction, true, true, CheckedBlock: "is_shown"));
        var mention = new StoredMention(stamp, new CheckRef("my_effect", CheckKind.Effect, 9, "a.1", CheckKind.Event, false, true, "effect", "is_shown"));
        store.Save("k", new StoredCheck(ScriptCheckStore.FormatVersion, DateTime.UtcNow, 1, "fp", new[] { (stamp, scan) }, null, new[] { mention }));

        var loaded = store.Load("k");

        Assert.NotNull(loaded);
        var back = loaded!.Files[0].Scan;
        Assert.Equal(scan.Definitions, back.Definitions);
        Assert.Equal(scan.Iterators, back.Iterators);
        Assert.Equal(scan.OnActionCalls, back.OnActionCalls);
        Assert.Equal(new[] { mention }, loaded.Mentions);
    }

    [Fact]
    public void OldFormatVersionIgnored()
    {
        var store = new ScriptCheckStore(_dir);
        store.Save("k", Sample(3));

        Assert.Equal(4, ScriptCheckStore.FormatVersion);
        Assert.Null(store.Load("k"));
    }

    [Fact]
    public void SaveCreatesDirectory()
    {
        var sub = Path.Combine(_dir, "a", "b");
        var store = new ScriptCheckStore(sub);
        store.Save("k", Sample());

        Assert.True(File.Exists(store.FileFor("k")));
        Assert.Empty(Directory.GetFiles(sub, "*.tmp"));
    }

    [Fact]
    public void SaveKeepsAtMostMaxFiles()
    {
        var store = new ScriptCheckStore(_dir, maxFiles: 2);
        store.Save("a", Sample());
        File.SetLastWriteTimeUtc(store.FileFor("a"), DateTime.UtcNow.AddHours(-2));
        store.Save("b", Sample());
        File.SetLastWriteTimeUtc(store.FileFor("b"), DateTime.UtcNow.AddHours(-1));
        store.Save("c", Sample());

        Assert.Equal(new[] { "b.json.gz", "c.json.gz" }, Directory.GetFiles(_dir, "*.json.gz").Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SaveWithoutMaxKeepsAll()
    {
        var store = new ScriptCheckStore(_dir);
        store.Save("a", Sample());
        File.SetLastWriteTimeUtc(store.FileFor("a"), DateTime.UtcNow.AddHours(-2));
        store.Save("b", Sample());
        File.SetLastWriteTimeUtc(store.FileFor("b"), DateTime.UtcNow.AddHours(-1));
        store.Save("c", Sample());

        Assert.Equal(3, Directory.GetFiles(_dir, "*.json.gz").Length);
    }
}
