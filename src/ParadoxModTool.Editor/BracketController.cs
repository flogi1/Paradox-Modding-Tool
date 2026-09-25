using System.IO;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.Editor;

/// <summary>
/// Markiert im Editor die Klammer am Cursor samt ihrem Gegenstück und springt auf Wunsch zu dieser (Strg+B). Die Markierung folgt dem Cursor
/// und lässt sich über <see cref="BracketSettings"/> abschalten; der Sprung funktioniert unabhängig davon.
/// </summary>
internal sealed class BracketController : IBackgroundRenderer
{
    private static readonly string[] LocalizationExtensions = { ".yml", ".yaml" };
    private static readonly Brush FallbackBrush = CreateFallbackBrush();

    private readonly TextEditor _editor;
    private readonly Func<string?> _currentFilePath;
    private BracketMatch? _match;

    public BracketController(TextEditor editor, Func<string?> currentFilePath)
    {
        _editor = editor;
        _currentFilePath = currentFilePath;

        _editor.TextArea.TextView.BackgroundRenderers.Add(this);
        _editor.TextArea.Caret.PositionChanged += (_, _) => Refresh();
        _editor.TextChanged += (_, _) => Refresh();
        BracketSettings.Changed += (_, _) => Refresh();
    }

    // Unter dem Text, aber über dem Hintergrund: die Markierung färbt die Zelle der Klammer, das Zeichen bleibt lesbar.
    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>Berechnet die Markierung neu (nach Cursorbewegung, Textänderung, Dokumentwechsel oder Schalteränderung).</summary>
    public void Refresh()
    {
        var match = BracketSettings.HighlightEnabled ? FindMatch() : null;
        if (match != _match)
        {
            _match = match;
            _editor.TextArea.TextView.InvalidateLayer(Layer);
        }
    }

    /// <summary>
    /// Setzt den Cursor hinter das Gegenstück der Klammer am Cursor. Der Cursor landet immer HINTER der Klammer, damit ein
    /// zweiter Aufruf zurückspringt. Ohne Klammer oder ohne Gegenstück geschieht nichts; das Ergebnis sagt, ob gesprungen wurde.
    /// </summary>
    public bool GoToMatching()
    {
        if (FindMatch() is not { } match)
        {
            return false;
        }

        _editor.CaretOffset = match.Partner + 1;
        _editor.TextArea.Caret.BringCaretToView();
        return true;
    }

    private BracketMatch? FindMatch()
    {
        var document = _editor.Document;
        if (document is null)
        {
            return null;
        }

        var caret = _editor.CaretOffset;

        // Vorprüfung ohne Textkopie: bei fast jeder Cursorbewegung steht keine Klammer daneben.
        var beforeIsBracket = caret > 0 && "{}()[]".Contains(document.GetCharAt(caret - 1));
        var afterIsBracket = caret < document.TextLength && "{}()[]".Contains(document.GetCharAt(caret));
        if (!beforeIsBracket && !afterIsBracket)
        {
            return null;
        }

        var path = _currentFilePath();
        var localization = path is not null && LocalizationExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        return BracketMatcher.Find(document.Text, caret, localization);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_match is not { } match || textView.Document is null)
        {
            return;
        }

        var brush = (_editor.TryFindResource("Theme.EditorBracketMatch") as Brush) ?? FallbackBrush;
        foreach (var offset in new[] { match.Anchor, match.Partner })
        {
            var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 2 };
            builder.AddSegment(textView, new ICSharpCode.AvalonEdit.Document.TextSegment { StartOffset = offset, Length = 1 });
            if (builder.CreateGeometry() is { } geometry)
            {
                drawingContext.DrawGeometry(brush, null, geometry);
            }
        }
    }

    private static Brush CreateFallbackBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD9, 0x8A));
        brush.Freeze();
        return brush;
    }
}
