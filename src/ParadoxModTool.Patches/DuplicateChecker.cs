using System.Text;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>Sucht, ob ein erzeugter Schlüssel in der Mod schon woanders definiert ist (gleicher Ordner wie das Ziel, nur oberste Ebene, ohne den eigenen Bereich).</summary>
public static class DuplicateChecker
{
    public static (string File, int Line)? FindDefinition(PatchWorkspace workspace, string targetRel, string key, string recipeId, string ruleId) =>
        BuildIndex(workspace, targetRel, recipeId, ruleId).TryGetValue(key, out var hit) ? hit : null;

    /// <summary>Alle Schlüssel der obersten Ebene im Zielordner mit erster Fundstelle; einmal je Regellauf aufbauen.</summary>
    public static IReadOnlyDictionary<string, (string File, int Line)> BuildIndex(PatchWorkspace workspace, string targetRel, string recipeId, string ruleId) =>
        BuildAllDefinitions(workspace, targetRel, recipeId, ruleId).ToDictionary(p => p.Key, p => p.Value[0], StringComparer.Ordinal);

    /// <summary>Wie <see cref="BuildIndex"/>, aber mit allen Fundstellen je Schlüssel (für "Bestehende Blöcke übernehmen": doppelte Definitionen erkennen).</summary>
    public static IReadOnlyDictionary<string, List<(string File, int Line)>> BuildAllDefinitions(PatchWorkspace workspace, string targetRel, string recipeId, string ruleId)
    {
        var index = new Dictionary<string, List<(string File, int Line)>>(StringComparer.Ordinal);
        var slash = targetRel.LastIndexOf('/');
        var folder = slash < 0 ? string.Empty : targetRel[..(slash + 1)];
        foreach (var rel in workspace.ModFiles(folder + "*.txt"))
        {
            var text = MaskOwnRegion(workspace.Get(rel)!, recipeId, ruleId);
            ScriptBlock root;
            try
            {
                root = ScriptDocument.Parse(text);
            }
            catch (ScriptParseException)
            {
                continue;
            }

            var line = 1;
            var position = 0;
            foreach (var statement in root.Entries.OfType<ScriptStatement>())
            {
                if (statement.SourceStart is not { } start)
                {
                    continue;
                }

                line += text.AsSpan(position, start - position).Count('\n');
                position = start;
                if (!index.TryGetValue(statement.Key, out var places))
                {
                    places = new List<(string, int)>();
                    index[statement.Key] = places;
                }

                places.Add((rel, line));
            }
        }

        return index;
    }

    /// <summary>Ersetzt den eigenen Bereich durch Leerraum (Zeilenumbrüche bleiben, damit Zeilennummern stimmen).</summary>
    private static string MaskOwnRegion(string text, string recipeId, string ruleId)
    {
        var begin = text.IndexOf(GeneratedRegion.BeginMarker(recipeId, ruleId), StringComparison.Ordinal);
        var endMarker = GeneratedRegion.EndMarker(recipeId, ruleId);
        var end = begin < 0 ? -1 : text.IndexOf(endMarker, begin, StringComparison.Ordinal);
        if (begin < 0 || end < 0)
        {
            return text;
        }

        end += endMarker.Length;
        var masked = new StringBuilder(text.Length);
        masked.Append(text, 0, begin);
        for (var i = begin; i < end; i++)
        {
            masked.Append(text[i] is '\n' or '\r' ? text[i] : ' ');
        }

        return masked.Append(text, end, text.Length - end).ToString();
    }
}
