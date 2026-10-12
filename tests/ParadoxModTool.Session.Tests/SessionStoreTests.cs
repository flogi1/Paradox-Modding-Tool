using Xunit;

namespace ParadoxModTool.Session.Tests;

public class SessionStoreTests : IDisposable
{
    private readonly string _dir;

    public SessionStoreTests()
    {
        _dir = Directory.CreateTempSubdirectory("pmt-session-").FullName;
    }

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

    private static AppSessionState CreateSampleState() => new()
    {
        SplitViewEnabled = true,
        ActiveGameProfileId = "eu5",
        LaunchOptionArgs = "-debug_mode",
        GeneratorScaleModifiers = false,
        GeneratorLookupSearch = "way",
        GeneratorLookupSource = "advances",
        GeneratorLookupTag = "SWE",
        GeneratorImageGridBounds = new WindowBoundsState { Width = 900, Height = 700 },
        LeftPane = new PaneSessionState
        {
            ActiveDocumentIndex = 1,
            TabSort = "Name",
            Documents =
            {
                new DocumentSessionState { FilePath = @"C:\mod\common\a.txt", CaretOffset = 12 },
                new DocumentSessionState { FilePath = @"C:\mod\common\b.txt", CaretOffset = 0 },
            },
        },
        RightPane = new PaneSessionState
        {
            ActiveDocumentIndex = 0,
            Documents =
            {
                new DocumentSessionState { FilePath = @"C:\mod\gui\c.gui", CaretOffset = 340 },
            },
        },
    };

    [Fact]
    public void SaveUndTryLoad_RundtripErhaeltAlleFelder()
    {
        var path = Path.Combine(_dir, "session.json");
        var original = CreateSampleState();

        SessionStore.Save(original, path);
        var loaded = SessionStore.TryLoad(path);

        Assert.NotNull(loaded);
        Assert.True(loaded!.SplitViewEnabled);
        Assert.Equal("eu5", loaded.ActiveGameProfileId);
        Assert.Equal("-debug_mode", loaded.LaunchOptionArgs);
        Assert.False(loaded.GeneratorScaleModifiers);
        Assert.True(loaded.GeneratorCopyModifiers);
        Assert.Equal(("way", "advances", "SWE"), (loaded.GeneratorLookupSearch, loaded.GeneratorLookupSource, loaded.GeneratorLookupTag));
        Assert.Equal(900, loaded.GeneratorImageGridBounds!.Width);
        Assert.Equal(1, loaded.LeftPane.ActiveDocumentIndex);
        Assert.Equal("Name", loaded.LeftPane.TabSort);
        Assert.Equal("Manual", loaded.RightPane.TabSort);
        Assert.Equal(2, loaded.LeftPane.Documents.Count);
        Assert.Equal(@"C:\mod\common\a.txt", loaded.LeftPane.Documents[0].FilePath);
        Assert.Equal(12, loaded.LeftPane.Documents[0].CaretOffset);
        Assert.Single(loaded.RightPane.Documents);
        Assert.Equal(@"C:\mod\gui\c.gui", loaded.RightPane.Documents[0].FilePath);
    }

    [Fact]
    public void Generator_switches_default_on()
    {
        var state = new AppSessionState();

        Assert.True(state.GeneratorCopyModifiers);
        Assert.True(state.GeneratorScaleModifiers);
        Assert.True(state.GeneratorRemoveAcrossLevels);
    }

    [Fact]
    public void TryLoad_DateiFehltNoch_GibtNullZurueck()
    {
        var path = Path.Combine(_dir, "gibt-es-nicht.json");

        var loaded = SessionStore.TryLoad(path);

        Assert.Null(loaded);
    }

    [Fact]
    public void TryLoad_KaputtesJson_GibtNullStattFehlerZurueck()
    {
        var path = Path.Combine(_dir, "kaputt.json");
        File.WriteAllText(path, "{ das ist kein gueltiges JSON ][");

        var loaded = SessionStore.TryLoad(path);

        Assert.Null(loaded);
    }

    [Fact]
    public void Save_LegtFehlendenSessionOrdnerAn()
    {
        var path = Path.Combine(_dir, "unterordner", "session.json");
        var state = CreateSampleState();

        SessionStore.Save(state, path);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_UeberschreibtVorhandeneDatei()
    {
        var path = Path.Combine(_dir, "session.json");
        SessionStore.Save(CreateSampleState(), path);

        var replacement = new AppSessionState { SplitViewEnabled = false };
        SessionStore.Save(replacement, path);
        var loaded = SessionStore.TryLoad(path);

        Assert.NotNull(loaded);
        Assert.False(loaded!.SplitViewEnabled);
        Assert.Empty(loaded.LeftPane.Documents);
    }

    [Fact]
    public void SaveUndTryLoad_LeererZustandOhneOffeneDokumente_FunktioniertEbenfalls()
    {
        var path = Path.Combine(_dir, "session.json");
        var empty = new AppSessionState();

        SessionStore.Save(empty, path);
        var loaded = SessionStore.TryLoad(path);

        Assert.NotNull(loaded);
        Assert.False(loaded!.SplitViewEnabled);
        Assert.Empty(loaded.LeftPane.Documents);
        Assert.Equal(-1, loaded.LeftPane.ActiveDocumentIndex);
    }

    [Fact]
    public void Kategorien_RundtripErhaeltRegelnUndReihenfolge()
    {
        var path = Path.Combine(_dir, "session.json");
        var state = new AppSessionState
        {
            Categories = new List<ParadoxModTool.Categories.CategoryDefinition>
            {
                new() { Id = "c-1", Name = "Events", ColorHex = "#FF0000", Extensions = { ".txt" }, Patterns = { "events/*" } },
                new() { Id = "Other", Name = "Sonstige", ColorHex = "#808080" },
            },
        };

        SessionStore.Save(state, path);
        var loaded = SessionStore.TryLoad(path);

        Assert.NotNull(loaded?.Categories);
        Assert.Equal(new[] { "c-1", "Other" }, loaded!.Categories!.Select(c => c.Id).ToArray());
        Assert.Equal("events/*", loaded.Categories![0].Patterns.Single());
        Assert.Equal("#FF0000", loaded.Categories[0].ColorHex);
    }

    [Fact]
    public void Kategorien_AlteSessionOhneFeld_LiefertNull()
    {
        var path = Path.Combine(_dir, "session.json");
        File.WriteAllText(path, "{ \"SplitViewEnabled\": true }");

        var loaded = SessionStore.TryLoad(path);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.Categories);
    }
}
