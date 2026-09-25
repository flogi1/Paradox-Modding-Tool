using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ParadoxModTool.VanillaDiff;
using Border = System.Windows.Controls.Border;

namespace ParadoxModTool.App;

/// <summary>
/// Zeigt Leerraum über bestimmten Zeilen eines AvalonEdit-Editors, ohne das Dokument zu verändern: am Zeilenanfang steht ein leeres Inline-Objekt der Dokumentlänge 0,
/// das die Zeile höher macht; der Text sitzt am unteren Rand. Objekthöhe = Zeilen × Zeilenhöhe + Grundlinie (sonst steht der Text eine Zeile zu hoch; im Spike geprüft).
/// Die Positionen sind Text-Anker und wandern beim Tippen mit; nach jedem Diff werden sie neu gesetzt.
/// </summary>
public sealed class AlignmentSpacerGenerator : VisualLineElementGenerator
{
    private readonly List<(TextAnchor Anchor, int Rows)> _gaps = new();
    private readonly List<int> _staleOffsets = new();

    /// <summary>Ersetzt alle Lücken durch die übergebenen (nur die Zeilen der Seite dieses Editors).</summary>
    public void SetGaps(IEnumerable<AlignmentGap> gaps, TextDocument document)
    {
        _staleOffsets.AddRange(_gaps.Where(g => !g.Anchor.IsDeleted).Select(g => g.Anchor.Offset));
        _gaps.Clear();
        foreach (var gap in gaps)
        {
            if (gap.LineCount <= 0 || gap.BeforeLine < 1 || gap.BeforeLine > document.LineCount)
            {
                continue;
            }

            var anchor = document.CreateAnchor(document.GetLineByNumber(gap.BeforeLine).Offset);
            anchor.MovementType = AnchorMovementType.BeforeInsertion;
            _gaps.Add((anchor, gap.LineCount));
            _staleOffsets.Add(anchor.Offset);
        }
    }

    /// <summary>
    /// Bringt die Zeilenhöhen von Zeilen mit alter oder neuer Lücke in AvalonEdit auf den neuen Stand. Ohne das behalten nie gezeichnete Zeilen ihre alte Höhe:
    /// die Gesamthöhe stimmt nicht, und der Gleichlauf der beiden Editoren (Bildlauf nach Pixeln) driftet, je weiter man nach unten scrollt.
    /// Nach <see cref="TextView.Redraw()"/> aufrufen.
    /// </summary>
    public void ApplyHeights(TextView textView)
    {
        if (!textView.IsLoaded)
        {
            // Das Fenster wird erst angezeigt: nach dem Laden erneut versuchen, die Lücken bleiben vorgemerkt.
            RoutedEventHandler onLoaded = null!;
            onLoaded = (_, _) =>
            {
                textView.Loaded -= onLoaded;
                ApplyHeights(textView);
            };
            textView.Loaded += onLoaded;
            return;
        }

        var document = textView.Document;
        var offsets = _staleOffsets.Distinct().ToList();
        _staleOffsets.Clear();
        if (document is null)
        {
            return;
        }

        foreach (var offset in offsets)
        {
            if (offset < 0 || offset > document.TextLength)
            {
                continue;
            }

            try
            {
                textView.GetOrConstructVisualLine(document.GetLineByOffset(offset));
            }
            catch (InvalidOperationException)
            {
                // Die Ansicht ist noch nicht messbar; die Höhe folgt beim ersten Zeichnen der Zeile.
            }
        }
    }

    /// <summary>Anzahl der Leerzeilen über dieser Dokumentzeile.</summary>
    public int RowsBefore(DocumentLine line)
    {
        var rows = 0;
        foreach (var (anchor, count) in _gaps)
        {
            if (!anchor.IsDeleted && anchor.Offset == line.Offset)
            {
                rows += count;
            }
        }

        return rows;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var line = CurrentContext.Document.GetLineByOffset(startOffset);
        return startOffset == line.Offset && RowsBefore(line) > 0 ? startOffset : -1;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        var line = CurrentContext.Document.GetLineByOffset(offset);
        var view = CurrentContext.TextView;
        var height = RowsBefore(line) * view.DefaultLineHeight + view.DefaultBaseline;
        return new InlineObjectElement(0, new Border { Width = 1, Height = height });
    }
}
