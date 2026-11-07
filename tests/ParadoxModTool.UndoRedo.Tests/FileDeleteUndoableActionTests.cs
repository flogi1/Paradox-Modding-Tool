using Xunit;

namespace ParadoxModTool.UndoRedo.Tests;

public sealed class FileDeleteUndoableActionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pmt-del-" + Guid.NewGuid().ToString("N"));

    public FileDeleteUndoableActionTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Execute_Undo_RoundTrip()
    {
        var path = Path.Combine(_folder, "a.txt");
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0x0D, 0x0A, 0xE4 };
        File.WriteAllBytes(path, bytes);
        var action = new FileDeleteUndoableAction("Löschen", path);

        action.Do();
        Assert.False(File.Exists(path));

        action.Undo();
        Assert.Equal(bytes, File.ReadAllBytes(path));

        action.Do();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Missing_file_is_a_no_op()
    {
        var path = Path.Combine(_folder, "missing.txt");
        var action = new FileDeleteUndoableAction("Löschen", path);

        action.Do();
        action.Undo();

        Assert.False(File.Exists(path));
    }
}
