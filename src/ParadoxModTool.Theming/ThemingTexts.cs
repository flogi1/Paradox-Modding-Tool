using ParadoxModTool.Localization;

namespace ParadoxModTool.Theming;

/// <summary>
/// Oberflächentexte der Theme-Bibliothek (Keys <c>theme.*</c> in <c>locales/*.json</c>). Der deutsche Text hier ist der Rückfall
/// und muss mit de.json übereinstimmen. Die Bezeichnungen der Farb-Tokens stehen unter <c>theme.token.&lt;key&gt;</c>, der Key wird
/// aus <see cref="ThemeToken.Key"/> gebildet (jedes Segment mit kleinem Anfangsbuchstaben, z. B. "Syntax.Comment" wird
/// "theme.token.syntax.comment"); Rückfall ist <see cref="ThemeToken.Label"/>.
/// </summary>
public static class ThemingTexts
{
    /// <summary>Name des eingebauten hellen Themes (derselbe Key wie in den Einstellungen).</summary>
    public static string LightName => T("settings.theme.light", "Hell");

    /// <summary>Name des eingebauten dunklen Themes (derselbe Key wie in den Einstellungen).</summary>
    public static string DarkName => T("settings.theme.dark", "Dunkel");

    public static string Unnamed => T("theme.unnamed", "Unbenanntes Theme");

    public static string BuiltInReadOnly => T("theme.builtInReadOnly", "Eingebaute Themes sind schreibgeschützt.");

    /// <summary>
    /// Anzeigename eines Themes. Eingebaute Themes werden über ihre Id übersetzt, weil ein beim Start erzeugtes Theme seinen
    /// Namen schon vor dem Laden der Sprache bekommen haben kann.
    /// </summary>
    public static string DisplayName(ThemeDefinition theme) => theme.Id switch
    {
        ThemeDefinition.LightId => LightName,
        ThemeDefinition.DarkId => DarkName,
        _ => theme.Name,
    };

    /// <summary>Überschrift einer Token-Gruppe im Theme-Editor.</summary>
    public static string GroupLabel(string group) => group switch
    {
        ThemeTokens.GroupUi => T("theme.group.ui", "Oberfläche"),
        ThemeTokens.GroupEditor => T("theme.group.editor", "Editor"),
        ThemeTokens.GroupSyntax => T("theme.group.syntax", "Syntax"),
        ThemeTokens.GroupDiff => T("theme.group.diff", "Vergleich (Diff)"),
        ThemeTokens.GroupCompatch => T("theme.group.compatch", "Kompatch-Merge"),
        _ => group,
    };

    /// <summary>Bezeichnung eines Farb-Tokens im Theme-Editor.</summary>
    public static string TokenLabel(ThemeToken token) => UiText.Get(TokenKey(token.Key), token.Label);

    /// <summary>Key der Token-Bezeichnung: "theme.token." und der Token-Schlüssel, jedes Segment mit kleinem Anfangsbuchstaben.</summary>
    public static string TokenKey(string tokenKey) =>
        "theme.token." + string.Join(".", tokenKey.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));

    private static string T(string key, string german) => UiText.Get(key, german);
}
