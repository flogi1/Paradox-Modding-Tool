using ParadoxModTool.Modifiers;

namespace ParadoxModTool.Editor;

/// <summary>
/// Datenquelle und Schalter für die Anzeige "Im Spiel: +10 %" beim Hover über eine Modifier-Zahl. Gilt wie <see cref="ScopeSettings"/> für alle
/// Arbeitsbereiche eines Fensters: das Hauptfenster setzt den Index und den Schalter, die Editoren lesen sie beim Hover.
/// </summary>
public static class ModifierValueSettings
{
    /// <summary>Modifier-Name → Anzeigeformat; leer, solange nichts geladen ist oder das Spiel keine Formate hat.</summary>
    public static ModifierFormatIndex Index { get; private set; } = ModifierFormatIndex.Empty;

    /// <summary>Name → Zahl für Script-Values und Konstanten, damit der Hover auch bei <c>modifier = mein_script_value</c> den Wert kennt.</summary>
    public static ScriptValueIndex ScriptValues { get; private set; } = ScriptValueIndex.Empty;

    /// <summary>Modifier-Name → übersetzter Anzeigename für den Tooltip; leer, solange nichts geladen ist.</summary>
    public static ModifierNameIndex Names { get; private set; } = ModifierNameIndex.Empty;

    /// <summary>Tooltip mit dem Wert im Spiel beim Hover über eine Modifier-Zahl. Standard: an.</summary>
    public static bool HoverEnabled { get; private set; } = true;

    /// <summary>Übersetzten Namen des Modifiers hinter dem Wert im Tooltip anzeigen. Standard: an.</summary>
    public static bool NamesEnabled { get; private set; } = true;

    public static event EventHandler? Changed;

    /// <summary>Wunsch, Formate, Script-Values und Namen neu einzulesen (Schaltfläche in Werkzeugleiste und Einstellungen); das Hauptfenster führt es aus.</summary>
    public static event EventHandler? ReloadRequested;

    public static void RequestReload() => ReloadRequested?.Invoke(null, EventArgs.Empty);

    public static void SetIndex(ModifierFormatIndex index, ScriptValueIndex? scriptValues = null, ModifierNameIndex? names = null)
    {
        Index = index;
        ScriptValues = scriptValues ?? ScriptValueIndex.Empty;
        Names = names ?? ModifierNameIndex.Empty;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Setzt die übersetzten Namen, die später fertig werden als die Formate; das Hauptfenster ruft es nach <see cref="SetIndex"/> auf.</summary>
    public static void SetNames(ModifierNameIndex names)
    {
        Names = names;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void SetNamesEnabled(bool enabled)
    {
        if (NamesEnabled != enabled)
        {
            NamesEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void SetHoverEnabled(bool enabled)
    {
        if (HoverEnabled != enabled)
        {
            HoverEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
