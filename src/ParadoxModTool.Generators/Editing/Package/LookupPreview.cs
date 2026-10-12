using ParadoxModTool.Generators.Lookups;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>
/// Ergebnis der Vorschau einer Quelle: Gesamtzahl, die ersten Einträge und, wenn nichts kommt, ein Grund. <see cref="ReasonKind"/>: <c>gameFolder</c>,
/// <c>asset</c>, <c>file</c>, <c>empty</c> oder <c>load</c>.
/// </summary>
public sealed record LookupPreviewResult(int Count, IReadOnlyList<LookupEntry> Entries, string? Reason, string? ReasonKind);

/// <summary>"Vorschau" in der Maske "Nachschlagen" (Spec Teil 5b, Abschnitt 2): liest eine Quelle aus dem Editor-Stand wie "Aus Datenbank".</summary>
public static class LookupPreview
{
    public static LookupPreviewResult Run(PackageDocument document, PackageFiles files, IReadOnlyDictionary<string, string>? pending, string sourceId,
        IReadOnlyList<string> gameRoots, int max = 50, string? dataFolder = null)
    {
        GeneratorPackage package;
        using (var copy = PackageTempCopy.Create(document, files, pending))
        {
            try
            {
                package = copy.Load("en", dataFolder);
            }
            catch (Exception ex) when (ex is GeneratorPackageException or GeneratorTemplateException)
            {
                return new LookupPreviewResult(0, Array.Empty<LookupEntry>(), ex.Message, "load");
            }
        }

        var catalog = LookupCatalog.Load(package, new FieldLookupDef(new[] { sourceId }, new Dictionary<string, string> { ["name"] = "x" }), gameRoots);
        if (catalog.Notes.FirstOrDefault() is { } note)
        {
            return new LookupPreviewResult(0, Array.Empty<LookupEntry>(), note.Text, note.Kind);
        }

        if (catalog.Entries.Count > 0)
        {
            return new LookupPreviewResult(catalog.Entries.Count, catalog.Entries.Take(max).ToList(), null, null);
        }

        var def = package.Lookups!.First(s => s.Id == sourceId);
        return new LookupPreviewResult(0, Array.Empty<LookupEntry>(), EmptyReason(def, gameRoots), "empty");
    }

    private static string EmptyReason(LookupSourceDef def, IReadOnlyList<string> gameRoots)
    {
        if (def.Kind == LookupKind.Json)
        {
            return GeneratorsTexts.PreviewItemsEmpty(def.Items ?? string.Empty);
        }

        var folder = (def.Folder ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
        var anyText = gameRoots.Select(root => Path.Combine(root, folder)).Any(path => Directory.Exists(path) && Directory.EnumerateFiles(path, "*.txt").Any());
        return anyText ? GeneratorsTexts.PreviewNoBlocks : GeneratorsTexts.PreviewNoTxt(def.Folder ?? string.Empty);
    }
}
