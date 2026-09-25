using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Ermittelt die Standardfarbe eines Ordnernamens anhand der <c>FolderCategories</c> des aktiven
/// Spielprofils (z. B. "common" → #4A90D9 aus profiles/eu5.json) — löst den bisher unter
/// "Noch offene Punkte" der Architektur-Dokumentation vermerkten Punkt "Eine zusätzliche
/// Einfärbung nach Datei-Herkunft ... braucht die noch fehlende Spielprofil-/Mod-Ordner-
/// Konfiguration als Voraussetzung", die diese Runde mit der Verzeichnis-Übersicht liefert.
/// Bewusst als reine, von WPF unabhängige Funktion (statt Teil von <c>ExplorerPanel</c>), damit
/// die Zuordnungslogik ohne UI testbar ist. Eine vom Nutzer explizit über
/// <see cref="ExplorerColorStore"/> gesetzte Farbe hat in <c>ExplorerPanel</c> immer Vorrang vor
/// diesem Vorschlag.
/// </summary>
/// <remarks>
/// Bekannte Vereinfachung: Der Ordnername wird GLOBAL gegen <c>FolderCategory.Path</c>
/// verglichen (z. B. "common"), unabhängig davon, wie tief er im Baum liegt oder ob er
/// tatsächlich direkt unterhalb einer Mod-/Spielwurzel liegt. Für die bekannten
/// EU5-Kategorienamen (common, gui, gfx, events, localization, map, history, in_game,
/// main_menu, loading_screen) ist eine zufällige Namensgleichheit an anderer Stelle im
/// Dateibaum unwahrscheinlich; eine exakte, pfadbasierte Prüfung wäre deutlich aufwendiger und
/// wurde für diesen ersten Ausbauschritt bewusst zurückgestellt.
/// </remarks>
public static class FolderCategoryColorResolver
{
    public static string? DefaultColorFor(string entryName, IReadOnlyList<FolderCategory>? categories)
    {
        if (categories is null)
        {
            return null;
        }

        foreach (var category in categories)
        {
            if (string.Equals(category.Path, entryName, StringComparison.OrdinalIgnoreCase))
            {
                return category.DefaultColor;
            }
        }

        return null;
    }
}
