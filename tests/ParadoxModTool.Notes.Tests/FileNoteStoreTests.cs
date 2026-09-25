using Xunit;

namespace ParadoxModTool.Notes.Tests;

[Collection("NoteKeys")]
public class FileNoteStoreTests : IDisposable
{
    private readonly string _dir;

    public FileNoteStoreTests()
    {
        _dir = Directory.CreateTempSubdirectory("pmt-notes-").FullName;
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

    [Fact]
    public void SaveUndTryLoad_Rundtrip()
    {
        var path = Path.Combine(_dir, "notes.json");
        var notes = new Dictionary<string, string>
        {
            [@"C:\mod\common\file1.txt"] = "Erste Notiz",
            [@"C:\mod\gui\file2.gui"] = "Zweite Notiz",
        };

        FileNoteStore.Save(notes, path);
        var loaded = FileNoteStore.TryLoad(path);

        Assert.Equal(notes, loaded);
    }

    [Fact]
    public void TryLoad_DateiFehltNoch_GibtLeeresWoerterbuchZurueck()
    {
        var path = Path.Combine(_dir, "gibt-es-nicht.json");

        var loaded = FileNoteStore.TryLoad(path);

        Assert.Empty(loaded);
    }

    [Fact]
    public void TryLoad_KaputtesJson_GibtLeeresWoerterbuchStattFehlerZurueck()
    {
        var path = Path.Combine(_dir, "kaputt.json");
        File.WriteAllText(path, "{ das ist kein gueltiges JSON ][");

        var loaded = FileNoteStore.TryLoad(path);

        Assert.Empty(loaded);
    }

    [Fact]
    public void GetNote_OhneEintrag_GibtLeerenStringZurueck()
    {
        var notes = new Dictionary<string, string>();

        var note = FileNoteStore.GetNote(notes, @"C:\mod\common\file1.txt");

        Assert.Equal(string.Empty, note);
    }

    [Fact]
    public void SetNote_UndGetNote_Rundtrip()
    {
        var notes = new Dictionary<string, string>();
        var filePath = Path.Combine(_dir, "file1.txt");

        FileNoteStore.SetNote(notes, filePath, "Meine Notiz");

        Assert.Equal("Meine Notiz", FileNoteStore.GetNote(notes, filePath));
    }

    [Fact]
    public void SetNote_LeererText_EntferntVorhandenenEintrag()
    {
        var notes = new Dictionary<string, string>();
        var filePath = Path.Combine(_dir, "file1.txt");
        FileNoteStore.SetNote(notes, filePath, "Meine Notiz");

        FileNoteStore.SetNote(notes, filePath, "   ");

        Assert.Empty(notes);
        Assert.Equal(string.Empty, FileNoteStore.GetNote(notes, filePath));
    }

    [Fact]
    public void SetNoteUndGetNote_UnterschiedlicheSchreibweiseDesselbenPfades_TreffenDieselbeNotiz()
    {
        var notes = new Dictionary<string, string>();
        var filePath = Path.Combine(_dir, "file1.txt");
        var pathWithDoubleSeparator = _dir + Path.DirectorySeparatorChar + Path.DirectorySeparatorChar + "file1.txt";

        FileNoteStore.SetNote(notes, filePath, "Meine Notiz");

        Assert.Equal("Meine Notiz", FileNoteStore.GetNote(notes, pathWithDoubleSeparator));
    }
}
