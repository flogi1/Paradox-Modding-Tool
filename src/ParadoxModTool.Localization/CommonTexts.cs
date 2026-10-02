namespace ParadoxModTool.Localization;

/// <summary>
/// Texte, die in vielen Fenstern gleich vorkommen (Knöpfe, Titel von Meldungen). Die Keys stehen in <c>locales/de.json</c> und
/// <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class CommonTexts
{
    public static string Ok => T("common.ok", "OK");

    public static string Cancel => T("common.cancel", "Abbrechen");

    public static string Close => T("common.close", "Schließen");

    public static string Save => T("common.save", "Speichern");

    public static string Delete => T("common.delete", "Löschen");

    public static string Remove => T("common.remove", "Entfernen");

    public static string Browse => T("common.browse", "Durchsuchen");

    public static string Yes => T("common.yes", "Ja");

    public static string No => T("common.no", "Nein");

    public static string Error => T("common.error", "Fehler");

    public static string Notice => T("common.notice", "Hinweis");

    public static string Warning => T("common.warning", "Warnung");

    public static string Cancelled => T("common.cancelled", "Abgebrochen.");

    public static string Done => T("common.done", "Fertig");

    private static string T(string key, string german) => UiText.Get(key, german);
}
