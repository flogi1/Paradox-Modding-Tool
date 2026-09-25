using Xunit;

namespace ParadoxModTool.UndoRedo.Tests;

public class CompositeUndoableActionTests
{
    private static IUndoableAction Step(string name, List<string> log) => new DelegateUndoableAction(
        name,
        () => log.Add($"do:{name}"),
        () => log.Add($"undo:{name}"));

    [Fact]
    public void Do_FuehrtTeilschritteInReihenfolgeAus()
    {
        var log = new List<string>();
        var composite = new CompositeUndoableAction("Massenumbenennung", new[]
        {
            Step("datei1.txt", log),
            Step("datei2.txt", log),
            Step("datei3.txt", log)
        });

        composite.Do();

        Assert.Equal(new[] { "do:datei1.txt", "do:datei2.txt", "do:datei3.txt" }, log);
    }

    [Fact]
    public void Undo_MachtTeilschritteInUmgekehrterReihenfolgeRueckgaengig()
    {
        var log = new List<string>();
        var composite = new CompositeUndoableAction("Massenumbenennung", new[]
        {
            Step("datei1.txt", log),
            Step("datei2.txt", log),
            Step("datei3.txt", log)
        });
        composite.Do();
        log.Clear();

        composite.Undo();

        Assert.Equal(new[] { "undo:datei3.txt", "undo:datei2.txt", "undo:datei1.txt" }, log);
    }

    [Fact]
    public void ErscheintAlsEinEintragImAktionsverlauf()
    {
        var log = new List<string>();
        var history = new ActionHistory();
        var composite = new CompositeUndoableAction("Massenumbenennung (3 Dateien)", new[]
        {
            Step("datei1.txt", log),
            Step("datei2.txt", log)
        });

        history.Execute(composite);

        Assert.Single(history.UndoDescriptions);
        Assert.Equal("Massenumbenennung (3 Dateien)", history.UndoDescriptions[0]);
    }
}
