using System.IO;
using ParadoxModTool.Editor.Gui;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class WorkspaceSaveTests
{
    [Fact]
    public void ModifiedDocumentsOfBothPanesInTabOrder() => Sta.Run(() =>
    {
        var left = new EditorPane();
        var right = new EditorPane();
        var a = left.NewDocument();
        var b = left.NewDocument();
        var c = right.NewDocument();
        var d = left.NewDocument();
        a.Document.Insert(0, "a");
        c.Document.Insert(0, "c");
        d.Document.Insert(0, "d");

        var result = WorkspaceSave.DocumentsToSave(new[] { left, right });

        Assert.Equal(new[] { (left, a), (left, d), (right, c) }, result);
        Assert.DoesNotContain(result, x => ReferenceEquals(x.Document, b));
    });

    [Fact]
    public void NothingModifiedMeansNothingToSave() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        pane.NewDocument();

        Assert.Empty(WorkspaceSave.DocumentsToSave(new[] { pane }));
    });

    [Fact]
    public void VanillaGuiIsNeverSaved() => Sta.Run(() =>
    {
        var install = Path.Combine(Path.GetTempPath(), "pmt-save-all-" + Guid.NewGuid().ToString("N"));
        var vanillaPath = Path.Combine(install, "game", "gui", "frontend.gui");
        var modPath = Path.Combine(Path.GetTempPath(), "pmt-save-all-mod-" + Guid.NewGuid().ToString("N") + ".gui");
        var previous = GuiSettings.InstallDirectory;
        GuiSettings.InstallDirectory = install;
        try
        {
            var pane = new EditorPane();
            pane.NewDocument(); // hält die Auswahl, damit der GUI-Modus der .gui-Dokumente nicht anläuft
            var vanilla = new OpenDocument(vanillaPath, "window = {}", null);
            var mod = new OpenDocument(modPath, "window = {}", null);
            pane.AddDocument(vanilla, select: false);
            pane.AddDocument(mod, select: false);
            vanilla.Document.Insert(0, "# ");
            mod.Document.Insert(0, "# ");

            var result = WorkspaceSave.DocumentsToSave(new[] { pane });

            Assert.Equal(new[] { (pane, mod) }, result);
        }
        finally
        {
            GuiSettings.InstallDirectory = previous;
        }
    });
}
