using Xunit;

namespace ParadoxModTool.UndoRedo.Tests;

public class ActionHistoryTests
{
    private sealed class RecordingAction : IUndoableAction
    {
        private readonly List<string> _log;

        public RecordingAction(string description, List<string> log)
        {
            Description = description;
            _log = log;
        }

        public string Description { get; }
        public int DoCount { get; private set; }
        public int UndoCount { get; private set; }

        public void Do()
        {
            DoCount++;
            _log.Add($"do:{Description}");
        }

        public void Undo()
        {
            UndoCount++;
            _log.Add($"undo:{Description}");
        }
    }

    [Fact]
    public void Execute_FuehrtAusUndLegtAufUndoStack()
    {
        var log = new List<string>();
        var history = new ActionHistory();
        var action = new RecordingAction("a", log);

        history.Execute(action);

        Assert.Equal(1, action.DoCount);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal("a", history.NextUndoDescription);
    }

    [Fact]
    public void Undo_MachtLetzteAktionRueckgaengigUndVerschiebtSieAufRedoStack()
    {
        var log = new List<string>();
        var history = new ActionHistory();
        var action = new RecordingAction("a", log);
        history.Execute(action);

        history.Undo();

        Assert.Equal(1, action.UndoCount);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.Equal("a", history.NextRedoDescription);
    }

    [Fact]
    public void Redo_FuehrtAktionErneutAusUndVerschiebtZurueckAufUndoStack()
    {
        var log = new List<string>();
        var history = new ActionHistory();
        var action = new RecordingAction("a", log);
        history.Execute(action);
        history.Undo();

        history.Redo();

        Assert.Equal(2, action.DoCount);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Execute_NachUndo_VerwirftDenRedoStack()
    {
        var log = new List<string>();
        var history = new ActionHistory();
        history.Execute(new RecordingAction("a", log));
        history.Undo();

        history.Execute(new RecordingAction("b", log));

        Assert.False(history.CanRedo);
        Assert.Equal(new[] { "b" }, history.UndoDescriptions);
    }

    [Fact]
    public void Undo_OhneVerfuegbareAktion_WirftInvalidOperationException()
    {
        var history = new ActionHistory();

        Assert.Throws<InvalidOperationException>(() => history.Undo());
    }

    [Fact]
    public void Redo_OhneVerfuegbareAktion_WirftInvalidOperationException()
    {
        var history = new ActionHistory();

        Assert.Throws<InvalidOperationException>(() => history.Redo());
    }

    [Fact]
    public void ChangedEvent_FeuertBeiExecuteUndoRedoUndClear()
    {
        var log = new List<string>();
        var history = new ActionHistory();
        var changedCount = 0;
        history.Changed += () => changedCount++;

        history.Execute(new RecordingAction("a", log));
        history.Undo();
        history.Redo();
        history.Clear();

        Assert.Equal(4, changedCount);
    }

    [Fact]
    public void MaxEntries_VerwirftAeltesteEintraegeBeiUeberschreitung()
    {
        var log = new List<string>();
        var history = new ActionHistory(maxEntries: 2);

        history.Execute(new RecordingAction("a", log));
        history.Execute(new RecordingAction("b", log));
        history.Execute(new RecordingAction("c", log));

        Assert.Equal(new[] { "b", "c" }, history.UndoDescriptions);
    }

    [Fact]
    public void Clear_LeertUndoUndRedoStack()
    {
        var log = new List<string>();
        var history = new ActionHistory();
        history.Execute(new RecordingAction("a", log));

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Konstruktor_MitUngueltigemMaxEntries_WirftArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActionHistory(maxEntries: 0));
    }
}
