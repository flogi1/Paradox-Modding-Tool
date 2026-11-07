using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Befunde im Designer: klassische Prüfungen (Plan 3, Task 2) und Jomini-Vorschau; Liste unter dem Eigenschaftenfeld, Baummarke, Lauf im Hintergrund.</summary>
public sealed class GuiFindingsDesignerTests
{
    private const string WindowGui =
        "window = { name = \"main\" size = { 100 100 } widget = { name = \"inner\" size = { 10 10 } } }";

    private static GuiWorkspace NewWorkspace(string install)
    {
        Directory.CreateDirectory(install);
        return GuiWorkspace.Create(new GuiWorkspaceOptions(install, null, Array.Empty<string>(), GuiRules.Empty, null));
    }

    private static (GuiDesignerView View, GuiDocument Document) Designer()
    {
        var document = GuiParser.Parse(WindowGui);
        var resolver = new GuiResolver(GuiIndex.Empty, GuiRules.Empty, document, null);
        var view = new GuiDesignerView();
        view.SetTree(GuiElementTree.Build(document, resolver, null));
        return (view, document);
    }

    /// <summary>Arbeitet Nachrichten ab, bis die Bedingung gilt (höchstens 10 Sekunden).</summary>
    private static void PumpUntil(Func<bool> condition)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            dispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            Thread.Sleep(10);
        }

        Assert.True(condition(), "Bedingung kam nicht rechtzeitig.");
    }

    /// <summary>Erster Lauf (Pfad "first") wartet, bis er abgebrochen wird, und liefert danach trotzdem einen Befund; der zweite liefert sofort.</summary>
    private sealed class SlowCheck : IClassicCheck
    {
        public readonly ManualResetEventSlim Started = new();
        public volatile bool FirstCancelled;
        public int Thread = -1;

        public IEnumerable<GuiFinding> Run(ClassicCheckContext context, CancellationToken cancellation)
        {
            Thread = Environment.CurrentManagedThreadId;
            if (context.DocumentPath == "first")
            {
                Started.Set();
                FirstCancelled = cancellation.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                return new[] { new GuiFinding(GuiFindingKind.SpriteMissing, GuiFindingSeverity.Error, "first", null, "first", 1) };
            }

            return new[] { new GuiFinding(GuiFindingKind.FontMissing, GuiFindingSeverity.Error, "second", null, "second", 2) };
        }
    }

    [Fact]
    public void Check_RunsOffUiThreadAndCancels()
    {
        var install = Path.Combine(Path.GetTempPath(), "pmt-findings-" + Guid.NewGuid().ToString("N"));
        try
        {
            Sta.Run(() =>
            {
                var workspace = NewWorkspace(install);
                var (view, document) = Designer();
                var check = new SlowCheck();
                view.Checks = new IClassicCheck[] { check };
                var ui = Environment.CurrentManagedThreadId;

                view.ScheduleChecks(new ClassicCheckContext(workspace, document, "first", null, IsModFile: true, OverflowEnabled: false));
                PumpUntil(() => check.Started.IsSet);
                Assert.NotEqual(ui, check.Thread);

                view.ScheduleChecks(new ClassicCheckContext(workspace, document, "second", null, IsModFile: true, OverflowEnabled: false));
                PumpUntil(() => view.Findings.Count == 1 && view.Findings[0].Message == "second");
                Assert.True(check.FirstCancelled);

                // Der abgebrochene Lauf kommt später noch an, darf das Ergebnis aber nicht ersetzen.
                Thread.Sleep(100);
                WpfWindows.Pump(5);
                Assert.Equal("second", Assert.Single(view.Findings).Message);
                Assert.Equal(GuiTexts.FindingsHeader(1), view.FindingsHeaderText);
            });
        }
        finally
        {
            if (Directory.Exists(install))
            {
                Directory.Delete(install, recursive: true);
            }
        }
    }

    [Fact]
    public void Findings_TreeMarkAndList()
    {
        Sta.Run(() =>
        {
            var (view, document) = Designer();
            var window = document.Root.Children[0];
            var inner = window.Children.First(c => c.Name == "widget");
            GuiNode? activated = null;
            view.NodeActivated += (_, node) => activated = node;

            view.SetFindings(new[]
            {
                new GuiFinding(GuiFindingKind.SpriteMissing, GuiFindingSeverity.Error, "missing", inner, "a.gui", inner.Line),
                new GuiFinding(GuiFindingKind.AnchorUnknown, GuiFindingSeverity.Warning, "anchor", window, "a.gui", window.Line),
            });

            Assert.Equal(GuiTexts.FindingsHeader(2), view.FindingsHeaderText);
            Assert.Equal(GuiFindingSeverity.Error, view.TreeItemFinding(inner));
            Assert.Equal(GuiFindingSeverity.Warning, view.TreeItemFinding(window));
            Assert.Equal(GuiFindingSeverity.Error, view.PreviewFindingMarks[inner]);
            Assert.Equal(GuiFindingSeverity.Warning, view.PreviewFindingMarks[window]);

            var row = view.FindingRows[0];
            row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            Assert.Same(inner, activated);

            // Der Baum entsteht neu: die Marken kommen wieder, solange die Befunde gelten.
            view.SetTree(GuiElementTree.Build(document, new GuiResolver(GuiIndex.Empty, GuiRules.Empty, document, null), null));
            Assert.Equal(GuiFindingSeverity.Error, view.TreeItemFinding(inner));

            view.SetFindings(Array.Empty<GuiFinding>());
            Assert.Null(view.TreeItemFinding(inner));
            Assert.Equal(GuiTexts.FindingsHeader(0), view.FindingsHeaderText);
        });
    }

    /// <summary>Wirft eine fremde Abbruch-Ausnahme: der Läufer lässt sie durch, der Lauf scheitert, ohne abgebrochen zu sein.</summary>
    private sealed class FaultingCheck : IClassicCheck
    {
        public IEnumerable<GuiFinding> Run(ClassicCheckContext context, CancellationToken cancellation) => throw new OperationCanceledException();
    }

    [Fact]
    public void Check_FaultedRun_ClearsOldFindings()
    {
        var install = Path.Combine(Path.GetTempPath(), "pmt-findings-" + Guid.NewGuid().ToString("N"));
        try
        {
            Sta.Run(() =>
            {
                var workspace = NewWorkspace(install);
                var (view, document) = Designer();
                var window = document.Root.Children[0];
                view.SetFindings(new[] { new GuiFinding(GuiFindingKind.SpriteMissing, GuiFindingSeverity.Error, "old", window, "a.gui", window.Line) });
                Assert.Equal(GuiFindingSeverity.Error, view.TreeItemFinding(window));
                view.Checks = new IClassicCheck[] { new FaultingCheck() };

                view.ScheduleChecks(new ClassicCheckContext(workspace, document, "x.gui", null, IsModFile: true, OverflowEnabled: false));
                PumpUntil(() => view.Findings.Count == 0);

                Assert.Null(view.TreeItemFinding(window));
                Assert.Empty(view.PreviewFindingMarks);
                Assert.Equal(GuiTexts.FindingsHeader(0), view.FindingsHeaderText);
            });
        }
        finally
        {
            if (Directory.Exists(install))
            {
                Directory.Delete(install, recursive: true);
            }
        }
    }

    [Fact]
    public void Finding_FromOldParse_SelectsNothing()
    {
        Sta.Run(() =>
        {
            var (view, document) = Designer();
            var inner = document.Root.Children[0].Children.First(c => c.Name == "widget");
            GuiNode? activated = null;
            view.NodeActivated += (_, node) => activated = node;
            view.SetFindings(new[] { new GuiFinding(GuiFindingKind.SpriteMissing, GuiFindingSeverity.Error, "missing", inner, "a.gui", inner.Line) });

            // Neuer Parse: der Baum zeigt neue Knoten, die Liste noch die alten.
            var reparsed = GuiParser.Parse(WindowGui);
            view.SetTree(GuiElementTree.Build(reparsed, new GuiResolver(GuiIndex.Empty, GuiRules.Empty, reparsed, null), null));
            view.FindingRows[0].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });

            Assert.Null(activated);
        });
    }

    [Fact]
    public void Finding_WithJumpPath_RequestsLocation()
    {
        Sta.Run(() =>
        {
            var (view, document) = Designer();
            GuiLocationEventArgs? location = null;
            GuiNode? activated = null;
            view.FindingLocationRequested += (_, e) => location = e;
            view.NodeActivated += (_, node) => activated = node;

            view.SetFindings(new[]
            {
                new GuiFinding(GuiFindingKind.ScriptWindowMissing, GuiFindingSeverity.Error, "script", document.Root.Children[0], "a.gui", 1, @"C:\mod\common\scripted_guis\x.txt", 7),
            });
            view.FindingRows[0].RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });

            Assert.NotNull(location);
            Assert.Equal(@"C:\mod\common\scripted_guis\x.txt", location!.Path);
            Assert.Equal(7, location.Line);
            Assert.Null(activated);
        });
    }

    /// <summary>Ohne Arbeitsbereich hat die Jomini-Vorschau keinen Textur-Finder (jede Textur fehlte): keine Liste statt Fehlerflut.</summary>
    [Fact]
    public void Jomini_WithoutWorkspace_NoFindings()
    {
        var layout = GuiSettings.Layout;
        var workspace = GuiSettings.Workspace;
        var status = GuiSettings.Status;
        var installDirectory = GuiSettings.InstallDirectory;
        try
        {
            Sta.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                GuiSettings.Layout = GuiLayout.Default;
                GuiSettings.InstallDirectory = null;
                GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(null, "test"));

                var document = new OpenDocument(@"C:\nowhere\probe.gui", "window = {\n\tname = \"main\"\n\tsize = { 100 100 }\n\ticon = {\n\t\tsize = { 10 10 }\n\t\ttexture = \"gfx/missing.dds\"\n\t}\n}\n", null);
                var editor = new ICSharpCode.AvalonEdit.TextEditor { Document = document.Document };
                var view = new GuiDesignerView();
                var controller = new GuiDesignerController(editor, view);
                controller.Show(document);

                // Wartezeit des Laufs (300 ms) plus Vorschau abwarten: danach darf nichts da sein.
                for (var i = 0; i < 80; i++)
                {
                    WpfWindows.Pump();
                    Thread.Sleep(10);
                }

                Assert.Empty(view.Findings);
                Assert.False(view.FindingsVisible);
                controller.Show(null);
            });
        }
        finally
        {
            GuiSettings.Layout = layout;
            GuiSettings.InstallDirectory = installDirectory;
            GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(workspace, status));
        }
    }

    [Fact]
    public void Jomini_VanillaFile_ShowsFindings()
    {
        var install = Path.Combine(Path.GetTempPath(), "pmt-findings-" + Guid.NewGuid().ToString("N"));
        var layout = GuiSettings.Layout;
        var workspace = GuiSettings.Workspace;
        var status = GuiSettings.Status;
        var installDirectory = GuiSettings.InstallDirectory;
        try
        {
            Sta.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                Directory.CreateDirectory(Path.Combine(install, "game", "in_game", "gui"));
                GuiSettings.Layout = GuiLayout.Default;
                GuiSettings.InstallDirectory = null;
                GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(NewWorkspace(install), "test"));

                // Vanilla-Datei (schreibgeschützt) mit fehlender Textur: die Jomini-Liste läuft trotzdem.
                var path = Path.Combine(install, "game", "in_game", "gui", "probe.gui");
                var document = new OpenDocument(path, "window = {\n\tname = \"main\"\n\tsize = { 100 100 }\n\ticon = {\n\t\tsize = { 10 10 }\n\t\ttexture = \"gfx/missing.dds\"\n\t}\n}\n", null);
                var editor = new ICSharpCode.AvalonEdit.TextEditor { Document = document.Document };
                var view = new GuiDesignerView();
                var controller = new GuiDesignerController(editor, view);
                controller.Show(document);

                PumpUntil(() => view.Findings.Any(f => f.Severity == GuiFindingSeverity.Error));
                Assert.True(view.FindingsVisible);
                var error = Assert.Single(view.Findings, f => f.Severity == GuiFindingSeverity.Error);
                Assert.Equal(GuiFindingKind.TextureMissing, error.Kind);
                Assert.Single(view.FindingRows, r => ((GuiFinding)r.Tag).Severity == GuiFindingSeverity.Error);
                controller.Show(null);
            });
        }
        finally
        {
            GuiSettings.Layout = layout;
            GuiSettings.InstallDirectory = installDirectory;
            GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(workspace, status));
            if (Directory.Exists(install))
            {
                Directory.Delete(install, recursive: true);
            }
        }
    }
}
