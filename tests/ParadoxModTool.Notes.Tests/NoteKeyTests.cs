using Xunit;

namespace ParadoxModTool.Notes.Tests;

/// <summary>Der Schlüssel-Hook ist globaler Zustand: alle Testklassen, die Store-Schlüssel bilden, laufen in dieser Sammlung nacheinander.</summary>
[CollectionDefinition("NoteKeys", DisableParallelization = true)]
public sealed class NoteKeysCollection
{
}

[Collection("NoteKeys")]
public class NoteKeyTests : IDisposable
{
    public NoteKeyTests()
    {
        NoteKeys.SetResolver(null);
    }

    public void Dispose() => NoteKeys.SetResolver(null);

    [Fact]
    public void ForFile_OhneHook_LiefertDenVollenPfad()
    {
        Assert.Equal(Path.GetFullPath(@"C:\m\.\a.txt"), NoteKeys.ForFile(@"C:\m\.\a.txt"));
    }

    [Fact]
    public void ForFile_MitHook_GibtDenModSchluesselZurueck_UndReichtDenVollenPfadDurch()
    {
        string? seen = null;
        NoteKeys.SetResolver(path =>
        {
            seen = path;
            return NoteKeys.ModKey("abc", "common/A.txt");
        });

        var key = NoteKeys.ForFile(@"C:\m\.\common\A.txt");

        Assert.Equal("mod:abc/common/a.txt", key);
        Assert.Equal(Path.GetFullPath(@"C:\m\common\A.txt"), seen);
    }

    [Fact]
    public void ForFile_HookOhneTreffer_FaelltAufDenPfadZurueck()
    {
        NoteKeys.SetResolver(_ => null);

        Assert.Equal(Path.GetFullPath(@"C:\v\a.txt"), NoteKeys.ForFile(@"C:\v\a.txt"));
    }

    [Fact]
    public void ModKey_NimmtSchraegstricheUndKleinbuchstaben()
    {
        Assert.Equal("mod:abc/common/buildings/x.txt", NoteKeys.ModKey("abc", @"Common\Buildings\X.txt"));
    }

    [Fact]
    public void TryParseModKey_ZerlegtSchluessel_UndLehntPfadeAb()
    {
        Assert.True(NoteKeys.TryParseModKey("mod:abc/common/x.txt", out var id, out var relative));
        Assert.Equal("abc", id);
        Assert.Equal("common/x.txt", relative);
        Assert.False(NoteKeys.TryParseModKey(@"C:\m\a.txt", out _, out _));
        Assert.False(NoteKeys.TryParseModKey("mod:ohne-strich", out _, out _));
    }

    [Fact]
    public void AlleStores_BildenSchluesselUeberDenHook()
    {
        NoteKeys.SetResolver(_ => NoteKeys.ModKey("abc", "a.txt"));
        var notes = new Dictionary<string, string>();
        var bookmarks = new Dictionary<string, List<BookmarkEntry>>();
        var bugs = new BugData();

        FileNoteStore.SetNote(notes, @"C:\m\a.txt", "n");
        BookmarkStore.Toggle(bookmarks, @"C:\m\a.txt", 3, "p");
        BugEditing.AddLocal(bugs, "t", string.Empty, "abc", BugLink.ForFile(@"C:\m\a.txt", null, null));

        Assert.Equal(new[] { "mod:abc/a.txt" }, notes.Keys);
        Assert.Equal(new[] { "mod:abc/a.txt" }, bookmarks.Keys);
        Assert.Equal("mod:abc/a.txt", Assert.Single(Assert.Single(bugs.Reports).Links).Key);
        Assert.Equal("n", FileNoteStore.GetNote(notes, @"C:\m\a.txt"));
        Assert.Single(BookmarkStore.GetBookmarks(bookmarks, @"C:\m\a.txt"));
        Assert.Single(BugQueries.ForFile(bugs, @"C:\m\a.txt", BugProjects.None));
    }
}
