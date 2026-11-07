using ParadoxModTool.ScriptDocs;

namespace ParadoxModTool.ScriptCheck;

/// <summary>Die eingebauten Namen der Skript-Prüfung aus den importierten Script-Docs (Kalibrierung und App nutzen dieselbe Regel).</summary>
public static class ScriptCheckBuiltIns
{
    /// <summary>
    /// Alle Einträge der Arten Effect, Trigger und EventTarget, auch veraltete: Script-Docs können älter sein als das installierte Spiel
    /// (EU V: 313 Namen stehen nur noch als veraltet), und ein falscher Error wiegt schwerer als ein verpasster.
    /// </summary>
    public static HashSet<string> FromDatabase(ScriptDocDatabase database) =>
        database.Entries
            .Where(e => e.Kind is ScriptDocKind.Effect or ScriptDocKind.Trigger or ScriptDocKind.EventTarget)
            .Select(e => e.Name)
            .ToHashSet(StringComparer.Ordinal);
}
