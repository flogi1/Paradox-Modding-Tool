using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ParadoxModTool.Scopes;
using ParadoxModTool.ScriptDocs;

namespace ParadoxModTool.Editor;

/// <summary>
/// Hängt Hover-Tipps und Autovervollständigung an einen AvalonEdit-Editor. Beides ist reine Anzeige über der Script-Datenbank
/// (<see cref="ScriptHintSettings.Provider"/>) und lässt sich einzeln abschalten; im Kommentar oder in einer Zeichenkette sowie in
/// Lokalisierungsdateien wird nichts vorgeschlagen. Vorschläge sind alle Namen mit dem getippten Anfang, ohne Prüfung des Kontexts
/// (Trigger-/Effect-Block); die Prüfung des Scopes ist Aufgabe des späteren Scope-Viewers.
/// </summary>
internal sealed class ScriptHintController
{
    private const int MaxCompletionItems = 300;
    private const int MinAutoPrefixLength = 3;
    private const int MaxDescriptionLines = 10;
    private const int MaxHintsInTip = 3;

    private static readonly string[] ScriptExtensions = { ".txt", ".gui", ".gfx" };

    private readonly TextEditor _editor;
    private readonly Func<string?> _currentFilePath;
    private CompletionWindow? _completion;
    private ToolTip? _tip;

    /// <summary>Liefert die Scope-Warnung an einer Textposition (aus dem <see cref="ScopeWarningController"/>); sie ersetzt im Tooltip die Scope-Zeile.</summary>
    public Func<int, string?>? WarningAt { get; set; }

    public ScriptHintController(TextEditor editor, Func<string?> currentFilePath)
    {
        _editor = editor;
        _currentFilePath = currentFilePath;

        _editor.MouseHover += OnMouseHover;
        _editor.MouseHoverStopped += (_, _) => CloseTip();
        _editor.MouseLeave += (_, _) => CloseTip();
        _editor.PreviewMouseDown += (_, _) => CloseTip();
        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Schließt Vorschlagsliste und Tooltip (z. B. beim Wechsel des Dokuments).</summary>
    public void Close()
    {
        _completion?.Close();
        CloseTip();
    }

    private bool IsScriptDocument()
    {
        var path = _currentFilePath();
        return path is null || ScriptExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    // ---- Hover

    private void OnMouseHover(object sender, MouseEventArgs e)
    {
        if (!ScriptHintSettings.HoverEnabled || ScriptHintSettings.Provider is not { } provider || _completion is not null || !IsScriptDocument())
        {
            return;
        }

        if (_editor.GetPositionFromPoint(e.GetPosition(_editor)) is not { } position
            || position.IsAtEndOfLine
            || position.Line < 1
            || position.Line > _editor.Document.LineCount)
        {
            return;
        }

        var line = _editor.Document.GetLineByNumber(position.Line);
        var lineText = _editor.Document.GetText(line.Offset, line.Length);
        if (ScriptWords.WordAt(lineText, position.Column - 1) is not { } word
            || !ScriptWords.IsCodeContext(lineText[..word.Start]))
        {
            return;
        }

        var hints = provider.Lookup(lineText.Substring(word.Start, word.Length));
        if (hints.Count == 0)
        {
            return;
        }

        _tip ??= CreateTip();
        var scopeLine = WarningAt?.Invoke(line.Offset + word.Start)
            ?? ScopeLine(hints, lineText.Substring(word.Start, word.Length), line.Offset + word.Start, filePath: _currentFilePath());
        _tip.Content = BuildTipContent(hints, scopeLine);
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

    /// <summary>
    /// Die Zeile "Hier: country – erlaubt" für den Tooltip, wenn der Name ein Trigger oder Effect ist und der Scope an der Stelle bekannt ist;
    /// sonst <see langword="null"/>. "Nicht erlaubt" nennt der Tooltip nur bei sicherem Scope, sonst "vermutlich".
    /// </summary>
    private string? ScopeLine(IReadOnlyList<ScriptHint> hints, string word, int wordOffset, string? filePath)
    {
        if (!ScopeSettings.HoverEnabled
            || ScopeSettings.Engine is not { } engine
            || !hints.Any(h => h.Kind is ScriptDocKind.Trigger or ScriptDocKind.Effect))
        {
            return null;
        }

        var context = engine.Analyze(_editor.Document.Text, wordOffset, filePath);
        if (context.CurrentScope is null)
        {
            return null;
        }

        var (result, supported) = engine.Check(word, context);
        return result switch
        {
            ScopeCheck.Allowed => $"Hier: {context.CurrentScope} – erlaubt",
            ScopeCheck.NotAllowed when context.Confident => $"Hier: {context.CurrentScope} – gilt nur in {string.Join(", ", supported)}",
            ScopeCheck.NotAllowed => $"Hier: {context.CurrentScope} (unsicher) – vermutlich nur in {string.Join(", ", supported)}",
            _ => null,
        };
    }

    private static UIElement BuildTipContent(IReadOnlyList<ScriptHint> hints, string? scopeLine = null)
    {
        var panel = new StackPanel { MaxWidth = 560 };
        foreach (var hint in hints.Take(MaxHintsInTip))
        {
            if (panel.Children.Count > 0)
            {
                panel.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 6) });
            }

            var title = new TextBlock { TextWrapping = TextWrapping.Wrap };
            title.Inlines.Add(new System.Windows.Documents.Run(hint.Name) { FontWeight = FontWeights.Bold });
            title.Inlines.Add(new System.Windows.Documents.Run("   " + ScriptDocLogFiles.DisplayNameOf(hint.Kind)) { Foreground = Brushes.Gray });
            panel.Children.Add(title);

            var meta = MetaLine(hint);
            if (meta.Length > 0)
            {
                panel.Children.Add(new TextBlock { Text = meta, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            }

            var description = ShortDescription(hint);
            if (description.Length > 0)
            {
                panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            }
        }

        if (scopeLine is not null)
        {
            panel.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 6) });
            panel.Children.Add(new TextBlock { Text = scopeLine, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        }

        if (hints.Count > MaxHintsInTip)
        {
            panel.Children.Add(new TextBlock { Text = $"… und {hints.Count - MaxHintsInTip} weitere", Foreground = Brushes.Gray, Margin = new Thickness(0, 6, 0, 0) });
        }

        return panel;
    }

    private static string MetaLine(ScriptHint hint)
    {
        var parts = new List<string>();
        if (hint.Scopes.Count > 0)
        {
            parts.Add((hint.Kind == ScriptDocKind.Modifier ? "Kategorien: " : "Scopes: ") + string.Join(", ", hint.Scopes));
        }

        if (hint.Targets.Count > 0)
        {
            parts.Add((hint.Kind == ScriptDocKind.EventTarget ? "Ergebnis: " : "Ziele: ") + string.Join(", ", hint.Targets));
        }

        if (hint.IsOutdated)
        {
            parts.Add("veraltet");
        }
        else if (hint.IsNew)
        {
            parts.Add("neu");
        }

        return string.Join("  ·  ", parts);
    }

    /// <summary>Die Beschreibung des Spiels, auf die ersten Zeilen gekürzt.</summary>
    private static string ShortDescription(ScriptHint hint)
    {
        var lines = hint.Description.Split('\n');
        return lines.Length <= MaxDescriptionLines
            ? hint.Description
            : string.Join("\n", lines.Take(MaxDescriptionLines)) + "\n…";
    }

    // ---- Autovervollständigung

    private void OnTextEntering(object? sender, TextCompositionEventArgs e)
    {
        // Ein Zeichen, das nicht zu einem Namen gehört (Leerzeichen, "=", "{" …), beendet die Auswahl, ohne etwas einzufügen.
        if (_completion is not null && e.Text.Length > 0 && !ScriptWords.IsWordChar(e.Text[0]))
        {
            _completion.Close();
        }
    }

    private void OnTextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (_completion is null && e.Text.Length == 1 && ScriptWords.IsWordChar(e.Text[0]))
        {
            ShowCompletion(manual: false);
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control && ScriptHintSettings.AutoCompleteEnabled)
        {
            e.Handled = true;
            ShowCompletion(manual: true);
        }
    }

    private void ShowCompletion(bool manual)
    {
        if (!ScriptHintSettings.AutoCompleteEnabled || ScriptHintSettings.Provider is not { } provider || _completion is not null || !IsScriptDocument())
        {
            return;
        }

        var caret = _editor.CaretOffset;
        var line = _editor.Document.GetLineByOffset(caret);
        var lineText = _editor.Document.GetText(line.Offset, line.Length);
        var column = caret - line.Offset;
        var startColumn = ScriptWords.PrefixStart(lineText, column);
        var prefix = lineText[startColumn..column];

        if (prefix.Length < (manual ? 1 : MinAutoPrefixLength)
            || !(char.IsLetter(prefix[0]) || prefix[0] == '_')
            || !ScriptWords.IsCodeContext(lineText[..startColumn]))
        {
            return;
        }

        var hints = provider.Complete(prefix, MaxCompletionItems);
        if (hints.Count == 0 || (hints.Count == 1 && hints[0].Name == prefix))
        {
            return;
        }

        var window = new CompletionWindow(_editor.TextArea) { StartOffset = line.Offset + startColumn };
        foreach (var hint in hints)
        {
            window.CompletionList.CompletionData.Add(new CompletionItem(hint));
        }

        window.CompletionList.SelectItem(prefix);
        window.Closed += (_, _) => _completion = null;
        _completion = window;
        window.Show();
    }

    private sealed class CompletionItem : ICompletionData
    {
        public CompletionItem(ScriptHint hint)
        {
            Text = hint.Name;

            var content = new StackPanel { Orientation = Orientation.Horizontal, Opacity = hint.IsOutdated ? 0.6 : 1.0 };
            content.Children.Add(new TextBlock { Text = hint.Name });
            content.Children.Add(new TextBlock
            {
                Text = ScriptDocLogFiles.DisplayNameOf(hint.Kind) + (hint.IsOutdated ? " · veraltet" : string.Empty),
                Foreground = Brushes.Gray,
                Margin = new Thickness(12, 0, 0, 0),
            });
            Content = content;

            var meta = MetaLine(hint);
            var description = ShortDescription(hint);
            Description = new TextBlock
            {
                Text = (meta.Length > 0 ? meta + "\n\n" : string.Empty) + description,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 480,
            };
        }

        public ImageSource? Image => null;

        public string Text { get; }

        public object Content { get; }

        public object Description { get; }

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
            textArea.Document.Replace(completionSegment, Text);
    }
}
