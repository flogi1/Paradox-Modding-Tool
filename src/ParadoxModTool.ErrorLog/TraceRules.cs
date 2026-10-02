using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Bedingungs-Vorlage für eine Scope-Spur (Spec Scope-Verfolgung, Teil 2): Passt <see cref="Detail"/> auf die <c>Error:</c>-Zeile, wird die
/// Spur nur bei einem scheiternden Aufruf aktiv. <see cref="Limit"/> enthält <c>{name}</c> für die Gruppe <c>name</c> aus dem Muster.
/// </summary>
public sealed record TraceGuard(Regex Detail, string Limit);

/// <summary>
/// Spielabhängige Regeln der Scope-Verfolgung aus dem Fehlerkatalog: welche Blöcke Effekt- bzw. Trigger-Kontext öffnen (Einträge mit
/// <c>*</c> am Ende sind Präfixe), in welchen Ordnern die oberste Ebene Effekt-Kontext ist, die Glieder einer <c>if</c>-Kette und die
/// Quelle der <c>debug_log</c>-Zeilen in debug.log. <see cref="DebugScopeSource"/> ist die Quelle der Scope-Meldung von <c>debug_log_scopes</c>, wenn sie
/// eine andere ist (Crusader Kings III: <c>effectimpl.cpp</c>); <see langword="null"/> = dieselbe wie <see cref="DebugLogSource"/>.
/// </summary>
public sealed record TraceRules(
    IReadOnlyList<string> EffectBlocks,
    IReadOnlyList<string> TriggerBlocks,
    IReadOnlyList<string> EffectFolders,
    IReadOnlyList<string> IfChain,
    string DebugLogSource,
    string? DebugScopeSource = null)
{
    public static TraceRules Empty { get; } = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), string.Empty);

    public bool OpensEffect(string key) => Matches(EffectBlocks, key);

    public bool OpensTrigger(string key) => Matches(TriggerBlocks, key);

    /// <summary>Liegt die Datei (Pfad wie im Log, z.B. <c>common/scripted_effects/x.txt</c>) in einem Ordner mit Effekt-Kontext oben?</summary>
    public bool IsEffectFile(string gamePath)
    {
        var path = gamePath.Replace('\\', '/');
        return EffectFolders.Any(folder => path.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool Matches(IReadOnlyList<string> entries, string key) =>
        entries.Any(entry => entry.EndsWith('*')
            ? key.StartsWith(entry[..^1], StringComparison.Ordinal)
            : string.Equals(entry, key, StringComparison.Ordinal));
}
