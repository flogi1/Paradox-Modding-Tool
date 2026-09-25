using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace ParadoxModTool.Syntax;

/// <summary>
/// Lädt community-editierbare AvalonEdit-XSHD-Highlighting-Definitionen aus
/// syntax-templates/ (siehe Architektur-Spezifikation, Abschnitt 15).
/// Eigener Klassenname (statt "HighlightingLoader"), um nicht mit
/// ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader zu kollidieren.
/// </summary>
public static class XshdHighlightingLoader
{
    public static IHighlightingDefinition LoadFromFile(string xshdPath) => LoadFromFile(xshdPath, null);

    /// <summary>
    /// Lädt die Definition und ersetzt dabei die Vordergrundfarbe der benannten Farben (z. B. "Comment") durch die Werte aus
    /// <paramref name="colorOverrides"/> (Farbname auf Hex, #RRGGBB) - so bestimmt das aktive Theme die Syntaxfarben, während
    /// die xshd-Datei weiterhin Struktur und Standardwerte liefert. Ungültige Werte werden ignoriert.
    /// </summary>
    public static IHighlightingDefinition LoadFromFile(string xshdPath, IReadOnlyDictionary<string, string>? colorOverrides)
    {
        using var reader = XmlReader.Create(xshdPath);
        var xshd = HighlightingLoader.LoadXshd(reader);

        if (colorOverrides is not null)
        {
            foreach (var color in xshd.Elements.OfType<XshdColor>())
            {
                if (color.Name is { } name
                    && colorOverrides.TryGetValue(name, out var hex)
                    && TryParse(hex, out var parsed))
                {
                    color.Foreground = new SimpleHighlightingBrush(parsed);
                }
            }
        }

        return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
    }

    private static bool TryParse(string hex, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
            return true;
        }
        catch (FormatException)
        {
            color = default;
            return false;
        }
    }
}
