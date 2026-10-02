using Xunit;

namespace ParadoxModTool.UndoRedo.Tests;

public sealed class CreateFoldersUndoableActionTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pmt-folders-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void UndoRemovesOnlyTheFoldersItCreatedOnceTheyAreEmpty()
    {
        var mod = Path.Combine(_root, "mod");
        Directory.CreateDirectory(mod);
        var gui = Path.Combine(mod, "in_game", "gui");
        var file = Path.Combine(gui, "a.gui");
        var action = new CompositeUndoableAction("x", new IUndoableAction[]
        {
            new CreateFoldersUndoableAction("x", new[] { gui }),
            new FileWriteUndoableAction("x", file, "text"),
        });

        action.Do();
        Assert.True(File.Exists(file));

        action.Undo();
        Assert.False(Directory.Exists(Path.Combine(mod, "in_game")));
        Assert.True(Directory.Exists(mod));

        action.Do();
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void AFolderThatCannotBeDeletedRightNowIsLeftWithoutError()
    {
        var gui = Path.Combine(_root, "in_game", "gui");
        var action = new CreateFoldersUndoableAction("x", new[] { gui });
        action.Do();
        var info = new DirectoryInfo(gui);
        info.Attributes |= FileAttributes.ReadOnly;
        try
        {
            var error = Record.Exception(action.Undo);

            Assert.Null(error);
            Assert.True(Directory.Exists(gui));
            Assert.True(Directory.Exists(Path.Combine(_root, "in_game")));
        }
        finally
        {
            info.Attributes &= ~FileAttributes.ReadOnly;
        }
    }

    [Fact]
    public void FoldersWithOtherContentStay()
    {
        var gui = Path.Combine(_root, "in_game", "gui");
        var action = new CreateFoldersUndoableAction("x", new[] { gui });
        action.Do();
        File.WriteAllText(Path.Combine(gui, "other.gui"), "x");

        action.Undo();

        Assert.True(File.Exists(Path.Combine(gui, "other.gui")));
    }
}
