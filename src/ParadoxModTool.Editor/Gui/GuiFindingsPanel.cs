using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Gemeinsame Befundliste von Designer und Inspektor: aufklappbare Liste "Prüfung (N)", oben Fehler und Warnungen, darunter die Hinweise
/// ("Vorschau-Hinweise" bei Jomini, "Hinweise" klassisch). Kennt weder Baum noch Vorschau; ein Klick meldet den Befund (<see cref="FindingActivated"/>),
/// der Gastgeber wählt das Element bzw. öffnet die Datei. Verborgen, bis zum ersten Mal Befunde gesetzt werden.
/// </summary>
internal sealed class GuiFindingsPanel : Expander
{
    private readonly ListBox _list = new() { MaxHeight = 160, BorderThickness = new Thickness(0) };
    private readonly TextBlock _header = new() { FontWeight = FontWeights.SemiBold };
    private IReadOnlyList<GuiFinding> _findings = Array.Empty<GuiFinding>();
    private bool _jominiHeadings;

    public GuiFindingsPanel()
    {
        IsExpanded = true;
        Visibility = Visibility.Collapsed;
        Margin = new Thickness(2, 4, 2, 2);
        _header.Text = GuiTexts.FindingsHeader(0);
        Header = _header;
        ToolTip = GuiTexts.FindingsTip;
        Content = _list;
    }

    /// <summary>Klick auf eine Befundzeile.</summary>
    public event EventHandler<GuiFinding>? FindingActivated;

    /// <summary>Jomini: Hinweise unter "Vorschau-Hinweise" und der Tooltip für alle Dateien; sonst klassisch "Hinweise" (nur Mod-Dateien).</summary>
    public bool JominiHeadings
    {
        get => _jominiHeadings;
        set
        {
            if (_jominiHeadings == value)
            {
                return;
            }

            _jominiHeadings = value;
            ToolTip = value ? GuiTexts.FindingsTipJomini : GuiTexts.FindingsTip;
            Fill();
        }
    }

    /// <summary>Die gezeigten Befunde.</summary>
    public IReadOnlyList<GuiFinding> Findings => _findings;

    /// <summary>Überschrift "Prüfung (N)", N = alle Befunde.</summary>
    public string HeaderText => _header.Text;

    /// <summary>Befundzeilen in Listenreihenfolge (ohne Gruppenüberschriften).</summary>
    public IReadOnlyList<ListBoxItem> Rows => _list.Items.OfType<ListBoxItem>().ToList();

    /// <summary>Zeigt die Befunde und macht die Liste sichtbar.</summary>
    public void SetFindings(IReadOnlyList<GuiFinding> findings)
    {
        _findings = findings;
        Visibility = Visibility.Visible;
        Fill();
    }

    /// <summary>Leert die Befunde und verbirgt die Liste (kein prüfbarer Stand).</summary>
    public void Hide()
    {
        SetFindings(Array.Empty<GuiFinding>());
        Visibility = Visibility.Collapsed;
    }

    /// <summary>Schwerster Befund (Fehler oder Warnung) je Element; Hinweise und Befunde ohne Element markieren nichts.</summary>
    public static IReadOnlyDictionary<GuiNode, GuiFindingSeverity> Marks(IReadOnlyList<GuiFinding> findings)
    {
        var marks = new Dictionary<GuiNode, GuiFindingSeverity>();
        foreach (var finding in findings)
        {
            if (finding.Node is { } node && finding.Severity != GuiFindingSeverity.Hint
                && (!marks.TryGetValue(node, out var current) || finding.Severity < current))
            {
                marks[node] = finding.Severity;
            }
        }

        return marks;
    }

    /// <summary>Befundtexte (Fehler und Warnungen) eines Elements, z. B. für den Tooltip seines Punkts im Baum.</summary>
    public string NodeTip(GuiNode node) =>
        string.Join("\n", _findings.Where(f => ReferenceEquals(f.Node, node) && f.Severity != GuiFindingSeverity.Hint).Select(f => f.Message));

    internal static string SeverityText(GuiFindingSeverity severity) => severity switch
    {
        GuiFindingSeverity.Error => GuiTexts.FindingError,
        GuiFindingSeverity.Warning => GuiTexts.FindingWarning,
        _ => GuiTexts.FindingHint,
    };

    /// <summary>Runder Punkt in der Farbe der Schwere: Fehler <c>Theme.Danger</c>, Warnung <c>Theme.Warning</c>, Hinweis <c>Theme.Accent</c>.</summary>
    internal static FrameworkElement FindingDot(GuiFindingSeverity severity, string? tip)
    {
        var dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = tip };
        dot.SetResourceReference(Shape.FillProperty, severity switch
        {
            GuiFindingSeverity.Error => "Theme.Danger",
            GuiFindingSeverity.Warning => "Theme.Warning",
            _ => "Theme.Accent",
        });
        return dot;
    }

    /// <summary>Liste neu: Fehler vor Warnungen (sonst in Lieferreihenfolge), dann die Hinweise; je Gruppe eine Überschrift, wenn sie Zeilen hat.</summary>
    private void Fill()
    {
        _header.Text = GuiTexts.FindingsHeader(_findings.Count);
        _list.Items.Clear();
        if (_findings.Count == 0)
        {
            _list.Items.Add(Note(GuiTexts.FindingsNone, heading: false));
            return;
        }

        var problems = _findings.Where(f => f.Severity != GuiFindingSeverity.Hint).OrderBy(f => f.Severity).ToList();
        var hints = _findings.Where(f => f.Severity == GuiFindingSeverity.Hint).ToList();
        AddGroup(GuiTexts.FindingsGroupProblems, problems);
        AddGroup(_jominiHeadings ? GuiTexts.FindingsGroupPreviewNotes : GuiTexts.FindingsGroupNotes, hints);
    }

    private void AddGroup(string heading, IReadOnlyList<GuiFinding> findings)
    {
        if (findings.Count == 0)
        {
            return;
        }

        _list.Items.Add(Note(heading, heading: true));
        foreach (var finding in findings)
        {
            _list.Items.Add(FindingRow(finding));
        }
    }

    /// <summary>Text ohne Befund in der Liste (Gruppenüberschrift oder "Keine Probleme gefunden"); kein <see cref="ListBoxItem"/>, also keine Zeile.</summary>
    private static TextBlock Note(string text, bool heading)
    {
        var note = new TextBlock { Text = text, Margin = new Thickness(4, heading ? 4 : 2, 4, 2), FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal };
        note.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return note;
    }

    private ListBoxItem FindingRow(GuiFinding finding)
    {
        var row = new DockPanel();
        var icon = FindingDot(finding.Severity, SeverityText(finding.Severity));
        icon.Margin = new Thickness(0, 0, 6, 0);
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        var path = finding.JumpPath ?? finding.FilePath;
        var line = finding.JumpPath is not null ? finding.JumpLine : finding.Line;
        if (path is not null)
        {
            var where = new TextBlock { Text = System.IO.Path.GetFileName(path) + ":" + line, Margin = new Thickness(8, 0, 0, 0) };
            where.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            DockPanel.SetDock(where, Dock.Right);
            row.Children.Add(where);
        }

        row.Children.Add(new TextBlock { Text = finding.Message, TextTrimming = TextTrimming.CharacterEllipsis });
        var item = new ListBoxItem { Content = row, Tag = finding, ToolTip = finding.Message };
        item.MouseLeftButtonUp += (_, e) =>
        {
            FindingActivated?.Invoke(this, finding);
            e.Handled = true;
        };
        return item;
    }
}
