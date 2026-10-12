using ParadoxModTool.Scopes;

namespace ParadoxModTool.Editor;

/// <summary>Eine Warnung im Text: Stelle, Meldung und ob sie schwer wiegt (rot) oder nur ein Hinweis ist (orange).</summary>
public sealed record EditorWarning(int Start, int Length, string Message, bool Severe);

/// <summary>
/// Datenquelle und Schalter für die Scope-Anzeigen im Editor (Statusleisten, Hover-Tooltip, Warnungen, Panel). Gilt wie <see cref="ScriptHintSettings"/> für alle
/// Arbeitsbereiche eines Fensters: das Hauptfenster setzt die Scope-Verfolgung und die Schalter, die Editoren lesen sie bei Bedarf.
/// </summary>
public static class ScopeSettings
{
    /// <summary>Die Scope-Verfolgung des Spiels; <see langword="null"/>, solange keine Script-Datenbank geladen ist.</summary>
    public static ScopeEngine? Engine { get; private set; }

    /// <summary>Kurzer Hinweis zum Stand der gelernten Regeln (z. B. "301 sichere Regeln, gelernt am …"), für den Tooltip der Statusleiste.</summary>
    public static string Info { get; private set; } = string.Empty;

    /// <summary>Zeile unter dem Editor mit dem Scope am Cursor. Standard: an.</summary>
    public static bool StatusBarEnabled { get; private set; } = true;

    /// <summary>Dieselbe Zeile zusätzlich direkt unter der Pfadleiste über dem Editor. Standard: aus.</summary>
    public static bool StatusBarTopEnabled { get; private set; }

    /// <summary>Rote Wellenlinie unter Triggern und Effects, die im (sicheren) Scope nicht gelten. Standard: an.</summary>
    public static bool WarningsEnabled { get; private set; } = true;

    /// <summary>Abschnitt "Scope" im Notiz-Panel: Scope am Cursor, gespeicherte Scopes und Warnungen der Datei. Standard: an.</summary>
    public static bool PanelEnabled { get; private set; } = true;

    /// <summary>Zusätzliche Zeile im Hover-Tooltip: ob der Name im aktuellen Scope erlaubt ist. Standard: an.</summary>
    public static bool HoverEnabled { get; private set; } = true;

    /// <summary>
    /// Weitere Warnungen derselben Art (Präfixe, die im Ordner nicht wirken, Victoria 3): Zum Dateipfad liefert das Hauptfenster auf dem UI-Thread eine
    /// Prüfung, die dann im Hintergrund den Text prüft; <see langword="null"/>, wenn es für die Datei nichts zu prüfen gibt.
    /// </summary>
    public static Func<string?, Func<string, IReadOnlyList<EditorWarning>>?>? PrefixChecker { get; private set; }

    public static event EventHandler? Changed;

    public static void SetPrefixChecker(Func<string?, Func<string, IReadOnlyList<EditorWarning>>?>? checker)
    {
        PrefixChecker = checker;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void SetEngine(ScopeEngine? engine, string info)
    {
        Engine = engine;
        Info = info;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void SetStatusBarEnabled(bool enabled)
    {
        if (StatusBarEnabled != enabled)
        {
            StatusBarEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void SetStatusBarTopEnabled(bool enabled)
    {
        if (StatusBarTopEnabled != enabled)
        {
            StatusBarTopEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void SetWarningsEnabled(bool enabled)
    {
        if (WarningsEnabled != enabled)
        {
            WarningsEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void SetPanelEnabled(bool enabled)
    {
        if (PanelEnabled != enabled)
        {
            PanelEnabled = enabled;
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
