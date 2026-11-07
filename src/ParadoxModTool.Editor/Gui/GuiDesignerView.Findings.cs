using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Befunde im Designer: die gemeinsame Liste <see cref="GuiFindingsPanel"/> unter dem Eigenschaftenfeld, roter bzw. gelber Punkt im Baum und ein Zeichen
/// in der Vorschau. Die Läufe starten 300 ms nach der letzten Änderung der Szene im Hintergrund (<see cref="GuiFindingsScheduler"/>): klassisch die
/// Prüfungen (nur Mod-Dateien, Plan 3), bei Jomini die Hinweise der Vorschau (alle Dateien).
/// </summary>
public sealed partial class GuiDesignerView
{
    private readonly GuiFindingsPanel _findingsPanel = new();

    // Schwerster Befund (Fehler oder Warnung) je Element und die zuletzt im Baum gezeigte Marke.
    private IReadOnlyDictionary<GuiNode, GuiFindingSeverity> _findingMarks = new Dictionary<GuiNode, GuiFindingSeverity>();
    private readonly Dictionary<GuiNode, GuiFindingSeverity> _shownFindingMarks = new();
    private GuiFindingsScheduler? _checks;

    // Derselbe Lauf mit dem aktuellen "Überstand markieren" (die Vorschau entsteht dafür nicht neu); null ohne prüfbaren Stand.
    private Action? _rescheduleFindings;

    /// <summary>Befund mit Skriptdatei (<see cref="GuiFinding.JumpPath"/>) angeklickt: Datei und Zeile zum Öffnen.</summary>
    public event EventHandler<GuiLocationEventArgs>? FindingLocationRequested;

    /// <summary>Die gezeigten Befunde (für Tests).</summary>
    internal IReadOnlyList<GuiFinding> Findings => _findingsPanel.Findings;

    /// <summary>Überschrift der Liste (für Tests).</summary>
    internal string FindingsHeaderText => _findingsPanel.HeaderText;

    /// <summary>Zeilen der Liste (für Tests).</summary>
    internal IReadOnlyList<ListBoxItem> FindingRows => _findingsPanel.Rows;

    /// <summary>Liste sichtbar (für Tests).</summary>
    internal bool FindingsVisible => _findingsPanel.Visibility == Visibility.Visible;

    /// <summary>Marke im Baum: schwerster Befund (Fehler oder Warnung) des Elements, null ohne (für Tests).</summary>
    internal GuiFindingSeverity? TreeItemFinding(GuiNode node) => _shownFindingMarks.TryGetValue(node, out var severity) ? severity : null;

    /// <summary>Befund-Zeichen der Vorschau (für Tests).</summary>
    internal IReadOnlyDictionary<GuiNode, GuiFindingSeverity> PreviewFindingMarks => _canvas.FindingMarks;

    /// <summary>Klassische Prüfungen der Läufe (Tests setzen eigene).</summary>
    internal IReadOnlyList<IClassicCheck> Checks { get; set; } = ClassicCheckRunner.DefaultChecks;

    /// <summary>
    /// Klassisch: neue Szene bzw. neuer Stand, Prüfung nach der Wartezeit im Hintergrund, der vorige Lauf wird abgebrochen. Null (kein Dokument) und
    /// Dateien außerhalb der Mod brechen ab, leeren die Befunde und verbergen die Liste.
    /// </summary>
    internal void ScheduleChecks(ClassicCheckContext? context)
    {
        if (context is not { IsModFile: true })
        {
            ScheduleFindings(null, jomini: false, reschedule: null);
            return;
        }

        var checks = Checks;
        ScheduleFindings(
            cancellation => ClassicCheckRunner.Run(context, checks, cancellation),
            jomini: false,
            () => ScheduleChecks(context with { OverflowEnabled = GuiSettings.MarkAllOverflow }));
    }

    /// <summary>Jomini: Hinweise der angezeigten Vorschau, für jede Datei (Vanilla, Abhängigkeit, Entwurf). Null (keine Vorschau) verbirgt die Liste.</summary>
    internal void ScheduleJominiFindings(GuiSchematicScene? scene)
    {
        if (scene is null)
        {
            ScheduleFindings(null, jomini: true, reschedule: null);
            return;
        }

        var overflow = GuiSettings.MarkAllOverflow;
        ScheduleFindings(cancellation => JominiFindings.Collect(scene, overflow, cancellation), jomini: true, () => ScheduleJominiFindings(scene));
    }

    /// <summary>Zeigt die Befunde in Liste, Baum und Vorschau.</summary>
    internal void SetFindings(IReadOnlyList<GuiFinding> findings)
    {
        _findingsPanel.SetFindings(findings);
        _findingMarks = GuiFindingsPanel.Marks(findings);
        _canvas.SetFindingMarks(_findingMarks);
        ApplyTreeMarks();
    }

    private void ScheduleFindings(Func<CancellationToken, IReadOnlyList<GuiFinding>>? run, bool jomini, Action? reschedule)
    {
        _rescheduleFindings = reschedule;
        _findingsPanel.JominiHeadings = jomini;
        _checks!.Schedule(run);
        if (run is null)
        {
            _findingsPanel.Hide();
        }
    }

    private FrameworkElement InitializeFindings()
    {
        _checks = new GuiFindingsScheduler(SetFindings, TimeSpan.FromMilliseconds(300));
        _findingsPanel.FindingActivated += (_, finding) => ActivateFinding(finding);

        // "Überstand markieren" schaltet die Überstand-Hinweise: mit demselben Stand neu prüfen.
        _markOverflow.Click += (_, _) => _rescheduleFindings?.Invoke();
        return _findingsPanel;
    }

    /// <summary>Skriptdatei an der Zeile öffnen lassen: derselbe Weg wie ein Befund (auch für die Scripted-GUI-Zeilen im Eigenschaftsfeld).</summary>
    private void RaiseFindingLocation(GuiLocationEventArgs location) => FindingLocationRequested?.Invoke(this, location);

    /// <summary>Klick auf einen Befund: Skriptdatei an der Zeile öffnen, sonst das Element auswählen (wie ein Klick in die Vorschau).</summary>
    private void ActivateFinding(GuiFinding finding)
    {
        if (finding.JumpPath is { } path)
        {
            FindingLocationRequested?.Invoke(this, new GuiLocationEventArgs(path, finding.JumpLine));
        }
        else if (finding.Node is { } node && IsCurrentNode(node))
        {
            _clickedInner = null;
            NodeActivated?.Invoke(this, node);
        }
    }

    /// <summary>
    /// Der Knoten stammt aus dem Stand, den der Baum zeigt. Nach einem neuen Parse zeigen Zeilen bis zum nächsten Prüflauf noch auf alte Knoten; ein Klick
    /// darauf wählt nichts.
    /// </summary>
    private bool IsCurrentNode(GuiNode node) =>
        _treeItems.Keys.FirstOrDefault() is { } shown && ReferenceEquals(shown.Root, node.Root);

    /// <summary>Befundtexte eines Elements für den Tooltip seines Punkts im Baum.</summary>
    private string FindingTip(GuiNode node) => _findingsPanel.NodeTip(node);
}
