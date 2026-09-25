using Xunit;

namespace ParadoxModTool.Notes.Tests;

[Collection("NoteKeys")]
public class NoteKeyMigrationTests : IDisposable
{
    private static readonly string Inside = Path.GetFullPath(@"C:\mods\one\common\a.txt");
    private static readonly string Outside = Path.GetFullPath(@"C:\vanilla\common\a.txt");
    private const string ModKey = "mod:abc/common/a.txt";

    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-keys-").FullName;

    public NoteKeyMigrationTests()
    {
        NoteKeys.SetResolver(null);
    }

    public void Dispose()
    {
        NoteKeys.SetResolver(null);
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    /// <summary>Vorwärts: Pfade unter der Mod werden zum Mod-Schlüssel, alles andere bleibt.</summary>
    private static string? ToModKey(string key) =>
        key == Inside ? ModKey : null;

    /// <summary>Rückwärts: Mod-Schlüssel wird wieder der Pfad.</summary>
    private static string? ToPath(string key) =>
        key == ModKey ? Inside : null;

    [Fact]
    public void RekeyNotes_BenenntPfadZumModSchluesselUm_LaesstFremdePfadeStehen()
    {
        var notes = new Dictionary<string, string> { [Inside] = "in der Mod", [Outside] = "Vanilla" };

        var changed = NoteKeyMigration.RekeyNotes(notes, ToModKey);

        Assert.True(changed);
        Assert.Equal("in der Mod", notes[ModKey]);
        Assert.Equal("Vanilla", notes[Outside]);
        Assert.False(notes.ContainsKey(Inside));
    }

    [Fact]
    public void RekeyNotes_ZweiteRundeAendertNichts()
    {
        var notes = new Dictionary<string, string> { [Inside] = "x" };
        NoteKeyMigration.RekeyNotes(notes, ToModKey);

        Assert.False(NoteKeyMigration.RekeyNotes(notes, ToModKey));
        Assert.Equal("x", notes[ModKey]);
    }

    [Fact]
    public void RekeyNotes_KollisionHaengtDieNotizenZusammen_GleicherTextBleibtEinfach()
    {
        var notes = new Dictionary<string, string> { [ModKey] = "alt", [Inside] = "neu" };
        NoteKeyMigration.RekeyNotes(notes, ToModKey);
        Assert.Equal("alt\n\nneu", notes[ModKey]);
        Assert.Single(notes);

        var same = new Dictionary<string, string> { [ModKey] = "gleich", [Inside] = "gleich" };
        NoteKeyMigration.RekeyNotes(same, ToModKey);
        Assert.Equal("gleich", same[ModKey]);
    }

    [Fact]
    public void RekeyNotes_Rueckwaerts_MachtAusDemModSchluesselWiederDenPfad()
    {
        var notes = new Dictionary<string, string> { [ModKey] = "x", [Outside] = "v" };

        Assert.True(NoteKeyMigration.RekeyNotes(notes, ToPath));

        Assert.Equal("x", notes[Inside]);
        Assert.Equal("v", notes[Outside]);
        Assert.False(notes.ContainsKey(ModKey));
    }

    [Fact]
    public void RekeyBookmarks_VereinigtBeiKollision_ZielZeileGewinnt()
    {
        var bookmarks = new Dictionary<string, List<BookmarkEntry>>
        {
            [ModKey] = new() { new BookmarkEntry { Line = 1, Preview = "ziel" } },
            [Inside] = new()
            {
                new BookmarkEntry { Line = 1, Preview = "verschoben" },
                new BookmarkEntry { Line = 7, Preview = "neu" },
            },
        };

        Assert.True(NoteKeyMigration.RekeyBookmarks(bookmarks, ToModKey));

        var list = bookmarks[ModKey].OrderBy(b => b.Line).ToList();
        Assert.Equal(new[] { 1, 7 }, list.Select(b => b.Line));
        Assert.Equal("ziel", list[0].Preview);
        Assert.Single(bookmarks);
    }

    [Fact]
    public void Run_UmziehtNotizenUndLesezeichen_UndSchreibtNurBeiAenderung()
    {
        var notesPath = Path.Combine(_dir, "notes.json");
        var bookmarksPath = Path.Combine(_dir, "bookmarks.json");
        FileNoteStore.Save(new Dictionary<string, string> { [Inside] = "n" }, notesPath);
        BookmarkStore.Save(new Dictionary<string, List<BookmarkEntry>> { [Inside] = new() { new BookmarkEntry { Line = 2 } } }, bookmarksPath);

        Assert.True(NoteKeyMigration.Run(notesPath, bookmarksPath, ToModKey));

        Assert.Equal("n", FileNoteStore.TryLoad(notesPath)[ModKey]);
        Assert.Single(BookmarkStore.TryLoad(bookmarksPath)[ModKey]);

        var stamp = File.GetLastWriteTimeUtc(notesPath);
        Thread.Sleep(50);
        Assert.False(NoteKeyMigration.Run(notesPath, bookmarksPath, ToModKey));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(notesPath));
    }

    [Fact]
    public void Run_OhneDateien_LegtNichtsAn()
    {
        var notesPath = Path.Combine(_dir, "notes.json");

        Assert.False(NoteKeyMigration.Run(notesPath, Path.Combine(_dir, "b.json"), ToModKey));
        Assert.False(File.Exists(notesPath));
    }
}
