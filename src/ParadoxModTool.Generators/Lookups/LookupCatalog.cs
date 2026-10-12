namespace ParadoxModTool.Generators.Lookups;

/// <summary>Hinweis zu einer ausgelassenen Quelle; <see cref="Kind"/> ist <c>gameFolder</c>, <c>asset</c> oder <c>file</c> (für den passenden Knopf im Fenster).</summary>
public sealed record LookupNote(string SourceId, string Kind, string Text);

/// <summary>
/// Alle Einträge der Quellen eines Felds (Spec 2026-10-05-generator-packages-lookups, Abschnitt 1). Eine Quelle, die fehlt oder sich nicht lesen lässt, wird mit
/// einem Hinweis ausgelassen; die anderen bleiben. Übersetzungen der Spielquellen werden je Sprache einmal gelesen und gemerkt.
/// </summary>
public sealed class LookupCatalog
{
    private readonly Dictionary<string, LookupSourceDef> _defs;
    private readonly Dictionary<string, GameLocalization> _languages = new(StringComparer.Ordinal);

    private LookupCatalog(Dictionary<string, LookupSourceDef> defs, IReadOnlyList<LookupEntry> entries, IReadOnlyList<LookupNote> notes, IReadOnlyList<(string Id, string Label)> sources)
    {
        _defs = defs;
        Entries = entries;
        Notes = notes;
        Sources = sources;
    }

    public IReadOnlyList<LookupEntry> Entries { get; }

    public IReadOnlyList<LookupNote> Notes { get; }

    /// <summary>Quellen des Felds in seiner Reihenfolge, mit Bezeichnung für die Spalte und den Filter "Art".</summary>
    public IReadOnlyList<(string Id, string Label)> Sources { get; }

    public static LookupCatalog Load(GeneratorPackage package, FieldLookupDef lookup, IReadOnlyList<string> gameRoots)
    {
        var defs = (package.Lookups ?? Array.Empty<LookupSourceDef>()).ToDictionary(d => d.Id, StringComparer.Ordinal);
        var entries = new List<LookupEntry>();
        var notes = new List<LookupNote>();
        var sources = new List<(string, string)>();
        GameLocalization? english = null;
        foreach (var id in lookup.Sources)
        {
            if (!defs.TryGetValue(id, out var def))
            {
                continue;
            }

            sources.Add((def.Id, def.Label));
            try
            {
                if (def.Kind == LookupKind.GameKeys)
                {
                    // Unsicherer Pfad zuerst: sonst hieße der Grund "Spielordner fehlt" oder "Datei", obwohl der Pfad das Problem ist
                    if (def.Folder is { Length: > 0 } relative && !SafeRelativePath.IsSafe(relative))
                    {
                        notes.Add(new LookupNote(def.Id, "unsafe", GeneratorsTexts.LookupUnsafeFolder(def.Label, relative)));
                        continue;
                    }

                    var folder = (def.Folder ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                    if (!gameRoots.Any(root => Directory.Exists(Path.Combine(root, folder))))
                    {
                        notes.Add(new LookupNote(def.Id, "gameFolder", GeneratorsTexts.LookupNoGameFolder(def.Label)));
                        continue;
                    }

                    english ??= GameLocalization.Load(gameRoots, LanguageFill.English);
                    entries.AddRange(GameKeySource.Read(def, gameRoots, english));
                }
                else
                {
                    var path = package.AssetPath(def.Asset!);
                    if (!File.Exists(path))
                    {
                        notes.Add(new LookupNote(def.Id, "asset", GeneratorsTexts.LookupAssetMissing(def.Label, def.Asset!)));
                        continue;
                    }

                    entries.AddRange(JsonLookupSource.Read(def, path));
                }
            }
            catch (GeneratorOutputException ex)
            {
                notes.Add(new LookupNote(def.Id, "file", GeneratorsTexts.LookupSourceFailed(def.Label, ex.Message)));
            }
            catch (ArgumentException ex)
            {
                // Ungültiges tagFromFile (der Loader übersetzt das Muster nicht): Hinweis statt Absturz, auch bei "Aus Datenbank".
                notes.Add(new LookupNote(def.Id, "file", GeneratorsTexts.LookupSourceFailed(def.Label, ex.Message)));
            }
        }

        return new LookupCatalog(defs, entries, notes, sources);
    }

    /// <summary>
    /// Offizielle Texte eines Spiel-Eintrags in einer Sprache: <c>name</c> und <c>desc</c>, nur die vorhandenen. Leer bei JSON-Einträgen und Sprachen ohne
    /// Lokalisierung.
    /// </summary>
    public IReadOnlyDictionary<string, string> Translations(LookupEntry entry, string language, IReadOnlyList<string> gameRoots)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!_defs.TryGetValue(entry.SourceId, out var def) || def.Kind != LookupKind.GameKeys)
        {
            return result;
        }

        if (!_languages.TryGetValue(language, out var localization))
        {
            localization = GameLocalization.Load(gameRoots, language);
            _languages[language] = localization;
        }

        if (localization.Text(entry.Key) is { Length: > 0 } name)
        {
            result["name"] = name;
        }

        if (localization.Desc(entry.Key) is { Length: > 0 } desc)
        {
            result["desc"] = desc;
        }

        return result;
    }
}
