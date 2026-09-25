using System.IO;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Editor;
using ParadoxModTool.Syntax;
using ParadoxModTool.Theming;

namespace ParadoxModTool.App;

/// <summary>Anbindung des Hauptfensters an das Theme (Paket 12b): Themenwechsel, Kategorie- und Syntaxfarben (Auswahl im Einstellungsfenster).</summary>
public partial class MainWindow
{
    private string? _xshdPath;

    private void InitializeThemeSupport()
    {
        ThemeManager.ThemeChanged += (_, _) => OnThemeChanged();
        ApplyCategoryColors();
    }

    private void OnThemeChanged()
    {
        ApplyCategoryColors();
        ReloadHighlighting();
    }

    /// <summary>Kategorie-Farben des aktiven Themes (samt persönlicher Anpassungen) an den Klassifizierer und die offenen Tabs geben.</summary>
    private void ApplyCategoryColors()
    {
        FileCategoryClassifier.SetThemeColors(ThemeManager.EffectiveCategoryColors);
        RefreshAllDocumentColors();
        foreach (var pane in AllPanes)
        {
            pane.RebuildCategoryFilter();
        }
    }

    /// <summary>Syntaxfarben des aktiven Themes als Farbname (wie in der xshd) auf Hex.</summary>
    private static Dictionary<string, string> CurrentSyntaxColors() =>
        ThemeManager.Current.Colors
            .Where(kv => kv.Key.StartsWith(ThemeTokens.SyntaxPrefix, StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key[ThemeTokens.SyntaxPrefix.Length..], kv => kv.Value);

    /// <summary>Lädt die Syntax-Definition mit den Farben des aktiven Themes neu und wendet sie auf alle offenen Dokumente an.</summary>
    private void ReloadHighlighting()
    {
        if (_activeHighlighting is null || _xshdPath is null)
        {
            return;
        }

        try
        {
            _activeHighlighting = XshdHighlightingLoader.LoadFromFile(_xshdPath, CurrentSyntaxColors());
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException)
        {
            return;
        }

        foreach (var pane in AllPanes)
        {
            pane.ReplaceHighlightingForAll(_activeHighlighting);
        }
    }
}
