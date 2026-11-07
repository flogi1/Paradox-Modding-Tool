using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Befundliste im GUI-Inspektor: Jomini für jede Datei (auch Vanilla), klassisch nur Mod-Dateien; ein Klick wählt das Element.</summary>
public sealed class GuiInspectorFindingsTests
{
    private const string ProbeGui = "window = {\n\tname = \"main\"\n\tsize = { 100 100 }\n\ticon = {\n\t\tname = \"pic\"\n\t\tsize = { 10 10 }\n\t\ttexture = \"gfx/missing.dds\"\n\t}\n}\n";

    private static void PumpUntil(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            WpfWindows.Pump();
            Thread.Sleep(10);
        }

        Assert.True(condition(), message);
    }

    /// <summary>Arbeitsbereich um eine Probe-Datei; <paramref name="relative"/> liegt unter der Installation (Vanilla) bzw. dem Mod-Ordner.</summary>
    private static GuiWorkspace Workspace(string install, string? modRoot, GuiRules rules, string relative)
    {
        var root = modRoot ?? install;
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ProbeGui);
        return GuiWorkspace.Create(new GuiWorkspaceOptions(install, modRoot, Array.Empty<string>(), rules, null));
    }

    private static GuiInspectTarget ProbeTarget(GuiWorkspace workspace)
    {
        var index = workspace.Index;
        var entry = Assert.Single(
            GuiElementSearch.Entries(index, workspace.CreateResolver(null, null)),
            e => GuiElementSearch.Matches(e, string.Empty, GuiElementFilter.FileRoot, "probe"));
        return GuiInspection.TargetFor(index, entry.Definition);
    }

    private static void WithSettings(string install, string? mod, Action<GuiInspectorWindow> body, GuiRules rules, string relative)
    {
        var layout = GuiSettings.Layout;
        var workspace = GuiSettings.Workspace;
        var status = GuiSettings.Status;
        try
        {
            Sta.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                GuiSettings.Layout = rules.Layout;
                GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(Workspace(install, mod, rules, relative), "test"));
                var inspector = new GuiInspectorWindow { Left = -3000, Top = -3000, ShowActivated = false };
                inspector.Show();
                try
                {
                    body(inspector);
                }
                finally
                {
                    inspector.Close();
                }
            });
        }
        finally
        {
            GuiSettings.Layout = layout;
            GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(workspace, status));
            foreach (var dir in new[] { install, mod })
            {
                if (dir is not null && Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }
    }

    private static string TempDir() => Path.Combine(Path.GetTempPath(), "pmt-inspfind-" + Guid.NewGuid().ToString("N"));

    private static readonly GuiRules ClassicRules = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), GuiLayout.Default with { Dialect = GuiDialect.Classic });

    [Fact]
    public void Inspector_JominiVanillaWindow_ListsFindings()
    {
        WithSettings(TempDir(), null, inspector =>
        {
            inspector.Inspect(ProbeTarget(GuiSettings.Workspace!));
            PumpUntil(() => inspector.Findings.Any(f => f.Severity == GuiFindingSeverity.Error), "Jomini-Befund kam nicht");

            Assert.True(inspector.FindingsVisible);
            var error = Assert.Single(inspector.Findings, f => f.Severity == GuiFindingSeverity.Error);
            Assert.Equal(GuiFindingKind.TextureMissing, error.Kind);
            Assert.Equal(GuiFindingSeverity.Error, inspector.TreeFinding(inspector.TreeElements.First(e => e.Name == "pic")));
        }, GuiRules.Empty, "game/in_game/gui/probe.gui");
    }

    [Fact]
    public void Inspector_ClassicVanillaWindow_NoFindings()
    {
        WithSettings(TempDir(), null, inspector =>
        {
            inspector.Inspect(ProbeTarget(GuiSettings.Workspace!));
            PumpUntil(() => inspector.PreviewScene is not null, "keine Vorschau");
            for (var i = 0; i < 40; i++)
            {
                WpfWindows.Pump();
                Thread.Sleep(10);
            }

            Assert.Empty(inspector.Findings);
            Assert.False(inspector.FindingsVisible);
        }, ClassicRules, "interface/probe.gui");
    }

    [Fact]
    public void Inspector_JominiWithoutWorkspace_NoFindings()
    {
        var layout = GuiSettings.Layout;
        var workspace = GuiSettings.Workspace;
        var status = GuiSettings.Status;
        try
        {
            Sta.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                GuiSettings.Layout = GuiLayout.Default;
                GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(null, "test"));
                var document = GuiParser.Parse(ProbeGui);
                var inspector = new GuiInspectorWindow { Left = -3000, Top = -3000, ShowActivated = false };
                inspector.Show();
                try
                {
                    inspector.Inspect(new GuiInspectTarget(document.Root.Children[0], document, null));
                    PumpUntil(() => inspector.PreviewScene is not null, "keine Vorschau");
                    for (var i = 0; i < 60; i++)
                    {
                        WpfWindows.Pump();
                        Thread.Sleep(10);
                    }

                    Assert.Empty(inspector.Findings);
                    Assert.False(inspector.FindingsVisible);
                }
                finally
                {
                    inspector.Close();
                }
            });
        }
        finally
        {
            GuiSettings.Layout = layout;
            GuiSettingsChanged.Quietly(() => GuiSettings.SetWorkspace(workspace, status));
        }
    }

    [Fact]
    public void Inspector_ActivateFinding_SelectsElement()
    {
        WithSettings(TempDir(), null, inspector =>
        {
            inspector.Inspect(ProbeTarget(GuiSettings.Workspace!));
            PumpUntil(() => inspector.Findings.Any(f => f.Severity == GuiFindingSeverity.Error), "Jomini-Befund kam nicht");
            Assert.NotEqual("pic", inspector.SelectedElement?.Name);

            var row = Assert.Single(inspector.FindingRows, r => ((GuiFinding)r.Tag).Severity == GuiFindingSeverity.Error);
            row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });

            Assert.Equal("pic", inspector.SelectedElement?.Name);
        }, GuiRules.Empty, "game/in_game/gui/probe.gui");
    }
}
