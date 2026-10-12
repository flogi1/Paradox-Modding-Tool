namespace ParadoxModTool.Generators;

/// <summary>Fortschritt von <see cref="LocalizationKeyIndex.Build"/>: <paramref name="Root"/> ist fertig gelesen, <paramref name="Done"/> von <paramref name="Total"/> Ordnern.</summary>
public sealed record KeyIndexProgress(string Root, int Done, int Total);

/// <summary>
/// Alle Lokalisierungsschlüssel der Ordner (Spiel, Mods) nach ihrem <see cref="LocalizationHash"/>: gelesen werden alle <c>*.yml</c> unter
/// <c>main_menu/localization</c> und <c>in_game/localization</c>, in allen Sprachen. Zwei Schlüssel mit gleichem Hash füllen das error.log.
/// </summary>
public sealed class LocalizationKeyIndex
{
    private static readonly string[] Parts = { "main_menu", "in_game" };

    /// <summary>Je gebautem Teil (ein <see cref="Build"/>) die Schlüssel nach Hash; <see cref="Combine"/> reiht Teile aneinander, ohne sie zu kopieren.</summary>
    private readonly IReadOnlyList<Dictionary<uint, List<string>>> _parts;

    private LocalizationKeyIndex(IReadOnlyList<Dictionary<uint, List<string>>> parts) => _parts = parts;

    /// <summary>
    /// Ein Index aus mehreren Indizes, z. B. dem einmal gebauten Spiel-Teil und dem nach jedem Erzeugen neu gebauten Mod-Teil; die Teile werden geteilt,
    /// nicht kopiert.
    /// </summary>
    public static LocalizationKeyIndex Combine(IEnumerable<LocalizationKeyIndex> indexes) => new(indexes.SelectMany(i => i._parts).ToList());

    /// <summary>
    /// Liest die Schlüssel der Ordner <paramref name="roots"/>. <paramref name="progress"/> meldet jeden fertigen Ordner; <paramref name="ct"/> wird vor jeder
    /// Datei geprüft (ein Abbruch wirft <see cref="OperationCanceledException"/> und liefert keinen halben Index).
    /// </summary>
    public static LocalizationKeyIndex Build(IEnumerable<string> roots, IProgress<KeyIndexProgress>? progress = null, CancellationToken ct = default)
    {
        var byHash = new Dictionary<uint, List<string>>();
        var rootList = roots.ToList();
        for (var i = 0; i < rootList.Count; i++)
        {
            var root = rootList[i];
            ct.ThrowIfCancellationRequested();
            foreach (var part in Parts)
            {
                var folder = Path.Combine(root, part, "localization");
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in YmlFiles(folder, ct))
                {
                    ct.ThrowIfCancellationRequested();
                    string text;
                    try
                    {
                        text = File.ReadAllText(file);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }

                    foreach (var key in YmlReader.Values(text).Keys)
                    {
                        var hash = LocalizationHash.Murmur3(key);
                        if (!byHash.TryGetValue(hash, out var keys))
                        {
                            byHash[hash] = keys = new List<string>();
                        }

                        if (!keys.Contains(key, StringComparer.Ordinal))
                        {
                            keys.Add(key);
                        }
                    }
                }
            }

            progress?.Report(new KeyIndexProgress(root, i + 1, rootList.Count));
        }

        return new LocalizationKeyIndex(new[] { byHash });
    }

    /// <summary>
    /// Alle <c>*.yml</c> unter dem Ordner wie <see cref="Directory.GetFiles(string, string, SearchOption)"/>, aber gesperrte Unterordner werden übersprungen
    /// statt die ganze Vorschau scheitern zu lassen; bricht das Lesen des Ordners trotzdem ab, zählt, was bis dahin gefunden war.
    /// </summary>
    private static List<string> YmlFiles(string folder, CancellationToken ct)
    {
        var files = new List<string>();
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0, MatchType = MatchType.Win32 };
            foreach (var file in Directory.EnumerateFiles(folder, "*.yml", options))
            {
                ct.ThrowIfCancellationRequested();
                files.Add(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Rest des Ordners überspringen
        }

        return files;
    }

    /// <summary>Indizierte Schlüssel mit demselben Hash, aber anderem Text; derselbe Schlüssel in anderen Dateien oder Sprachen zählt nicht.</summary>
    public IReadOnlyList<string> Others(string key)
    {
        var hash = LocalizationHash.Murmur3(key);
        return _parts.SelectMany(p => p.TryGetValue(hash, out var keys) ? keys : Enumerable.Empty<string>())
            .Where(k => !string.Equals(k, key, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToList();
    }
}
