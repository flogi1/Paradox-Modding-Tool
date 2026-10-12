using System.Text.Json;

namespace ParadoxModTool.Generators.Lookups;

/// <summary>
/// Einträge einer <see cref="LookupKind.Json"/>-Quelle (Spec 2026-10-05-generator-packages-lookups, Abschnitt 1). <see cref="LookupSourceDef.Items"/> ist ein Pfad aus
/// Eigenschaftsnamen, getrennt durch Punkte; <c>name[]</c> geht in jedes Element des Arrays. <see cref="LookupSourceDef.Fields"/> nennt je Eintragsfeld
/// (<c>key</c>, <c>name</c>, <c>desc</c>, <c>tag</c>, <c>nation</c>) die Eigenschaft des Elements oder mit <c>../</c> die des Elternelements.
/// </summary>
public static class JsonLookupSource
{
    /// <summary>Fehlende oder nicht-textuelle Eigenschaften ergeben leere Werte; ungültiges JSON wirft <see cref="GeneratorOutputException"/>.</summary>
    public static IReadOnlyList<LookupEntry> Read(LookupSourceDef def, string path)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new GeneratorOutputException(GeneratorsTexts.InFile(path, GeneratorsTexts.ParseFailed(ex.Message)));
        }

        using (document)
        {
            var fields = def.Fields ?? new Dictionary<string, string>();
            var segments = (def.Items ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries);
            var entries = new List<LookupEntry>();
            foreach (var (item, parent) in Walk(document.RootElement, null, segments, 0))
            {
                string Value(string field)
                {
                    if (!fields.TryGetValue(field, out var property) || property.Length == 0)
                    {
                        return string.Empty;
                    }

                    var source = property.StartsWith("../", StringComparison.Ordinal) ? parent : item;
                    var name = property.StartsWith("../", StringComparison.Ordinal) ? property[3..] : property;
                    return source is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                        ? value.GetString() ?? string.Empty
                        : string.Empty;
                }

                entries.Add(new LookupEntry(def.Id, Value("key"), Value("name"), Value("desc"), Value("tag"), Value("nation")));
            }

            return entries;
        }
    }

    /// <summary>Elemente am Ende des Pfads, jeweils mit dem Element, aus dessen Array sie kommen.</summary>
    private static IEnumerable<(JsonElement Item, JsonElement? Parent)> Walk(JsonElement current, JsonElement? parent, string[] segments, int index)
    {
        if (index == segments.Length)
        {
            yield return (current, parent);
            yield break;
        }

        var segment = segments[index];
        var many = segment.EndsWith("[]", StringComparison.Ordinal);
        var name = many ? segment[..^2] : segment;
        if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out var next))
        {
            yield break;
        }

        if (!many)
        {
            foreach (var result in Walk(next, parent, segments, index + 1))
            {
                yield return result;
            }

            yield break;
        }

        if (next.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var element in next.EnumerateArray())
        {
            foreach (var result in Walk(element, current, segments, index + 1))
            {
                yield return result;
            }
        }
    }
}
