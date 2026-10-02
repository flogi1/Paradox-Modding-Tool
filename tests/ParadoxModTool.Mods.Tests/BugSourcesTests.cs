using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class BugSourcesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-bugsrc-" + Guid.NewGuid().ToString("N"));
    private readonly string _modFolder;

    public BugSourcesTests()
    {
        _modFolder = Path.Combine(_dir, "mod");
        Directory.CreateDirectory(_modFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string GameDir => Path.Combine(_dir, "game");

    private (ModRegistry Registry, ModInfo Mod) NewMod()
    {
        var registry = ModRegistry.Load(GameDir);
        return (registry, registry.Add("Meine Mod", _modFolder));
    }

    [Fact]
    public void Quellen_WerdenGespeichertUndGeladen_MitBereinigtenLabels()
    {
        var (registry, mod) = NewMod();

        registry.SetBugSources(mod.Id, new[]
        {
            new BugSourceEntry { Kind = BugSourceKinds.GitHub, Key = " flogi/mod ", Enabled = false, Labels = { " bug ", "", "Bug", "crash" } },
        });

        var source = Assert.Single(ModRegistry.Load(GameDir).Get(mod.Id)!.BugSources);
        Assert.Equal(BugSourceKinds.GitHub, source.Kind);
        Assert.Equal("flogi/mod", source.Key);
        Assert.False(source.Enabled);
        Assert.Equal(new[] { "bug", "crash" }, source.Labels);
    }

    [Fact]
    public void UngueltigesRepo_Doppelt_UnbekannteArt_Werfen()
    {
        var (registry, mod) = NewMod();

        Assert.Throws<ArgumentException>(() => registry.SetBugSources(mod.Id, new[] { new BugSourceEntry { Key = "kein-repo" } }));
        Assert.Throws<ArgumentException>(() => registry.SetBugSources(mod.Id, new[] { new BugSourceEntry { Key = "a/b" }, new BugSourceEntry { Key = "A/B" } }));
        Assert.Throws<ArgumentException>(() => registry.SetBugSources(mod.Id, new[] { new BugSourceEntry { Kind = "steam", Key = "a/b" } }));
        Assert.Empty(registry.Get(mod.Id)!.BugSources);
    }

    [Fact]
    public void Laden_VerwirftUngueltigeEintraege()
    {
        var (registry, mod) = NewMod();
        registry.SetBugSources(mod.Id, new[] { new BugSourceEntry { Key = "flogi/mod" } });
        var path = registry.FilePath;
        File.WriteAllText(path, File.ReadAllText(path).Replace("flogi/mod", "kaputt"));

        Assert.Empty(ModRegistry.Load(GameDir).Get(mod.Id)!.BugSources);
    }

    [Fact]
    public void NeueMod_HatKeineQuellen()
    {
        var (_, mod) = NewMod();

        Assert.Empty(mod.BugSources);
    }

    private static BugSourceEntry Forum(string key = "111/222") => new()
    {
        Kind = BugSourceKinds.Discord,
        Key = key,
        Name = "Server / #bugs",
        OpenTagId = "1",
        DoneTagId = "2",
        RejectedTagId = "3",
        ArchiveClosed = false,
        Labels = { "2", " 4 " },
    };

    [Fact]
    public void DiscordQuelle_WirdMitAllenFeldernGespeichert()
    {
        var (registry, mod) = NewMod();

        registry.SetBugSources(mod.Id, new[] { Forum() });

        var source = Assert.Single(ModRegistry.Load(GameDir).Get(mod.Id)!.BugSources);
        Assert.Equal(BugSourceKinds.Discord, source.Kind);
        Assert.Equal("111/222", source.Key);
        Assert.Equal("Server / #bugs", source.Name);
        Assert.Equal(("1", "2", "3"), (source.OpenTagId, source.DoneTagId, source.RejectedTagId));
        Assert.False(source.ArchiveClosed);
        Assert.Equal(new[] { "2", "4" }, source.Labels);
    }

    [Fact]
    public void DiscordQuelle_UngueltigeWerteWerfen()
    {
        var (registry, mod) = NewMod();
        var sameTags = Forum();
        sameTags.DoneTagId = "1";
        var badFilter = Forum();
        badFilter.Labels = new List<string> { "bug" };
        var missingTag = Forum();
        missingTag.RejectedTagId = string.Empty;

        Assert.Throws<ArgumentException>(() => registry.SetBugSources(mod.Id, new[] { Forum("abc/222") }));
        Assert.Throws<ArgumentException>(() => registry.SetBugSources(mod.Id, new[] { sameTags }));
        Assert.Throws<ArgumentException>(() => registry.SetBugSources(mod.Id, new[] { badFilter }));
        Assert.Throws<ArgumentException>(() => registry.SetBugSources(mod.Id, new[] { missingTag }));
    }

    [Fact]
    public void Clone_KopiertAlleFelder()
    {
        var original = Forum();

        var copy = original.Clone();
        copy.Labels.Add("9");

        Assert.Equal(original.Key, copy.Key);
        Assert.Equal(original.RejectedTagId, copy.RejectedTagId);
        Assert.Equal(original.ArchiveClosed, copy.ArchiveClosed);
        Assert.Equal(2, original.Labels.Count);
    }
}
