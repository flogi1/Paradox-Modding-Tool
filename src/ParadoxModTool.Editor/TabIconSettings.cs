using System.Collections.Generic;
using System.Linq;
using ParadoxModTool.Categories;

namespace ParadoxModTool.Editor;

/// <summary>Die Symbole, die ein Tab in der Dokumentenleiste zeigen kann.</summary>
public enum TabIconKind
{
    Note,
    Bookmark,
    Bug,
    Vanilla,
    Mod,
    Lock,
}

/// <summary>
/// Liefert zu einem Dateipfad den Tooltip der Sperre (Datei, Ordner oder ganze Mod gesperrt) oder <see langword="null"/>, wenn nichts sperrt.
/// Das Hauptfenster setzt den <see cref="Provider"/>; der Editor kennt keine Mods.
/// </summary>
public static class TabLockProvider
{
    public static Func<string, string?>? Provider { get; set; }
}

/// <summary>
/// Welche Tab-Symbole sichtbar sind (gilt für alle Arbeitsbereiche). Standard: alle sichtbar. Die Einstellung
/// schaltet nur die Anzeige der Symbole; Filter wie "Nur mit Notiz" arbeiten unabhängig davon weiter.
/// </summary>
public static class TabIconSettings
{
    private static readonly HashSet<TabIconKind> Hidden = new();

    /// <summary>Wird ausgelöst, wenn sich die Sichtbarkeit eines Symbols ändert.</summary>
    public static event EventHandler? Changed;

    public static bool IsVisible(TabIconKind kind) => !Hidden.Contains(kind);

    public static void SetVisible(TabIconKind kind, bool visible)
    {
        var changed = visible ? Hidden.Remove(kind) : Hidden.Add(kind);
        if (changed)
        {
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Namen der ausgeblendeten Symbole, für die Session.</summary>
    public static List<string> HiddenNames() => Hidden.Select(k => k.ToString()).OrderBy(n => n).ToList();

    /// <summary>Stellt die ausgeblendeten Symbole aus der Session wieder her; unbekannte Namen werden ignoriert.</summary>
    public static void LoadHidden(IEnumerable<string>? names)
    {
        Hidden.Clear();
        foreach (var name in names ?? Enumerable.Empty<string>())
        {
            if (Enum.TryParse<TabIconKind>(name, out var kind) && Enum.IsDefined(kind))
            {
                Hidden.Add(kind);
            }
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }
}

/// <summary>Hält die Wurzelordner für die Herkunftserkennung (Vanilla, Mod); MainWindow setzt sie.</summary>
public static class FileOriginTracker
{
    private static IReadOnlyList<string> _vanillaRoots = new List<string>();
    private static IReadOnlyList<string> _modRoots = new List<string>();

    public static void SetRoots(IEnumerable<string> vanillaRoots, IEnumerable<string> modRoots)
    {
        _vanillaRoots = vanillaRoots.ToList();
        _modRoots = modRoots.ToList();
    }

    public static FileOrigin Classify(string? filePath) => FileOriginClassifier.Classify(filePath, _vanillaRoots, _modRoots);
}
