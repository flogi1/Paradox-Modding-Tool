using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class ScriptCheckHideListTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-schide-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, true);
        }
        catch (IOException)
        {
        }
    }

    private static Finding Find(string name) =>
        Finding.Create(FindingKind.DeadEvent, FindingSeverity.Warning, name, CheckKind.Event, new SiteLocation("r", "events/a.txt", 1), Array.Empty<string>());

    [Fact]
    public void RoundTripKeepsKeys()
    {
        var mod = Path.Combine(_temp, "mod");
        var list = ScriptCheckHideList.Load(_temp, mod);
        Assert.Empty(list.Keys);
        Assert.True(list.Add("DeadEvent|a.1"));
        Assert.False(list.Add("DeadEvent|a.1"));
        Assert.True(list.Add("UnusedBlock|x"));
        list.Save();

        var again = ScriptCheckHideList.Load(_temp, mod);

        Assert.Equal(new[] { "DeadEvent|a.1", "UnusedBlock|x" }, again.Keys.Order());
        Assert.True(again.IsHidden(Find("a.1")));
        Assert.False(again.IsHidden(Find("a.2")));
        Assert.True(again.Remove("DeadEvent|a.1"));
        Assert.False(again.Remove("DeadEvent|a.1"));
        Assert.False(again.IsHidden(Find("a.1")));
    }

    [Fact]
    public void IsHiddenByKey()
    {
        var list = ScriptCheckHideList.Load(_temp, Path.Combine(_temp, "mod"));
        list.Add("MissingTexture|gfx/a.dds");

        Assert.True(list.IsHidden("MissingTexture|gfx/a.dds"));
        Assert.False(list.IsHidden("MissingTexture|gfx/b.dds"));
        Assert.False(list.IsHidden(string.Empty));
    }

    [Fact]
    public void ModsHaveSeparateFiles()
    {
        var a = ScriptCheckHideList.Load(_temp, Path.Combine(_temp, "a"));
        a.Add("DeadEvent|a.1");
        a.Save();

        Assert.Empty(ScriptCheckHideList.Load(_temp, Path.Combine(_temp, "b")).Keys);
        Assert.Single(ScriptCheckHideList.Load(_temp, Path.Combine(_temp, "a")).Keys);
        Assert.True(File.Exists(Path.Combine(_temp, FlagHideList.KeyFor(Path.Combine(_temp, "a")) + ".json")));
    }

    [Fact]
    public void CorruptFileGivesEmptyList()
    {
        var mod = Path.Combine(_temp, "mod");
        Directory.CreateDirectory(_temp);
        File.WriteAllText(Path.Combine(_temp, FlagHideList.KeyFor(mod) + ".json"), "{ kaputt");

        var list = ScriptCheckHideList.Load(_temp, mod);

        Assert.Empty(list.Keys);
        Assert.True(list.Add("DeadEvent|a.1"));
        list.Save();
        Assert.Single(ScriptCheckHideList.Load(_temp, mod).Keys);
    }

    [Fact]
    public void ReadErrorBlocksSave()
    {
        var mod = Path.Combine(_temp, "mod");
        Directory.CreateDirectory(_temp);
        var file = Path.Combine(_temp, FlagHideList.KeyFor(mod) + ".json");
        File.WriteAllText(file, "[\"DeadEvent|a.1\"]");

        ScriptCheckHideList list;
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            list = ScriptCheckHideList.Load(_temp, mod);
        }

        Assert.True(list.ReadFailed);
        Assert.Empty(list.Keys);
        list.Add("UnusedBlock|x");
        Assert.Throws<InvalidOperationException>(() => list.Save());
        Assert.Equal("[\"DeadEvent|a.1\"]", File.ReadAllText(file));
    }
}
