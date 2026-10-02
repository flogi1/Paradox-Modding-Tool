namespace ParadoxModTool.Theming;

/// <summary>Ein benannter, im Theme-Editor einstellbarer Farbwert samt Standardwerten der beiden eingebauten Themes.</summary>
/// <param name="Key">Eindeutiger Schlüssel; in der Oberfläche als Ressource <c>Theme.&lt;Key&gt;</c> verfügbar.</param>
/// <param name="Group">Gruppe für die Darstellung im Theme-Editor.</param>
/// <param name="Label">Deutsche Bezeichnung im Theme-Editor.</param>
/// <param name="Light">Wert im eingebauten Theme "Hell" (#RRGGBB oder #AARRGGBB).</param>
/// <param name="Dark">Wert im eingebauten Theme "Dunkel".</param>
public sealed record ThemeToken(string Key, string Group, string Label, string Light, string Dark);

/// <summary>Katalog aller Farb-Tokens. Reihenfolge = Reihenfolge im Theme-Editor.</summary>
public static class ThemeTokens
{
    public const string GroupUi = "Oberfläche";
    public const string GroupEditor = "Editor";
    public const string GroupSyntax = "Syntax";
    public const string GroupDiff = "Vergleich (Diff)";
    public const string GroupCompatch = "Kompatch-Merge";

    /// <summary>Präfix der Syntax-Tokens; der Rest des Schlüssels ist der Farbname in der xshd-Datei (z. B. "Comment").</summary>
    public const string SyntaxPrefix = "Syntax.";

    public static IReadOnlyList<ThemeToken> All { get; } =
    [
        new("WindowBackground", GroupUi, "Fensterhintergrund", "#F0F0F0", "#1E1F22"),
        new("PanelBackground", GroupUi, "Flächen (Listen, Eingabefelder)", "#FFFFFF", "#2B2D30"),
        new("ControlBackground", GroupUi, "Schaltflächen", "#E1E1E1", "#3C3F41"),
        new("ControlHover", GroupUi, "Schaltfläche, Maus darüber", "#E5F1FB", "#4A4E52"),
        new("ControlPressed", GroupUi, "Schaltfläche, gedrückt", "#CCE4F7", "#5A5F63"),
        new("ControlBorder", GroupUi, "Rahmen", "#ADADAD", "#5C6064"),
        new("Text", GroupUi, "Text", "#000000", "#DFE1E5"),
        new("TextMuted", GroupUi, "Text, gedämpft", "#808080", "#9AA0A6"),
        new("TextDisabled", GroupUi, "Text, deaktiviert", "#8A8A8A", "#6F747A"),
        new("Accent", GroupUi, "Akzent", "#0078D7", "#4B9CE2"),
        new("AccentText", GroupUi, "Text auf Akzent", "#FFFFFF", "#FFFFFF"),
        new("Selection", GroupUi, "Auswahl, aktiv", "#CCE8FF", "#2F4F73"),
        new("SelectionText", GroupUi, "Text in Auswahl", "#000000", "#FFFFFF"),
        new("InactiveSelection", GroupUi, "Auswahl, inaktiv", "#E0E0E0", "#3A3D41"),
        new("Separator", GroupUi, "Trennlinien", "#A0A0A0", "#4A4D50"),
        new("NoticeBackground", GroupUi, "Hinweisleiste, Hintergrund", "#FFF4CC", "#4A4127"),
        new("NoticeBorder", GroupUi, "Hinweisleiste, Rand", "#DEB254", "#8A7434"),
        new("NoticeText", GroupUi, "Hinweisleiste, Text", "#000000", "#F2E4B0"),
        new("Warning", GroupUi, "Punkt für ungespeicherte Änderungen", "#FF4500", "#FF7A59"),
        new("Danger", GroupUi, "Fehler- und Warntext", "#B00020", "#FF6B6B"),
        new("Bookmark", GroupUi, "Lesezeichen", "#5B8DEF", "#7AA2F7"),

        new("EditorBackground", GroupEditor, "Hintergrund", "#FFFFFF", "#1E1F22"),
        new("EditorForeground", GroupEditor, "Text", "#000000", "#D4D4D4"),
        new("LineNumbers", GroupEditor, "Zeilennummern", "#6E7781", "#858585"),
        new("EditorSelection", GroupEditor, "Textauswahl", "#ADD6FF", "#264F78"),
        new("EditorBracketMatch", GroupEditor, "Zusammengehörende Klammern", "#FFD98A", "#6B5B2A"),

        new("Syntax.Comment", GroupSyntax, "Kommentar", "#008000", "#57A64A"),
        new("Syntax.String", GroupSyntax, "Zeichenkette", "#A31515", "#D69D85"),
        new("Syntax.Date", GroupSyntax, "Datum", "#0451A5", "#569CD6"),
        new("Syntax.Number", GroupSyntax, "Zahl", "#098658", "#B5CEA8"),
        new("Syntax.Operator", GroupSyntax, "Operator und Klammern", "#333333", "#D4D4D4"),
        new("Syntax.ScopeKeyword", GroupSyntax, "Scope-Wörter (ROOT, THIS, ...)", "#267F99", "#4EC9B0"),
        new("Syntax.BooleanKeyword", GroupSyntax, "yes / no", "#0000FF", "#569CD6"),
        new("Syntax.LogicKeyword", GroupSyntax, "AND / OR / NOT", "#AF00DB", "#C586C0"),

        new("DiffAdded", GroupDiff, "Hinzugefügt", "#4CD98E", "#3FBF7F"),
        new("DiffRemoved", GroupDiff, "Entfernt", "#D94C4C", "#E05C5C"),
        new("DiffModified", GroupDiff, "Geändert", "#D9B24C", "#D9B24C"),
        new("DiffVanilla", GroupDiff, "Vanilla-Seite (Rahmen, Pfad)", "#3B82F6", "#5B9BFF"),
        new("DiffMod", GroupDiff, "Mod-Seite (Rahmen, Pfad)", "#F59E0B", "#FBB040"),
        new("DiffViewport", GroupDiff, "Sichtbereich in der Übersichtsleiste", "#463C78C8", "#556FA8E0"),
        new("OverviewTrack", GroupDiff, "Spur der Übersichtsleiste", "#28808080", "#40808080"),
        new("DiffMark", GroupDiff, "Markierte Zeilen (Compare und Update Tool)", "#80A9CBF2", "#8034588F"),

        new("CompatchA", GroupCompatch, "Herkunft Mod A", "#1FA3A3", "#3CC8C8"),
        new("CompatchB", GroupCompatch, "Herkunft Mod B", "#E07B1A", "#FFA24D"),
        new("CompatchReview", GroupCompatch, "Prüfen", "#D4B02A", "#D9B84A"),
        new("CompatchConflict", GroupCompatch, "Konflikt", "#D94C4C", "#E05C5C"),
        new("CompatchVanilla", GroupCompatch, "Vanilla und Lücken", "#9E9E9E", "#6E6E6E"),
        new("CompatchSeen", GroupCompatch, "Gesehen (Auge in der Liste)", "#29A8E0", "#7DD3FC"),
    ];

    public static IReadOnlyList<string> Groups { get; } = [GroupUi, GroupEditor, GroupSyntax, GroupDiff, GroupCompatch];

    public static bool IsKnown(string key) => All.Any(t => t.Key == key);
}

/// <summary>Gültigkeitsprüfung für Farbwerte in Theme-Dateien.</summary>
public static class HexColor
{
    /// <summary>#RRGGBB oder #AARRGGBB.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value[0] != '#' || (value.Length != 7 && value.Length != 9))
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }
}
