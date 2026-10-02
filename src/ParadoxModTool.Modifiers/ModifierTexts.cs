using ParadoxModTool.Localization;

namespace ParadoxModTool.Modifiers;

/// <summary>Texte der Modifier-Wert-Anzeige im Editor (Schlüssel in locales/de.json und en.json; der Rückfalltext gleicht de.json).</summary>
public static class ModifierTexts
{
    /// <summary>"Im Spiel: +10 %", bei geschätztem Format mit "(geschätzt)" dahinter.</summary>
    public static string InGame(string value, bool estimated)
    {
        var text = F("editor.modifierValue", "Im Spiel: {0}", value);
        return estimated ? text + " " + T("editor.modifierValue.estimated", "(geschätzt)") : text;
    }

    /// <summary>"Modifier-Werte neu geladen: 1980 Formate, 8804 Script-Values, 1425 Namen" (Statuszeile nach dem Neuladen).</summary>
    public static string Reloaded(int formats, int scriptValues, int names) => F("editor.modifierReloaded", "Modifier-Werte neu geladen: {0} Formate, {1} Script-Values, {2} Namen", formats, scriptValues, names);

    private static string T(string key, string fallback) => UiText.Get(key, fallback);

    private static string F(string key, string fallback, params object?[] args) => UiText.Format(key, fallback, args);
}
