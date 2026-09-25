using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.Editor;

/// <summary>
/// Hält die Statusleiste unter dem Editor aktuell: zeigt den Scope am Cursor samt dem Weg dorthin. Die Berechnung läuft kurz nach der letzten Cursorbewegung
/// oder Textänderung, damit sie beim Tippen nicht bei jedem Zeichen anfällt.
/// </summary>
internal sealed class ScopeStatusController
{
    private static readonly string[] ScriptExtensions = { ".txt", ".gui", ".gfx" };

    private readonly TextEditor _editor;
    private readonly Func<string?> _currentFilePath;
    private readonly Target[] _targets;
    private readonly DispatcherTimer _timer;

    /// <summary>Eine Leiste, die den Scope zeigt, samt ihrem Schalter (unter dem Editor bzw. unter der Pfadleiste).</summary>
    public sealed record Target(FrameworkElement Bar, TextBlock Text, Func<bool> Enabled);

    public ScopeStatusController(TextEditor editor, Func<string?> currentFilePath, params Target[] targets)
    {
        _editor = editor;
        _currentFilePath = currentFilePath;
        _targets = targets;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Update();
        };

        _editor.TextArea.Caret.PositionChanged += (_, _) => Schedule();
        _editor.TextChanged += (_, _) => Schedule();
        ScopeSettings.Changed += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>Wendet Schalter und Datenquelle an und berechnet sofort neu (z. B. beim Wechsel des Dokuments).</summary>
    public void Refresh()
    {
        foreach (var target in _targets)
        {
            target.Bar.Visibility = target.Enabled() ? Visibility.Visible : Visibility.Collapsed;
        }

        if (AnyEnabled)
        {
            Update();
        }
    }

    // Auch das Panel braucht die Analyse, selbst wenn keine Leiste sichtbar ist.
    private bool AnyEnabled => ScopeSettings.PanelEnabled || _targets.Any(t => t.Enabled());

    /// <summary>Die Analyse an der letzten Cursorposition; <see langword="null"/>, wenn es keine gibt (keine Regeln, keine Skriptdatei, außerhalb eines Objekts).</summary>
    public ScopeContext? Context { get; private set; }

    /// <summary>Ob für die aktive Datei überhaupt Scopes bestimmt werden können (Regeln vorhanden, Skriptdatei in <c>common</c> oder <c>events</c>).</summary>
    public bool Available { get; private set; }

    /// <summary>Die Analyse wurde neu berechnet.</summary>
    public event EventHandler? ContextChanged;

    private void Schedule()
    {
        if (AnyEnabled)
        {
            _timer.Stop();
            _timer.Start();
        }
    }

    private void Update()
    {
        var (text, resource, tooltip, context, available) = Describe();
        Context = context;
        Available = available;
        foreach (var target in _targets)
        {
            target.Text.Text = text;
            target.Text.ToolTip = tooltip;
            if (resource is not null)
            {
                target.Text.SetResourceReference(TextBlock.ForegroundProperty, resource);
            }
        }

        ContextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Text, Farbschlüssel (<see langword="null"/> = unverändert) und Tooltip der Leisten für die aktuelle Cursorposition.</summary>
    private (string Text, string? Resource, string? Tooltip, ScopeContext? Context, bool Available) Describe()
    {
        if (ScopeSettings.Engine is not { } engine)
        {
            return ("Scope: Script-Datenbank noch nicht geladen", "Theme.TextMuted", null, null, false);
        }

        var path = _currentFilePath();
        if (path is not null && !ScriptExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        {
            return (string.Empty, null, null, null, false);
        }

        var context = engine.Analyze(_editor.Document.Text, _editor.CaretOffset, path);

        var lines = new List<string>();
        if (context.FieldPath is not null)
        {
            lines.Add($"Feld: {context.FieldPath}" + (context.StartScope is null ? string.Empty : $"  (Start-Scope {context.StartScope}, Zustimmung {context.StartSupport:P0})"));
        }

        if (context.SavedScopes.Count > 0)
        {
            lines.Add("Gespeichert: " + string.Join(", ", context.SavedScopes.Select(s => $"scope:{s.Key} = {s.Value ?? "?"}")));
        }

        if (ScopeSettings.Info.Length > 0)
        {
            lines.Add(ScopeSettings.Info);
        }

        return (context.Describe(), context.Confident ? "Theme.Text" : "Theme.TextMuted", lines.Count == 0 ? null : string.Join("\n", lines), context, ScopeEngine.FolderOf(path) is not null);
    }
}
