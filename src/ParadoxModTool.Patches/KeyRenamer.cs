using System.Text.RegularExpressions;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>Eine Umbenennung alter Schlüssel → neuer Schlüssel (Etappe 5, "Bestehende Mod-Gebäude übernehmen").</summary>
public sealed record KeyRename(string OldKey, string NewKey);

/// <summary>
/// Benennt Schlüssel in Mod-Dateien um (Spec 2026-09-30-patch-recipes, "Adopting existing buildings (stage 5)"): nur ganze Wörter,
/// dazu abgeleitete Formen wie <c>building_&lt;key&gt;</c>, <c>&lt;key&gt;_desc</c> oder <c>flogi_is_special_&lt;key&gt;</c>.
/// </summary>
public static class KeyRenamer
{
    private static readonly Regex Token = new("[A-Za-z0-9_]+", RegexOptions.CultureInvariant);

    /// <summary>
    /// Ersetzt in jedem Token (längste Folge aus <c>[A-Za-z0-9_]</c>) den alten Schlüssel, wenn das Token der Schlüssel ist, auf <c>_&lt;key&gt;</c> endet
    /// oder mit <c>&lt;key&gt;_</c> plus Buchstabe beginnt (auch beides). Ein abgeleitetes Token, das selbst in <paramref name="definedKeys"/> steht,
    /// ist ein eigenes Gebäude (<c>gold_mine_1</c> bei altem Schlüssel <c>mine_1</c>) und bleibt. Passen mehrere alte Schlüssel, gewinnt der längste.
    /// </summary>
    public static string RenameTokens(string text, IReadOnlyDictionary<string, string> map, IReadOnlySet<string> definedKeys, out int count)
    {
        var keys = map.Keys.Where(k => k.Length > 0).OrderByDescending(k => k.Length).ThenBy(k => k, StringComparer.Ordinal).ToList();
        var replaced = 0;
        var result = keys.Count == 0 ? text : Token.Replace(text, match =>
        {
            var token = match.Value;
            foreach (var key in keys)
            {
                if (token == key)
                {
                    replaced++;
                    return map[key];
                }

                if (Position(token, key) is { } at)
                {
                    if (definedKeys.Contains(token))
                    {
                        return token;
                    }

                    replaced++;
                    return string.Concat(token.AsSpan(0, at), map[key], token.AsSpan(at + key.Length));
                }
            }

            return token;
        });
        count = replaced;
        return result;
    }

    /// <summary>
    /// Plan der Umbenennung in allen Mod-Dateien (<c>.txt</c>, <c>.gui</c>, <c>.yml</c>; Ordner mit Punkt am Anfang nicht), wie ein Rezeptlauf geschrieben
    /// über <see cref="PatchPlanWriter"/>: Snapshot, ein Rückgängig-Schritt, Gedächtnis unverändert. Mit <paramref name="wasComments"/> steht vor jeder
    /// umbenannten Definition in <c>common/</c> eine Zeile <c># was: &lt;alter Schlüssel&gt;</c>.
    /// </summary>
    public static PatchPlan Plan(PatchContext context, PatchRecipe recipe, IReadOnlyList<KeyRename> renames, string folder, bool wasComments, CancellationToken ct = default)
    {
        var workspace = new PatchWorkspace(context.ModRoot, context.VanillaRoot, context.NewFileHasBom);
        var memory = PatchRecipeStore.LoadMemory(context.EffectiveRecipesFolder, recipe.Id);
        var proposals = KeyRenameProposals.Build(workspace, folder);
        var defined = proposals.DefinedKeys;
        var map = renames.ToDictionary(r => r.OldKey, r => r.NewKey, StringComparer.Ordinal);
        var oldByNew = renames.ToDictionary(r => r.NewKey, r => r.OldKey, StringComparer.Ordinal);
        foreach (var rel in workspace.ModFiles("**/*.txt;**/*.gui"))
        {
            ct.ThrowIfCancellationRequested();
            var original = workspace.Get(rel)!;
            var text = RenameOutsideWasComments(original, map, defined, out var count);
            if (count == 0)
            {
                continue;
            }

            if (wasComments && rel.StartsWith("common/", StringComparison.OrdinalIgnoreCase))
            {
                text = AddWasComments(original, text, oldByNew, workspace.NewLineOf(rel));
            }

            workspace.Set(rel, text);
            workspace.AddNote(rel, PatchTexts.RenameTokens(count));
        }

        ct.ThrowIfCancellationRequested();
        var loc = RenamedLocalization.Apply(workspace, map, defined, language => LocalizationIndex.LoadVanilla(context.VanillaRoot, language));
        var warnings = loc.Warnings.ToList();
        if (proposals.OrphanLocKeys > 0)
        {
            warnings.Add(PatchTexts.RenameOrphans(proposals.OrphanLocKeys));
        }

        var touched = workspace.Touched.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files = workspace.ReportedFiles.OrderBy(r => r, StringComparer.Ordinal).Select(rel =>
        {
            var original = workspace.Original(rel);
            var text = touched.Contains(rel) ? workspace.Get(rel) ?? string.Empty : original ?? string.Empty;
            return new PlannedFile(rel, PlannedFileStatuses.For(original, text, false), original, text, workspace.HasBomOf(rel), false,
                workspace.NotesOf(rel), Array.Empty<string>(), workspace.EncodingNameOf(rel));
        }).ToList();
        return new PatchPlan
        {
            RecipeId = recipe.Id,
            RecipeName = PatchTexts.RenamePlanName(recipe.Name),
            Files = files,
            Skipped = Array.Empty<SkippedRow>(),
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToList(),
            OldMemory = memory,
            NewMemory = memory,
            ConflictUnitIds = Array.Empty<string>(),
            BrokenMemoryText = PatchRecipeStore.BrokenMemoryText(context.EffectiveRecipesFolder, recipe.Id),
        };
    }

    /// <summary>Wie <see cref="RenameTokens"/>, aber Zeilen <c># was: &lt;alt&gt;</c> bleiben: sie sollen den alten Schlüssel behalten, auch bei einem zweiten Lauf.</summary>
    private static string RenameOutsideWasComments(string text, IReadOnlyDictionary<string, string> map, IReadOnlySet<string> definedKeys, out int count)
    {
        var total = 0;
        var parts = WasComment.Split(text);
        for (var i = 0; i < parts.Length; i += 2)
        {
            parts[i] = RenameTokens(parts[i], map, definedKeys, out var partCount);
            total += partCount;
        }

        count = total;
        return string.Concat(parts);
    }

    private static readonly Regex WasComment = new(@"(?m)(^[ \t]*# was: [^\r\n]*)", RegexOptions.CultureInvariant);

    /// <summary>
    /// Setzt in jede Block-Definition der obersten Ebene, die im Original unter dem alten Schlüssel stand, als erste Zeile <c># was: &lt;alt&gt;</c>.
    /// Im Block statt davor, damit der Kommentar mitwandert, wenn "Bestehende Blöcke übernehmen" den Block in den erzeugten Bereich holt.
    /// </summary>
    private static string AddWasComments(string original, string renamed, IReadOnlyDictionary<string, string> oldByNew, string newLine)
    {
        ScriptBlock before, after;
        try
        {
            before = ScriptDocument.Parse(original);
            after = ScriptDocument.Parse(renamed);
        }
        catch (ScriptParseException)
        {
            return renamed;
        }

        var oldTop = before.Entries.OfType<ScriptStatement>().Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        var inserts = after.Entries.OfType<ScriptStatement>()
            .Where(s => s.SourceStart is not null && s.Value is ScriptBlock && oldByNew.TryGetValue(s.Key, out var old) && oldTop.Contains(old))
            .Select(s => (Start: s.SourceStart!.Value, Old: oldByNew[s.Key]))
            .OrderByDescending(i => i.Start);
        foreach (var (start, old) in inserts)
        {
            var brace = renamed.IndexOf('{', start);
            if (brace < 0)
            {
                continue;
            }

            var lineStart = renamed.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
            var indent = renamed[lineStart..start];
            indent = indent[..(indent.Length - indent.TrimStart(' ', '\t').Length)];
            var lineEnd = renamed.IndexOfAny(new[] { '\r', '\n' }, brace + 1);
            var restOfLine = lineEnd < 0 ? renamed[(brace + 1)..] : renamed[(brace + 1)..lineEnd];

            // Steht nach "{" noch etwas auf der Zeile, kommt es auf eine neue Zeile (sonst würde der Kommentar es auskommentieren).
            var insert = newLine + indent + "\t# was: " + old + (restOfLine.Trim().Length > 0 ? newLine + indent : string.Empty);
            renamed = renamed.Insert(brace + 1, insert);
        }

        return renamed;
    }

    /// <summary>Stelle von <paramref name="key"/> als abgeleitete Form in <paramref name="token"/>, sonst null.</summary>
    private static int? Position(string token, string key)
    {
        for (var at = token.IndexOf(key, StringComparison.Ordinal); at >= 0; at = token.IndexOf(key, at + 1, StringComparison.Ordinal))
        {
            var end = at + key.Length;
            var startOk = at == 0 || token[at - 1] == '_';
            var endOk = end == token.Length || (token[end] == '_' && end + 1 < token.Length && char.IsAsciiLetter(token[end + 1]));
            if (startOk && endOk)
            {
                return at;
            }
        }

        return null;
    }
}
