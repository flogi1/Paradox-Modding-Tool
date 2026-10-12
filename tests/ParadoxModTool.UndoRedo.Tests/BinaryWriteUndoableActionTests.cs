using Xunit;

namespace ParadoxModTool.UndoRedo.Tests;

/// <summary>Rückgängig machbares Schreiben einer Binärdatei (z. B. ein Wappen als .dds).</summary>
public sealed class BinaryWriteUndoableActionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-binundo-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Binary_write_and_undo_restores_previous_bytes()
    {
        var path = Path.Combine(_dir, "a.dds");
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(path, new byte[] { 1, 2 });
        var action = new BinaryWriteUndoableAction("x", path, new byte[] { 9 });

        action.Do();
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(path));
        action.Undo();
        Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(path));
        action.Do();
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void Binary_write_undo_deletes_new_file()
    {
        var path = Path.Combine(_dir, "sub", "b.dds");
        var action = new BinaryWriteUndoableAction("x", path, new byte[] { 5 });

        action.Do();
        Assert.True(File.Exists(path));
        action.Undo();
        Assert.False(File.Exists(path));
    }
}
