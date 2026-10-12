using System.Text.RegularExpressions;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Generators.Lookups;

/// <summary>
/// Einträge einer <see cref="LookupKind.GameKeys"/>-Quelle (Spec 2026-10-05-generator-packages-lookups, Abschnitt 1): Blöcke bis zur Tiefe der Quelle aus allen
/// <c>*.txt</c> ihres Ordners, Name und Beschreibung aus der englischen Lokalisierung, Tag aus Dateiname oder Block.
/// </summary>
public static class GameKeySource
{
    private static readonly Regex TagValue = new("^[A-Za-z]{2,5}$", RegexOptions.CultureInvariant);

    /// <summary>Einträge in Datei- und Blockreihenfolge; bei gleichem Schlüssel gewinnt der spätere Spielordner. Eine unlesbare Datei wirft <see cref="GeneratorOutputException"/>.</summary>
    public static IReadOnlyList<LookupEntry> Read(LookupSourceDef def, IReadOnlyList<string> gameRoots, GameLocalization english)
    {
        // Ein importiertes Paket darf keine Dateien außerhalb des Spielordners lesen.
        if (def.Folder is { Length: > 0 } relative && !SafeRelativePath.IsSafe(relative))
        {
            throw new GeneratorOutputException(GeneratorsTexts.LookupUnsafeFolder(def.Label, relative));
        }

        var entries = new List<LookupEntry>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var fileTag = def.TagFromFile is { Length: > 0 } pattern ? new Regex(pattern, RegexOptions.CultureInvariant) : null;
        foreach (var root in gameRoots)
        {
            var folder = Path.Combine(root, (def.Folder ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(folder, "*.txt").OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            {
                ScriptBlock document;
                try
                {
                    document = ScriptDocument.Parse(File.ReadAllText(file));
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    throw new GeneratorOutputException(GeneratorsTexts.InFile(file, GeneratorsTexts.ParseFailed(ex.Message)));
                }

                var tag = fileTag?.Match(Path.GetFileName(file)) is { Success: true } match && match.Groups.Count > 1 ? match.Groups[1].Value.ToUpperInvariant() : null;
                foreach (var entry in Blocks(def, document, 1, tag, english))
                {
                    if (positions.TryGetValue(entry.Key, out var index))
                    {
                        entries[index] = entry;
                    }
                    else
                    {
                        positions[entry.Key] = entries.Count;
                        entries.Add(entry);
                    }
                }
            }
        }

        return entries;
    }

    private static IEnumerable<LookupEntry> Blocks(LookupSourceDef def, ScriptBlock block, int depth, string? parentTag, GameLocalization english)
    {
        foreach (var statement in block.Entries.OfType<ScriptStatement>())
        {
            if (statement.Value is not ScriptBlock inner)
            {
                continue;
            }

            var tag = parentTag ?? BlockTag(def, inner);
            var name = english.Text(statement.Key);
            if (!def.NamedOnly || name is not null)
            {
                yield return new LookupEntry(def.Id, statement.Key, name ?? statement.Key, english.Desc(statement.Key) ?? string.Empty, tag ?? string.Empty, string.Empty);
            }

            if (depth < def.Depth)
            {
                foreach (var child in Blocks(def, inner, depth + 1, tag, english))
                {
                    yield return child;
                }
            }
        }
    }

    /// <summary>Erster Wert eines Schlüssels aus <see cref="LookupSourceDef.TagInBlock"/> irgendwo im Block, der wie ein Tag aussieht.</summary>
    private static string? BlockTag(LookupSourceDef def, ScriptBlock block)
    {
        if (def.TagInBlock is not { Count: > 0 } keys)
        {
            return null;
        }

        foreach (var statement in block.Entries.OfType<ScriptStatement>())
        {
            if (statement.Value is ScriptScalar scalar && keys.Contains(statement.Key, StringComparer.OrdinalIgnoreCase) && TagValue.IsMatch(scalar.Text))
            {
                return scalar.Text.ToUpperInvariant();
            }

            if (statement.Value is ScriptBlock inner && BlockTag(def, inner) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
