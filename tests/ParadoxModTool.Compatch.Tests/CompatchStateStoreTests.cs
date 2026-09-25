using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public sealed class CompatchStateStoreTests : IDisposable
{
    private readonly string _game = Path.Combine(Path.GetTempPath(), "pmt-state-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_game))
        {
            Directory.Delete(_game, recursive: true);
        }
    }

    private CompatchStateStore Store() => new(CompatchStateStore.DirectoryFor(_game, "abc123"));

    [Fact]
    public void State_round_trips()
    {
        var state = new CompatchState
        {
            ModAFolder = @"C:\a", ModBFolder = @"C:\b", ModAName = "A", ModBName = "B", ModAId = "ida",
            Target = PatchTarget.Compatch, TargetFolder = @"C:\p", TargetModId = "abc123", Cautious = true, SkipLocked = true,
            HiddenKinds = { "Grafik" },
        };
        state.Decisions["x|farm::cost"] = new StoredDecision(DecisionKind.Manual, "cost = 5", "h1");
        state.Forms["x|farm"] = PatchForm.WholeFile;
        state.Seen.Add("x|farm::cost");

        Store().Save(state);
        var back = Store().Load()!;

        Assert.Equal(Path.Combine(_game, "compatch", "abc123"), Store().Directory);
        Assert.Equal("A", back.ModAName);
        Assert.True(back.Cautious);
        Assert.Equal(new[] { "Grafik" }, back.HiddenKinds);
        Assert.Equal(new StoredDecision(DecisionKind.Manual, "cost = 5", "h1"), back.Decisions["x|farm::cost"]);
        Assert.Equal(PatchForm.WholeFile, back.Forms["x|farm"]);
        Assert.Contains("x|farm::cost", back.Seen);
        Assert.Contains("\"Manual\"", File.ReadAllText(Path.Combine(Store().Directory, "state.json")));
    }

    [Fact]
    public void Base_round_trips_and_missing_files_give_null()
    {
        Assert.Null(Store().Load());
        Assert.Null(Store().LoadBase());

        var baseline = new CompatchBase { ModAVersion = "1.0", AddressHashes = { ["x|farm"] = "h" }, ForeignEntries = { new WrittenEntry("a/b.txt", "x|farm") }, WrittenFiles = { ["c/d.txt"] = "FF" } };
        Store().SaveBase(baseline);
        var back = Store().LoadBase()!;

        Assert.Equal("1.0", back.ModAVersion);
        Assert.Equal("h", back.AddressHashes["x|farm"]);
        Assert.Equal(new WrittenEntry("a/b.txt", "x|farm"), Assert.Single(back.ForeignEntries));
        Assert.Equal("FF", back.WrittenFiles["c/d.txt"]);
    }

    [Fact]
    public void Corrupt_state_is_backed_up_and_replaced_by_empty()
    {
        var store = Store();
        Directory.CreateDirectory(store.Directory);
        File.WriteAllText(Path.Combine(store.Directory, "state.json"), "{ kaputt");

        var state = store.Load();

        Assert.Null(state);
        Assert.NotNull(store.Warning);
        Assert.True(File.Exists(Path.Combine(store.Directory, "state.json.bad")));
    }
}
