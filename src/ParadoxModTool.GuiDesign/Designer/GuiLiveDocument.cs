using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// Syntaxbaum einer Datei, die gerade bearbeitet wird. <see cref="Current"/> ist der letzte verwendbare Stand: ohne Diagnosen über das
/// hinaus, was die Datei beim Öffnen schon hatte (je Art gezählt; einige Vanilla-Dateien haben von Haus aus Klammerfehler). Solange der
/// Text kaputt ist, zeigt der Designer diesen Stand grau mit Hinweis.
/// </summary>
public sealed class GuiLiveDocument
{
    private readonly Dictionary<GuiDiagnosticKind, int> _baseline;

    public GuiLiveDocument(string text)
    {
        Current = GuiParser.Parse(text);
        Latest = Current;
        _baseline = Current.Diagnostics.GroupBy(d => d.Kind).ToDictionary(g => g.Key, g => g.Count());
    }

    public GuiDocument Current { get; private set; }

    public GuiDocument Latest { get; private set; }

    public bool IsStale => !ReferenceEquals(Current, Latest);

    /// <summary>Erste neue Diagnose des letzten Laufs (für den Hinweis), sonst null.</summary>
    public GuiDiagnostic? Problem { get; private set; }

    /// <summary>Parst den Text neu. True, wenn der neue Stand verwendbar ist und <see cref="Current"/> ersetzt hat.</summary>
    public bool Update(string text)
    {
        if (!string.Equals(text, Latest.Text, StringComparison.Ordinal))
        {
            Latest = GuiParser.Parse(text);
        }

        Problem = FirstNewDiagnostic(Latest.Diagnostics);
        if (Problem is not null)
        {
            return false;
        }

        Current = Latest;
        return true;
    }

    private GuiDiagnostic? FirstNewDiagnostic(IReadOnlyList<GuiDiagnostic> diagnostics)
    {
        var seen = new Dictionary<GuiDiagnosticKind, int>();
        foreach (var diagnostic in diagnostics)
        {
            seen[diagnostic.Kind] = seen.GetValueOrDefault(diagnostic.Kind) + 1;
            if (seen[diagnostic.Kind] > _baseline.GetValueOrDefault(diagnostic.Kind))
            {
                return diagnostic;
            }
        }

        return null;
    }
}
