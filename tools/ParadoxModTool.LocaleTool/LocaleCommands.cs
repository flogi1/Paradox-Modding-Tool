using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ParadoxModTool.Localization;

namespace ParadoxModTool.LocaleTool;

/// <summary>
/// Befehle für das Übersetzen der Sprachdateien in einem <c>locales</c>-Ordner: fehlende Keys ausgeben, übersetzte Keys einfügen, Dateien prüfen.
/// Referenz ist immer <c>de.json</c>; die Reihenfolge der Keys folgt <c>en.json</c>, damit Diffs lesbar bleiben.
/// </summary>
public static class LocaleCommands
{
    private const string MachineTranslatedKey = "_machineTranslated";

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Fehlende oder leere Keys von <paramref name="code"/> als JSON <c>{"key":{"de":…,"en":…}}</c>, höchstens <paramref name="max"/>.</summary>
    public static string Missing(string localesDir, string code, string? prefix, int max)
    {
        var (german, english) = References(localesDir);
        var existing = UiTextTable.Read(LocaleFile(localesDir, code));
        var keys = OrderedKeys(german, english)
            .Where(key => !existing.TryGetValue(key, out var text) || text.Length == 0)
            .Where(key => prefix is null || key.StartsWith(prefix + ".", StringComparison.Ordinal))
            .Take(max);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var key in keys)
            {
                writer.WriteStartObject(key);
                writer.WriteString("de", german[key]);
                writer.WriteString("en", english.GetValueOrDefault(key, string.Empty));
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Fügt die Texte aus <paramref name="chunkPath"/> (flaches JSON <c>{"key":"Text"}</c>) in <c>&lt;code&gt;.json</c> ein. Eine neue Datei bekommt
    /// <c>"_machineTranslated": true</c>. Exit-Code 0; 2, wenn der Chunk Keys enthält, die es in <c>de.json</c> nicht gibt (dann wird nichts
    /// geschrieben); 3, wenn Chunk oder vorhandene Datei kein gültiges JSON sind.
    /// </summary>
    public static int Merge(string localesDir, string code, string chunkPath)
    {
        var (german, english) = References(localesDir);
        var chunk = UiTextTable.ReadFile(chunkPath);
        if (chunk.Error is not null)
        {
            Console.Error.WriteLine($"{chunkPath}: {chunk.Error.Message}");
            return 3;
        }

        var unknown = chunk.Texts.Keys.Where(key => !german.ContainsKey(key)).ToList();
        if (unknown.Count > 0)
        {
            foreach (var key in unknown)
            {
                Console.Error.WriteLine($"unknown key: {key}");
            }

            return 2;
        }

        var path = LocaleFile(localesDir, code);
        var comments = new List<(string Name, JsonElement Value)>();
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (File.Exists(path))
        {
            JsonDocument document;
            try
            {
                // Stream-Überladung wie UiTextTable.ReadFile: überspringt ein UTF-8-BOM (Windows-Editor).
                using var stream = File.OpenRead(path);
                document = JsonDocument.Parse(stream);
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"{path}: {ex.Message}");
                return 3;
            }

            using (document)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Name.StartsWith('_'))
                    {
                        comments.Add((property.Name, property.Value.Clone()));
                    }
                    else if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        texts[property.Name] = property.Value.GetString()!;
                    }
                }
            }
        }
        else
        {
            comments.Add((MachineTranslatedKey, JsonDocument.Parse("true").RootElement.Clone()));
        }

        foreach (var (key, text) in chunk.Texts)
        {
            texts[key] = text;
        }

        var order = OrderedKeys(german, english).ToList();
        var ordered = order.Where(texts.ContainsKey).Concat(texts.Keys.Except(order, StringComparer.Ordinal));
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in comments)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }

            foreach (var key in ordered)
            {
                writer.WriteString(key, texts[key]);
            }

            writer.WriteEndObject();
        }

        File.WriteAllBytes(path, buffer.ToArray());
        return 0;
    }

    /// <summary>Prüft <paramref name="code"/> oder, ohne Code, jede Sprache außer Deutsch. Exit-Code 1, wenn eine Datei Fehler hat.</summary>
    public static int Check(string localesDir, string? code, TextWriter output)
    {
        var (german, english) = References(localesDir);
        var codes = code is not null
            ? new[] { code }
            : Directory.EnumerateFiles(localesDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => name!.Length > 0 && !name.StartsWith('_') && !name.Equals(UiText.SourceLanguage, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .ToArray()!;

        var result = 0;
        foreach (var language in codes)
        {
            var path = LocaleFile(localesDir, language!);
            if (!File.Exists(path))
            {
                output.WriteLine($"{language}: no file");
                result = 1;
                continue;
            }

            var report = LocaleValidator.Check(path, german, english);
            var missing = report.MissingKeys.Count;
            output.WriteLine($"{language}: {report.Percent} %" + (missing > 0 ? $" ({missing} missing)" : string.Empty));
            if (report.ParseError is { } parse)
            {
                output.WriteLine($"  error: line {parse.Line}, position {parse.Position}: {parse.Message}");
            }

            foreach (var key in report.UnknownKeys)
            {
                output.WriteLine($"  error: unknown key {key}");
            }

            foreach (var mismatch in report.PlaceholderMismatches)
            {
                output.WriteLine($"  error: {mismatch.Key}: expected {Join(mismatch.Expected)}, found {Join(mismatch.Found)}");
            }

            foreach (var key in report.LongTexts)
            {
                output.WriteLine($"  hint: long text {key}");
            }

            if (report.HasErrors)
            {
                result = 1;
            }
        }

        return result;
    }

    private static (IReadOnlyDictionary<string, string> German, IReadOnlyDictionary<string, string> English) References(string localesDir) =>
        (UiTextTable.Read(LocaleFile(localesDir, UiText.SourceLanguage)), UiTextTable.Read(LocaleFile(localesDir, "en")));

    /// <summary>Keys der Referenz in der Reihenfolge von <c>en.json</c>, danach die nur deutschen in ihrer Reihenfolge.</summary>
    private static IEnumerable<string> OrderedKeys(IReadOnlyDictionary<string, string> german, IReadOnlyDictionary<string, string> english) =>
        english.Keys.Where(german.ContainsKey).Concat(german.Keys.Where(key => !english.ContainsKey(key)));

    private static string LocaleFile(string localesDir, string code) => Path.Combine(localesDir, code + ".json");

    private static string Join(IReadOnlyList<string> placeholders) => placeholders.Count == 0 ? "-" : string.Join(" ", placeholders);
}
