using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Eine Scope-Spur in einer Mod-Datei: Zeile des Einschubs (= Zeile des Ziel-Befehls), Zeilenbereich des Ziel-Befehls, mit Bedingung oder
/// ohne. <see cref="Broken"/>: nur Einschub oder nur Marker gefunden (von Hand geändert); die Spur bleibt sichtbar und entfernbar.
/// </summary>
public sealed record ActiveTrace(ModSource Mod, string Path, string Id, int Line, int FirstLine, int LastLine, bool Guarded, bool Broken);

/// <summary>Geplanter Einschub: Plan, die Änderung (nur bei <see cref="TraceStatus.Ok"/>) und der Dateitext, aus dem geplant wurde.</summary>
public sealed record TraceInsert(TracePlan Plan, ModRenamePlan? Edit, string OriginalText);

/// <summary>
/// Findet, setzt und entfernt Scope-Spuren in eigenen Mods (Spec Scope-Verfolgung, Teil 2). Quelle der Wahrheit sind die Einschübe und
/// Marker in den Dateien; PMT speichert nichts daneben.
/// </summary>
public static class TraceMarkers
{
    private static readonly Regex Prefix = new(
        @"(?<guard>if = \{ limit = \{ .*? \} )?debug_log = ""PMT_TRACE_(?<id>[0-9a-f]{4})"" debug_log_scopes = yes (?(guard)\} )",
        RegexOptions.CultureInvariant);

    private static readonly Regex Suffix = new(@" # PMT-TRACE (?<id>[0-9a-f]{4})$", RegexOptions.CultureInvariant);

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly string[] PhaseFolders = { "in_game", "main_menu", "loading_screen" };

    public static IReadOnlyList<ActiveTrace> Scan(ModSource mod, TraceRules rules)
    {
        var result = new List<ActiveTrace>();
        if (!Directory.Exists(mod.Directory))
        {
            return result;
        }

        foreach (var path in Directory.EnumerateFiles(mod.Directory, "*.txt", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var relative = System.IO.Path.GetRelativePath(mod.Directory, path);
            if (relative.Split('\\', '/').Any(part => part.StartsWith('.')) || Read(path) is not { } file || !file.Text.Contains("PMT", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = file.Text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var prefix = Prefix.Match(line);
                var suffix = Suffix.Match(line);
                if (!prefix.Success && !suffix.Success)
                {
                    continue;
                }

                var id = prefix.Success ? prefix.Groups["id"].Value : suffix.Groups["id"].Value;
                var broken = !prefix.Success || !suffix.Success || suffix.Groups["id"].Value != id;
                var cleaned = RemoveFromText(file.Text, i, id);
                var plan = TracePlanner.Plan(cleaned, GamePath(relative), i + 1, null, Array.Empty<TraceGuard>(), rules, id);
                var (first, last) = plan.Status == TraceStatus.Ok && plan.Line == i + 1 ? (plan.FirstLine, plan.LastLine) : (i + 1, i + 1);
                result.Add(new ActiveTrace(mod, path, id, i + 1, first, last, prefix.Success && prefix.Groups["guard"].Success, broken));
            }
        }

        return result;
    }

    public static TraceInsert PlanInsert(
        ModSource mod, string path, string gamePath, int line, string? detail, IReadOnlyList<TraceGuard> guards, TraceRules rules, string id)
    {
        if (Read(path) is not { } file)
        {
            return new TraceInsert(new TracePlan(TraceStatus.LineNotFound, 0, 0, 0, 0, false, string.Empty, string.Empty, id), null, string.Empty);
        }

        var plan = TracePlanner.Plan(file.Text, gamePath, line, detail, guards, rules, id);
        if (plan.Status != TraceStatus.Ok)
        {
            return new TraceInsert(plan, null, file.Text);
        }

        var newText = TracePlanner.Apply(file.Text, plan);
        var preview = new RenameLine(plan.Line, LineText(file.Text, plan.Line), LineText(newText, plan.Line));
        var edit = new ModRenamePlan(mod.Name, mod.Directory, new[] { new RenameFile(path, newText, 1, new[] { preview }, file.HasBom) });
        return new TraceInsert(plan, edit, file.Text);
    }

    /// <summary>Hat die Datei noch genau den Text, aus dem geplant wurde? Sonst würde das Schreiben Änderungen des Nutzers überschreiben.</summary>
    public static bool IsCurrent(TraceInsert insert) =>
        insert.Edit is { Files: [var file, ..] } && Read(file.Path) is { } current && current.Text == insert.OriginalText;

    /// <summary>Entfernen: je Mod ein Plan, je Datei die Zeilen ohne Einschub und Marker (genau das, was eingefügt wurde).</summary>
    public static IReadOnlyList<ModRenamePlan> PlanRemove(IEnumerable<ActiveTrace> traces)
    {
        var plans = new List<ModRenamePlan>();
        foreach (var byMod in traces.GroupBy(t => t.Mod))
        {
            var files = new List<RenameFile>();
            foreach (var byFile in byMod.GroupBy(t => t.Path, StringComparer.OrdinalIgnoreCase))
            {
                if (Read(byFile.Key) is not { } file)
                {
                    continue;
                }

                var text = file.Text;
                var changed = new List<RenameLine>();
                foreach (var trace in byFile.OrderBy(t => t.Line))
                {
                    var before = LineText(text, trace.Line);
                    text = RemoveFromText(text, trace.Line - 1, trace.Id);
                    changed.Add(new RenameLine(trace.Line, before, LineText(text, trace.Line)));
                }

                files.Add(new RenameFile(byFile.Key, text, changed.Count, changed, file.HasBom));
            }

            plans.Add(new ModRenamePlan(byMod.Key.Name, byMod.Key.Directory, files));
        }

        return plans;
    }

    /// <summary>Neue Spur-Id (4 Kleinbuchstaben-Hex), die weder eine Spur der Mod noch ein Treffer im aktuellen debug.log trägt.</summary>
    public static string NewId(IEnumerable<string> usedIds, Random random)
    {
        var used = usedIds.ToHashSet(StringComparer.Ordinal);
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var id = random.Next(0x10000).ToString("x4");
            if (!used.Contains(id))
            {
                return id;
            }
        }

        return Enumerable.Range(0, 0x10000).Select(i => i.ToString("x4")).First(id => !used.Contains(id));
    }

    /// <summary>Pfad wie im Log: ohne Phasenordner (<c>in_game</c>, ...), mit "/".</summary>
    private static string GamePath(string relative)
    {
        var parts = relative.Replace('\\', '/').Split('/');
        return parts.Length > 1 && PhaseFolders.Contains(parts[0], StringComparer.OrdinalIgnoreCase) ? string.Join('/', parts[1..]) : string.Join('/', parts);
    }

    private static string RemoveFromText(string text, int lineIndex, string id)
    {
        var lines = text.Split('\n');
        var line = lines[lineIndex];
        var cr = line.EndsWith('\r') ? "\r" : string.Empty;
        var content = line.TrimEnd('\r');
        var suffix = " # PMT-TRACE " + id;
        if (content.EndsWith(suffix, StringComparison.Ordinal))
        {
            content = content[..^suffix.Length];
        }

        var prefix = Prefix.Matches(content).FirstOrDefault(m => m.Groups["id"].Value == id);
        if (prefix is not null)
        {
            content = content.Remove(prefix.Index, prefix.Length);
        }

        lines[lineIndex] = content + cr;
        return string.Join('\n', lines);
    }

    private static string LineText(string text, int line) => text.Split('\n')[line - 1].TrimEnd('\r');

    private static ScannedFile? Read(string path)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        try
        {
            return new ScannedFile(path, StrictUtf8.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0)), hasBom);
        }
        catch (DecoderFallbackException)
        {
            // Kein gültiges UTF-8: Umschreiben würde die Datei beschädigen (wie bei NameEdits).
            return null;
        }
    }
}
