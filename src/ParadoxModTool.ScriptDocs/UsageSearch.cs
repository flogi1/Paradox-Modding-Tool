using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.SearchReplace;
using ParadoxModTool.Storage;

namespace ParadoxModTool.ScriptDocs;

public sealed class UsageHit
{
    public string FilePath { get; set; } = string.Empty;

    public int Line { get; set; }

    /// <summary>Die Trefferzeile, getrimmt und auf höchstens 200 Zeichen gekürzt.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>Gespeichertes Ergebnis der Beispielsuche zu einem Eintrag.</summary>
public sealed class UsageSearchResult
{
    public ScriptDocKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime SearchedUtc { get; set; }

    /// <summary>Die durchsuchten Wurzelordner.</summary>
    public List<string> Roots { get; set; } = new();

    /// <summary>Die Suche wurde beim Höchstwert an Treffern abgebrochen.</summary>
    public bool Truncated { get; set; }

    public List<UsageHit> Hits { get; set; } = new();
}

/// <summary>
/// Sucht Beispiele für die Verwendung eines Eintrags in Vanilla- und Mod-Dateien: der Name als ganzes Wort, in Skriptdateien
/// (bei Custom-Localization zusätzlich in Lokalisierungsdateien).
/// </summary>
public static class UsageSearch
{
    public const int MaxHits = 1000;

    private static readonly string[] ScriptExtensions = { ".txt", ".gui", ".gfx" };
    private static readonly string[] CustomLocExtensions = { ".txt", ".yml", ".yaml" };

    public static IReadOnlyCollection<string> ExtensionsFor(ScriptDocKind kind) =>
        kind == ScriptDocKind.CustomLocalization ? CustomLocExtensions : ScriptExtensions;

    /// <summary>
    /// Durchsucht die Wurzelordner nacheinander. Ein Abbruch über <paramref name="cancellation"/> wirft <see cref="OperationCanceledException"/>.
    /// <paramref name="progress"/> meldet die Datei, die gerade gelesen wird. Nach <see cref="MaxHits"/> Treffern endet die Suche mit <see cref="UsageSearchResult.Truncated"/>.
    /// </summary>
    public static UsageSearchResult Run(
        ScriptDocKind kind,
        string name,
        IEnumerable<string> roots,
        DateTime nowUtc,
        CancellationToken cancellation = default,
        IProgress<string>? progress = null)
    {
        var rootList = roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new UsageSearchResult { Kind = kind, Name = name, SearchedUtc = nowUtc, Roots = rootList };
        var options = new SearchOptions { MatchCase = true, WholeWord = true };
        var extensions = ExtensionsFor(kind);

        foreach (var root in rootList)
        {
            foreach (var file in FileSearchService.EnumerateSearchableFiles(root, extensions))
            {
                cancellation.ThrowIfCancellationRequested();
                progress?.Report(file);

                string text;
                try
                {
                    text = File.ReadAllText(file, Encoding.UTF8);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var match in TextSearch.FindAll(text, name, options))
                {
                    if (result.Hits.Count >= MaxHits)
                    {
                        result.Truncated = true;
                        return result;
                    }

                    var line = match.LineText.Trim();
                    result.Hits.Add(new UsageHit
                    {
                        FilePath = file,
                        Line = match.LineNumber,
                        Text = line.Length > 200 ? line[..200] : line,
                    });
                }
            }
        }

        return result;
    }
}

/// <summary>Speichert die Ergebnisse der Beispielsuche, eine Datei je Eintrag, damit eine Suche nicht wiederholt werden muss.</summary>
public sealed class UsageSearchStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public UsageSearchStore(string directory)
    {
        _directory = directory;
    }

    public UsageSearchResult? TryLoad(ScriptDocKind kind, string name)
    {
        var path = PathFor(kind, name);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<UsageSearchResult>(File.ReadAllText(path), Options) : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Save(UsageSearchResult result) =>
        SharedFile.WriteAllTextAtomic(PathFor(result.Kind, result.Name), JsonSerializer.Serialize(result, Options));

    /// <summary>Dateiname aus Art und Name: unzulässige Zeichen ersetzt, dazu ein Hash des genauen Namens gegen Kollisionen.</summary>
    internal string PathFor(ScriptDocKind kind, string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        if (safe.Length > 60)
        {
            safe = safe[..60];
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)), 0, 4);
        return Path.Combine(_directory, $"{kind}-{safe}-{hash}.json");
    }
}
