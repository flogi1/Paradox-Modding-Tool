using Xunit;

namespace ParadoxModTool.UndoRedo.Tests;

public class FileWriteUndoableActionTests : IDisposable
{
    private readonly string _dir;

    public FileWriteUndoableActionTests()
    {
        _dir = Directory.CreateTempSubdirectory("pmt-undoredo-").FullName;
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
    public void Do_SchreibtNeuenInhalt()
    {
        var path = Path.Combine(_dir, "datei.txt");
        var action = new FileWriteUndoableAction("Datei gespeichert", path, "neuer inhalt");

        action.Do();

        Assert.Equal("neuer inhalt", File.ReadAllText(path));
    }

    [Fact]
    public void Undo_StelltVorherigenInhaltWieder()
    {
        var path = Path.Combine(_dir, "datei.txt");
        File.WriteAllText(path, "alter inhalt");
        var action = new FileWriteUndoableAction("Datei gespeichert", path, "neuer inhalt");
        action.Do();

        action.Undo();

        Assert.Equal("alter inhalt", File.ReadAllText(path));
    }

    [Fact]
    public void Undo_BeiNeuAngelegterDatei_LoeschtSieWieder()
    {
        var path = Path.Combine(_dir, "neue-datei.txt");
        var action = new FileWriteUndoableAction("Datei gespeichert", path, "inhalt");
        action.Do();
        Assert.True(File.Exists(path));

        action.Undo();

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Undo_OhneVorherigesDo_WirftInvalidOperationException()
    {
        var path = Path.Combine(_dir, "datei.txt");
        var action = new FileWriteUndoableAction("Datei gespeichert", path, "inhalt");

        Assert.Throws<InvalidOperationException>(() => action.Undo());
    }

    [Fact]
    public void RedoUeberActionHistory_SchreibtErneutDenNeuenInhalt()
    {
        var path = Path.Combine(_dir, "datei.txt");
        File.WriteAllText(path, "alter inhalt");
        var history = new ActionHistory();
        var action = new FileWriteUndoableAction("Datei gespeichert", path, "neuer inhalt");

        history.Execute(action);
        history.Undo();
        Assert.Equal("alter inhalt", File.ReadAllText(path));

        history.Redo();

        Assert.Equal("neuer inhalt", File.ReadAllText(path));
    }

    [Fact]
    public void Do_LegtFehlendesVerzeichnisAn()
    {
        var path = Path.Combine(_dir, "unterordner", "datei.txt");
        var action = new FileWriteUndoableAction("Datei gespeichert", path, "inhalt");

        action.Do();

        Assert.Equal("inhalt", File.ReadAllText(path));
    }
}
