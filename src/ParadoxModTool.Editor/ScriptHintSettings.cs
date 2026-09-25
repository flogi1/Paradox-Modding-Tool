using ParadoxModTool.ScriptDocs;

namespace ParadoxModTool.Editor;

/// <summary>
/// Einstellungen und Datenquelle für Hover-Tipps und Autovervollständigung im Editor. Gilt für alle Arbeitsbereiche eines Fensters
/// (wie <see cref="TabIconSettings"/>): das Hauptfenster setzt den <see cref="Provider"/> und die Schalter, die Editoren lesen sie bei Bedarf.
/// </summary>
public static class ScriptHintSettings
{
    /// <summary>Quelle der Hinweise (Script-Datenbank des Spiels); <see langword="null"/>, solange keine geladen ist - dann tut der Editor nichts.</summary>
    public static IScriptHintProvider? Provider { get; set; }

    /// <summary>Tooltip mit Beschreibung und Scopes, wenn der Mauszeiger auf einem bekannten Namen ruht. Standard: an.</summary>
    public static bool HoverEnabled { get; private set; } = true;

    /// <summary>Vorschlagsliste beim Tippen (ab 3 Zeichen) und mit Strg+Leertaste. Standard: an.</summary>
    public static bool AutoCompleteEnabled { get; private set; } = true;

    public static event EventHandler? Changed;

    public static void SetHoverEnabled(bool enabled)
    {
        if (HoverEnabled != enabled)
        {
            HoverEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void SetAutoCompleteEnabled(bool enabled)
    {
        if (AutoCompleteEnabled != enabled)
        {
            AutoCompleteEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
