using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.Editor;

/// <summary>Eine Scope-Warnung der aktiven Datei mit Zeile, für das Panel.</summary>
public sealed record ScopeIssueLine(int Line, int Offset, string Message);

/// <summary>Was der Editor über die Scopes der aktiven Datei weiß: für den Abschnitt "Scope" im Notiz-Panel.</summary>
/// <param name="Available">Scopes lassen sich für diese Datei bestimmen (Regeln geladen, Skriptdatei in common oder events).</param>
/// <param name="Context">Der Scope an der Cursorposition; <see langword="null"/>, wenn es keinen gibt.</param>
/// <param name="Issues">Die Scope-Warnungen der Datei.</param>
public sealed record ScopeInsight(bool Available, ScopeContext? Context, IReadOnlyList<ScopeIssueLine> Issues);

/// <summary>
/// Unterstreicht Trigger und Effects, die im (sicheren) Scope an ihrer Stelle nicht gelten, mit einer roten Wellenlinie. Geprüft wird die ganze Datei kurz
/// nach der letzten Änderung im Hintergrund; die Fundstellen wandern bis dahin mit dem Text mit. Der Tooltip zur Wellenlinie erscheint beim Verweilen der Maus.
/// </summary>
internal sealed class ScopeWarningController : IBackgroundRenderer
{
    private static readonly string[] ScriptExtensions = { ".txt", ".gui", ".gfx" };
    private static readonly Brush FallbackBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
    private static readonly Brush WarningFallbackBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0x9A, 0x1A));

    private readonly TextEditor _editor;
    private readonly Func<string?> _currentFilePath;
    private readonly DispatcherTimer _timer;
    private TextSegmentCollection<IssueSegment>? _segments;
    private IReadOnlyList<ScopeIssueLine> _issues = Array.Empty<ScopeIssueLine>();
    private ToolTip? _tip;
    private int _version;

    private sealed class IssueSegment : TextSegment
    {
        public string Message { get; init; } = string.Empty;

        public bool Severe { get; init; } = true;
    }

    /// <summary>
    /// Die Präfix-Prüfung für die Datei: nur für Skriptdateien gefragt (sie liest Wurzeln und Regeln auf dem UI-Thread); scheitert sie, gibt es
    /// keine Prüfung statt eines Fehlers.
    /// </summary>
    internal static Func<string, IReadOnlyList<EditorWarning>>? PrefixCheckFor(string? path, Func<string?, Func<string, IReadOnlyList<EditorWarning>>?>? checker)
    {
        if (path is null || checker is null || !ScriptExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return checker(path);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Scope-Warnungen (schwer) und weitere Warnungen (Präfixe) in einer Liste, nach Textstelle geordnet.</summary>
    internal static IReadOnlyList<EditorWarning> Merge(IReadOnlyList<ScopeIssue> scope, IReadOnlyList<EditorWarning> other) =>
        scope.Select(issue => new EditorWarning(issue.Start, issue.Length, issue.Message, true))
            .Concat(other)
            .OrderBy(warning => warning.Start)
            .ToList();

    public ScopeWarningController(TextEditor editor, Func<string?> currentFilePath)
    {
        _editor = editor;
        _currentFilePath = currentFilePath;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            _ = ComputeAsync();
        };

        _editor.TextArea.TextView.BackgroundRenderers.Add(this);
        _editor.TextChanged += (_, _) => Schedule();
        _editor.DocumentChanged += (_, _) => Reset();
        _editor.MouseHover += OnMouseHover;
        _editor.MouseHoverStopped += (_, _) => CloseTip();
        _editor.MouseLeave += (_, _) => CloseTip();
        _editor.PreviewMouseDown += (_, _) => CloseTip();
        ScopeSettings.Changed += (_, _) => Refresh();
    }

    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>Die Warnungen der aktiven Datei (bei ausgeschalteter Prüfung leer).</summary>
    public IReadOnlyList<ScopeIssueLine> Issues => _issues;

    public event EventHandler? IssuesChanged;

    /// <summary>Die Meldung an dieser Textposition, falls dort eine Warnung steht.</summary>
    public string? MessageAt(int offset) =>
        ScopeSettings.WarningsEnabled ? _segments?.FindSegmentsContaining(offset).FirstOrDefault()?.Message : null;

    /// <summary>Wendet Schalter und Datenquelle an und prüft sofort neu (z. B. beim Wechsel des Dokuments).</summary>
    public void Refresh()
    {
        _timer.Stop();
        if (IsActive)
        {
            _ = ComputeAsync();
        }
        else
        {
            Clear();
        }
    }

    private bool IsActive => (ScopeSettings.WarningsEnabled || ScopeSettings.PanelEnabled) && (ScopeSettings.Engine is not null || ScopeSettings.PrefixChecker is not null);

    private void Reset()
    {
        _segments = null;
        Clear();
        Refresh();
    }

    private void Schedule()
    {
        if (IsActive)
        {
            _timer.Stop();
            _timer.Start();
        }
    }

    private void Clear()
    {
        _version++;
        var changed = _segments is not null || _issues.Count > 0;
        _segments = null;
        _issues = Array.Empty<ScopeIssueLine>();
        _editor.TextArea.TextView.InvalidateLayer(Layer);
        if (changed)
        {
            IssuesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ComputeAsync()
    {
        var path = _currentFilePath();
        var engine = ScopeSettings.Engine;
        var prefixCheck = PrefixCheckFor(path, ScopeSettings.PrefixChecker);
        if ((engine is null && prefixCheck is null)
            || (path is not null && !ScriptExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)))
        {
            Clear();
            return;
        }

        var document = _editor.Document;
        var version = ++_version;
        var text = document.Text;

        IReadOnlyList<EditorWarning> found;
        try
        {
            found = await Task.Run(() => Merge(engine?.FindIssues(text, path) ?? Array.Empty<ScopeIssue>(), prefixCheck?.Invoke(text) ?? Array.Empty<EditorWarning>()));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Eine Prüfung, die scheitert, darf den Editor nicht stören: dann gibt es eben keine Warnungen.
            found = Array.Empty<EditorWarning>();
        }

        if (version != _version || !ReferenceEquals(document, _editor.Document))
        {
            return;
        }

        var segments = new TextSegmentCollection<IssueSegment>(document);
        var lines = new List<ScopeIssueLine>(found.Count);
        foreach (var issue in found)
        {
            if (issue.Start < 0 || issue.Start + issue.Length > document.TextLength)
            {
                continue;
            }

            segments.Add(new IssueSegment { StartOffset = issue.Start, Length = issue.Length, Message = issue.Message, Severe = issue.Severe });
            lines.Add(new ScopeIssueLine(document.GetLineByOffset(issue.Start).LineNumber, issue.Start, issue.Message));
        }

        _segments = segments;
        _issues = lines;
        _editor.TextArea.TextView.InvalidateLayer(Layer);
        IssuesChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- Zeichnen

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!ScopeSettings.WarningsEnabled || _segments is null || textView.Document is null || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
        {
            return;
        }

        var viewStart = textView.VisualLines[0].FirstDocumentLine.Offset;
        var viewEnd = textView.VisualLines[^1].LastDocumentLine.EndOffset;
        var severePen = new Pen(_editor.TryFindResource("Theme.Danger") as Brush ?? FallbackBrush, 1);
        var hintPen = new Pen(_editor.TryFindResource("Theme.Warning") as Brush ?? WarningFallbackBrush, 1);

        foreach (var segment in _segments.FindOverlappingSegments(viewStart, viewEnd - viewStart))
        {
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.DrawGeometry(null, segment.Severe ? severePen : hintPen, CreateSquiggle(rect.BottomLeft, rect.BottomRight));
            }
        }
    }

    private static StreamGeometry CreateSquiggle(Point start, Point end)
    {
        const double step = 2.5;
        var count = Math.Max((int)((end.X - start.X) / step) + 1, 4);
        var points = new Point[count];
        for (var i = 0; i < count; i++)
        {
            points[i] = new Point(start.X + i * step, start.Y - ((i + 1) % 2 == 0 ? step : 0));
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false, false);
            context.PolyLineTo(points.Skip(1).ToArray(), true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    // ---- Tooltip (nur, wenn der Hover-Tipp der Script-Datenbank nichts zeigt; sonst steht die Meldung dort)

    private void OnMouseHover(object sender, MouseEventArgs e)
    {
        if (!ScopeSettings.WarningsEnabled || _segments is null)
        {
            return;
        }

        var position = _editor.GetPositionFromPoint(e.GetPosition(_editor));
        if (position is null || MessageAt(_editor.Document.GetOffset(position.Value.Location)) is not { } message)
        {
            return;
        }

        _tip ??= CreateTip();
        _tip.Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 560 };
        _tip.IsOpen = true;
        e.Handled = true;
    }

    private ToolTip CreateTip()
    {
        var tip = new ToolTip { PlacementTarget = _editor, Placement = PlacementMode.Mouse };
        tip.SetResourceReference(Control.BackgroundProperty, "Theme.PanelBackground");
        tip.SetResourceReference(Control.BorderBrushProperty, "Theme.ControlBorder");
        tip.SetResourceReference(Control.ForegroundProperty, "Theme.Text");
        return tip;
    }

    private void CloseTip()
    {
        if (_tip is not null)
        {
            _tip.IsOpen = false;
        }
    }
}
