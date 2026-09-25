using Xunit;

namespace ParadoxModTool.Notes.Tests;

[Collection("NoteKeys")]
public class BookmarkStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-bm-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Set_LegtKommentarUndBereichAnUndErsetztAufGleicherStartzeile()
    {
        var bookmarks = new Dictionary<string, List<BookmarkEntry>>();

        BookmarkStore.Set(bookmarks, @"C:\m\a.txt", 5, 9, "erste", "x = 1");
        BookmarkStore.Set(bookmarks, @"C:\m\a.txt", 5, null, "zweite", "x = 1");

        var entry = Assert.Single(BookmarkStore.GetBookmarks(bookmarks, @"C:\m\a.txt"));
        Assert.Equal("zweite", entry.Comment);
        Assert.Null(entry.EndLine);
        Assert.Equal("Zeile 5", entry.LineText);
    }

    [Fact]
    public void LineText_ZeigtBereich()
    {
        var entry = new BookmarkEntry { Line = 5, EndLine = 9 };

        Assert.Equal("Zeilen 5\u20139", entry.LineText);
        Assert.Equal(9, entry.LastLine);
    }

    [Fact]
    public void FindContaining_FindetLesezeichenAnJederZeileDesBereichs()
    {
        var bookmarks = new Dictionary<string, List<BookmarkEntry>>();
        BookmarkStore.Set(bookmarks, @"C:\m\a.txt", 5, 9, "k", "");

        Assert.NotNull(BookmarkStore.FindContaining(bookmarks, @"C:\m\a.txt", 7));
        Assert.Null(BookmarkStore.FindContaining(bookmarks, @"C:\m\a.txt", 10));
    }

    [Fact]
    public void SaveUndTryLoad_BehaeltKommentarUndBereich()
    {
        var path = Path.Combine(_dir, "bookmarks.json");
        var bookmarks = new Dictionary<string, List<BookmarkEntry>>();
        BookmarkStore.Set(bookmarks, @"C:\m\a.txt", 3, 4, "wichtig", "y");

        BookmarkStore.Save(bookmarks, path);
        var entry = Assert.Single(BookmarkStore.GetBookmarks(BookmarkStore.TryLoad(path), @"C:\m\a.txt"));

        Assert.Equal("wichtig", entry.Comment);
        Assert.Equal(4, entry.EndLine);
    }

    [Fact]
    public void TryLoad_AlteDateiOhneNeueFelder_LaedtMitStandardwerten()
    {
        var path = Path.Combine(_dir, "old.json");
        File.WriteAllText(path, "{ \"C:\\\\m\\\\a.txt\": [ { \"Line\": 2, \"Preview\": \"alt\" } ] }");

        var entry = Assert.Single(BookmarkStore.GetBookmarks(BookmarkStore.TryLoad(path), @"C:\m\a.txt"));

        Assert.Equal(2, entry.Line);
        Assert.Equal(string.Empty, entry.Comment);
        Assert.Null(entry.EndLine);
    }
}
