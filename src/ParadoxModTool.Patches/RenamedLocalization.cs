using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Patches;

/// <summary>Zähler und Warnungen des Aufräumens nach einer Umbenennung (alle Dateien zusammen).</summary>
public sealed record LocCleanupResult(int SelfRemoved, int Inlined, int DuplicatesRemoved, int Moved, IReadOnlyList<string> Warnings);

/// <summary>
/// Lokalisierung nach einer Umbenennung (Spec 2026-09-30-patch-recipes, Etappe 5; Entscheidung 2026-10-01 "wie bei den Hauptgebäuden"):
/// Schlüssel und Verweise werden umbenannt; eine Zeile, deren Schlüssel sich ändert, verliert reine Selbstverweise, bekommt bei Selbstverweis mit
/// Zusatztext den Vanilla-Text, fällt als Doppel mit gleichem Wert weg und wandert nach <c>replace/</c>, wenn Vanilla den Schlüssel auch definiert.
/// </summary>
public static class RenamedLocalization
{
    public const string ReplaceFilePrefix = "pmt_renamed_l_";

    private sealed record Line(string Body, string Terminator);

    public static LocCleanupResult Apply(PatchWorkspace workspace, IReadOnlyDictionary<string, string> map, IReadOnlySet<string> definedKeys, Func<string, LocalizationIndex> vanilla)
    {
        var indexes = new Dictionary<string, LocalizationIndex>(StringComparer.Ordinal);
        LocalizationIndex? Vanilla(string? language)
        {
            if (language is null)
            {
                return null;
            }

            if (!indexes.TryGetValue(language, out var index))
            {
                index = vanilla(language);
                indexes[language] = index;
            }

            return index;
        }

        var files = workspace.ModFiles("localization/**/*.yml")
            .Select(rel => (Rel: rel, Text: workspace.Get(rel)!))
            .Select(f => (f.Rel, f.Text, Language: LocalizationIndex.LanguageOf(f.Text, f.Rel), Lines: SplitLines(f.Text)))
            .ToList();

        // Bestand je Sprache: Zeilen, deren Schlüssel gleich bleibt (Werte schon umbenannt)
        var existing = new Dictionary<string, Dictionary<string, (string Value, string File)>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var line in file.Lines)
            {
                if (LocalizationLines.Parse(line.Body) is { } old
                    && LocalizationLines.Parse(KeyRenamer.RenameTokens(line.Body, map, definedKeys, out _)) is { } renamed
                    && renamed.Key == old.Key)
                {
                    ListFor(existing, file.Language ?? string.Empty).TryAdd(renamed.Key, (renamed.Value, file.Rel));
                }
            }
        }

        int selfRemoved = 0, inlined = 0, duplicates = 0, moved = 0;
        var warnings = new List<string>();
        var movedLines = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            int fileSelf = 0, fileInlined = 0, fileDuplicates = 0, fileMoved = 0;
            var output = new StringBuilder(file.Text.Length);
            var inReplace = file.Rel.Contains("/replace/", StringComparison.OrdinalIgnoreCase);
            foreach (var line in file.Lines)
            {
                var body = KeyRenamer.RenameTokens(line.Body, map, definedKeys, out _);
                var old = LocalizationLines.Parse(line.Body);
                var renamed = LocalizationLines.Parse(body);
                if (old is null || renamed is null || renamed.Key == old.Key)
                {
                    output.Append(body).Append(line.Terminator);
                    continue;
                }

                var key = renamed.Key;
                var language = file.Language ?? "?";
                var vanillaText = Vanilla(file.Language)?.Value(key);
                var self = new Regex(@"\$" + Regex.Escape(key) + @"(\|[^$]*)?\$", RegexOptions.CultureInvariant);
                if (Regex.IsMatch(renamed.Value, @"^\$" + Regex.Escape(key) + @"(\|[^$]*)?\$\s*$", RegexOptions.CultureInvariant))
                {
                    if (vanillaText is null)
                    {
                        warnings.Add(PatchTexts.RenameNoVanillaText(key, language));
                        output.Append(body).Append(line.Terminator);
                    }
                    else
                    {
                        fileSelf++;
                    }

                    continue;
                }

                var value = renamed.Value;
                if (self.IsMatch(value))
                {
                    if (vanillaText is null)
                    {
                        warnings.Add(PatchTexts.RenameNoVanillaText(key, language));
                        output.Append(body).Append(line.Terminator);
                        continue;
                    }

                    value = self.Replace(value, _ => vanillaText);
                    body = string.Concat(body.AsSpan(0, renamed.ValueStart), value, body.AsSpan(renamed.ValueStart + renamed.ValueLength));
                    fileInlined++;
                }

                if (existing.TryGetValue(file.Language ?? string.Empty, out var known) && known.TryGetValue(key, out var other))
                {
                    if (other.Value == value)
                    {
                        fileDuplicates++;
                    }
                    else
                    {
                        warnings.Add(PatchTexts.RenameLocDiffers(key, language, other.File));
                        output.Append(body).Append(line.Terminator);
                    }

                    continue;
                }

                if (vanillaText is not null && !inReplace && file.Language is { } target)
                {
                    ListFor(movedLines, target).Add(body);
                    fileMoved++;
                    continue;
                }

                output.Append(body).Append(line.Terminator);
            }

            var text = output.ToString();
            if (text != file.Text)
            {
                workspace.Set(file.Rel, text);
            }

            Note(workspace, file.Rel, fileSelf, PatchTexts.RenameSelfRemoved);
            Note(workspace, file.Rel, fileInlined, PatchTexts.RenameInlined);
            Note(workspace, file.Rel, fileDuplicates, PatchTexts.RenameDuplicates);
            Note(workspace, file.Rel, fileMoved, PatchTexts.RenameMoved);
            selfRemoved += fileSelf;
            inlined += fileInlined;
            duplicates += fileDuplicates;
            moved += fileMoved;
        }

        foreach (var (language, lines) in movedLines)
        {
            var target = $"localization/{language}/replace/{ReplaceFilePrefix}{language}.yml";
            var newLine = workspace.NewLineOf(target);
            var current = workspace.Get(target);
            var text = current is null
                ? "l_" + language + ":" + newLine
                : current.Length == 0 || current.EndsWith('\n') ? current : current + newLine;
            workspace.Set(target, text + string.Join(string.Empty, lines.Select(l => l + newLine)));
        }

        return new LocCleanupResult(selfRemoved, inlined, duplicates, moved, warnings);
    }

    private static void Note(PatchWorkspace workspace, string rel, int count, Func<int, string> text)
    {
        if (count > 0)
        {
            workspace.AddNote(rel, text(count));
        }
    }

    private static List<Line> SplitLines(string text)
    {
        var lines = new List<Line>();
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                lines.Add(new Line(text[start..], string.Empty));
                break;
            }

            var bodyEnd = end > start && text[end - 1] == '\r' ? end - 1 : end;
            lines.Add(new Line(text[start..bodyEnd], text[bodyEnd..(end + 1)]));
            start = end + 1;
        }

        return lines;
    }

    private static TValue ListFor<TValue>(Dictionary<string, TValue> map, string key)
        where TValue : new()
    {
        if (!map.TryGetValue(key, out var value))
        {
            value = new TValue();
            map[key] = value;
        }

        return value;
    }
}
