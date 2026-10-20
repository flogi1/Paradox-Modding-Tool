using System.Text.Json;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>Eine Quelle einer Erklärung: <c>wiki</c>, <c>measured</c>, <c>vanilla</c> oder <c>unsure</c>.</summary>
public sealed record ClassicExplanationSource(string Kind, string? Date, string? Note);

/// <summary>
/// Eine Erklärung zu einem Element, einer Eigenschaft oder einem Thema des klassischen Formats, Texte schon in der gewählten Sprache.
/// <see cref="Kind"/> ist <c>element</c>, <c>property</c> oder <c>topic</c>; <see cref="Sketch"/> ist <see langword="null"/>, <c>anchors</c>,
/// <c>percent</c>, <c>frames</c> oder <c>nineSlice</c>.
/// </summary>
public sealed record ClassicExplanation(
    string Kind,
    string Key,
    string Text,
    string? Default,
    IReadOnlyList<string> Pitfalls,
    string? Sketch,
    IReadOnlyList<ClassicExplanationSource> Sources,
    IReadOnlyList<string> Children)
{
    /// <summary>Keine Quelle außer <c>unsure</c> (oder gar keine): die Aussage ist nicht belegt und wird als unsicher gezeigt.</summary>
    public bool IsUncertain => Sources.All(s => string.Equals(s.Kind, "unsure", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Erklärtexte des klassischen Designers aus <c>profiles/guiexplain/classic.json</c> für ein Spiel. Ein Eintrag mit <c>games</c> für dieses Spiel gewinnt
/// gegen einen allgemeinen mit gleicher Art und gleichem Schlüssel. Laden wirft nie: eine fehlende oder ungültige Datei gibt eine leere Instanz mit
/// <see cref="Problems"/>, ungültige Einträge werden übersprungen und gemeldet.
/// </summary>
public sealed class ClassicExplanations
{
    private static readonly string[] Kinds = { "element", "property", "topic" };
    private static readonly string[] Sketches = { "anchors", "percent", "frames", "nineSlice" };
    private static readonly string[] SourceKinds = { "wiki", "measured", "vanilla", "unsure" };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Dictionary<(string Kind, string Key), Entry> _entries;

    private ClassicExplanations(Dictionary<(string Kind, string Key), Entry> entries, IReadOnlyList<string> problems)
    {
        _entries = entries;
        Problems = problems;
    }

    public static ClassicExplanations Empty { get; } = new(new Dictionary<(string, string), Entry>(), Array.Empty<string>());

    public IReadOnlyList<string> Problems { get; }

    /// <summary>Liest die Datei für <paramref name="gameId"/> (Profil-Id, z.B. <c>hoi4</c>). Wirft nie.</summary>
    public static ClassicExplanations Load(string path, string gameId)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failed(path + ": " + ex.Message);
        }

        FileModel? file;
        try
        {
            file = JsonSerializer.Deserialize<FileModel>(json, Options);
        }
        catch (JsonException ex)
        {
            return Failed(path + ": " + ex.Message);
        }

        if (file is null)
        {
            return Failed(path + ": leere Datei");
        }

        var problems = new List<string>();
        // Je (Art, Schlüssel) der Eintrag für genau dieses Spiel und der allgemeine; der spezielle gewinnt.
        var specific = new Dictionary<(string, string), Entry>();
        var general = new Dictionary<(string, string), Entry>();
        var number = 0;
        foreach (var model in file.Entries ?? new List<EntryModel>())
        {
            number++;
            var label = $"Eintrag {number}" + (string.IsNullOrWhiteSpace(model?.Key) ? string.Empty : " (" + model!.Key + ")");
            if (Convert(model, label, problems) is not { } entry)
            {
                continue;
            }

            var games = model!.Games ?? new List<string>();
            var isSpecific = games.Any(g => string.Equals(g, gameId, StringComparison.OrdinalIgnoreCase));
            if (games.Count > 0 && !isSpecific)
            {
                continue;
            }

            var target = isSpecific ? specific : general;
            if (!target.TryAdd((entry.Kind, entry.Key), entry))
            {
                problems.Add($"{label}: Art und Schlüssel kommen doppelt vor, der erste gilt.");
            }
        }

        foreach (var (key, entry) in specific)
        {
            general[key] = entry;
        }

        return new ClassicExplanations(general, problems);
    }

    public ClassicExplanation? Property(string name, string language) => Find("property", name, language);

    public ClassicExplanation? Element(string key, string language) => Find("element", key, language);

    public ClassicExplanation? Topic(string key, string language) => Find("topic", key, language);

    private static ClassicExplanations Failed(string problem) => new(new Dictionary<(string, string), Entry>(), new[] { problem });

    private ClassicExplanation? Find(string kind, string key, string language) =>
        _entries.TryGetValue((kind, key.ToLowerInvariant()), out var entry) ? entry.In(language) : null;

    private static Entry? Convert(EntryModel? model, string label, List<string> problems)
    {
        var kind = Kinds.FirstOrDefault(k => string.Equals(k, model?.Kind, StringComparison.OrdinalIgnoreCase));
        if (kind is null)
        {
            problems.Add($"{label}: unbekannte Art '{model?.Kind}'.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(model!.Key))
        {
            problems.Add($"{label}: Schlüssel fehlt.");
            return null;
        }

        var texts = Folded(model.Text);
        if (texts.Count == 0)
        {
            problems.Add($"{label}: Text fehlt.");
            return null;
        }

        string? sketch = null;
        if (!string.IsNullOrWhiteSpace(model.Sketch))
        {
            sketch = Sketches.FirstOrDefault(s => string.Equals(s, model.Sketch, StringComparison.OrdinalIgnoreCase));
            if (sketch is null)
            {
                problems.Add($"{label}: unbekannte Skizze '{model.Sketch}'.");
            }
        }

        var sources = new List<ClassicExplanationSource>();
        foreach (var source in model.Sources ?? new List<SourceModel>())
        {
            var sourceKind = SourceKinds.FirstOrDefault(k => string.Equals(k, source?.Kind, StringComparison.OrdinalIgnoreCase));
            if (sourceKind is null)
            {
                problems.Add($"{label}: unbekannte Quelle '{source?.Kind}'.");
                continue;
            }

            sources.Add(new ClassicExplanationSource(sourceKind, source!.Date, source.Note));
        }

        var pitfalls = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (language, list) in model.Pitfalls ?? new Dictionary<string, List<string>>())
        {
            pitfalls[language] = list ?? new List<string>();
        }

        return new Entry(
            kind,
            model.Key.Trim().ToLowerInvariant(),
            texts,
            string.IsNullOrWhiteSpace(model.Default) ? null : model.Default,
            pitfalls,
            sketch,
            sources,
            (model.Children ?? new List<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).ToList());
    }

    private static Dictionary<string, string> Folded(Dictionary<string, string>? texts)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (language, text) in texts ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrEmpty(text))
            {
                result[language] = text;
            }
        }

        return result;
    }

    // Wie ErrorLog.LocalizedText.Pick (GuiDesign darf ErrorLog nicht kennen): die Sprache, sonst Englisch, sonst Deutsch, sonst der erste Eintrag.
    private static T? Pick<T>(IReadOnlyDictionary<string, T> values, string language) =>
        values.TryGetValue(language, out var exact) ? exact
        : values.TryGetValue("en", out var english) ? english
        : values.TryGetValue("de", out var german) ? german
        : values.Values.FirstOrDefault();

    private sealed record Entry(
        string Kind,
        string Key,
        Dictionary<string, string> Texts,
        string? Default,
        Dictionary<string, List<string>> Pitfalls,
        string? Sketch,
        IReadOnlyList<ClassicExplanationSource> Sources,
        IReadOnlyList<string> Children)
    {
        public ClassicExplanation In(string language) => new(
            Kind,
            Key,
            Pick(Texts, language) ?? string.Empty,
            Default,
            (IReadOnlyList<string>?)Pick(Pitfalls, language) ?? Array.Empty<string>(),
            Sketch,
            Sources,
            Children);
    }

    private sealed class FileModel
    {
        public int Version { get; set; }

        public List<EntryModel>? Entries { get; set; }
    }

    private sealed class EntryModel
    {
        public string? Kind { get; set; }

        public string? Key { get; set; }

        public List<string>? Games { get; set; }

        public Dictionary<string, string>? Text { get; set; }

        public string? Default { get; set; }

        public Dictionary<string, List<string>>? Pitfalls { get; set; }

        public string? Sketch { get; set; }

        public List<SourceModel>? Sources { get; set; }

        public List<string>? Children { get; set; }
    }

    private sealed class SourceModel
    {
        public string? Kind { get; set; }

        public string? Date { get; set; }

        public string? Note { get; set; }
    }
}
