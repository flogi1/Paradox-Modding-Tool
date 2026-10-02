using System.Text.Json;

namespace ParadoxModTool.Desync;

/// <summary>Das Urteil des Nutzers über einen Fund der MP-Prüfung (JSON: <c>harmless</c>, <c>risky</c>).</summary>
public enum SafetyVerdict
{
    /// <summary>Unbedenklich: der Fund zählt als <see cref="SafetySeverity.Allowed"/>.</summary>
    Harmless,

    /// <summary>Riskant: der Fund zählt als <see cref="SafetySeverity.Risk"/>.</summary>
    Risky,
}

/// <summary>
/// Die Urteile des Nutzers über Funde der MP-Prüfung, je Spiel in <c>&lt;GameDirectory&gt;/desync/safety-verdicts.json</c>, Form
/// <c>{ "version": 1, "verdicts": { "console_other|add_gold": "harmless" } }</c>. Der Schlüssel ist <c>regelId|token</c> klein geschrieben
/// (siehe <see cref="KeyOf"/>), der Wert <c>harmless</c> oder <c>risky</c>. Geschrieben wird über eine temporäre Datei, die die alte ersetzt.
/// </summary>
public sealed class SafetyVerdicts
{
    private const int FormatVersion = 1;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    private readonly string _path;
    private readonly Dictionary<string, SafetyVerdict> _verdicts = new(StringComparer.Ordinal);

    /// <param name="path">Pfad der Datei, <c>&lt;GameDirectory&gt;/desync/safety-verdicts.json</c>.</param>
    public SafetyVerdicts(string path) => _path = path;

    /// <summary>Schlüssel eines Urteils: <c>regelId|token</c>, klein geschrieben.</summary>
    public static string KeyOf(string ruleId, string token) => (ruleId + "|" + token).ToLowerInvariant();

    /// <summary>
    /// Liest die Datei und ersetzt damit den Stand im Speicher. Eine fehlende Datei ergibt <c>true</c> und einen leeren Stand. Ist die Datei
    /// kaputt (kein JSON oder falsche Form), wird sie nach <c>&lt;Pfad&gt;.bak</c> verschoben (eine ältere <c>.bak</c> wird überschrieben), der
    /// Stand bleibt leer und das Ergebnis ist <c>false</c>. Unbekannte Urteilswerte in der Datei werden übersprungen.
    /// </summary>
    /// <exception cref="IOException">Die Datei lässt sich nicht lesen oder nicht verschieben.</exception>
    /// <exception cref="UnauthorizedAccessException">Kein Lese- oder Schreibrecht.</exception>
    public bool Load()
    {
        _verdicts.Clear();

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(_path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return true;
        }

        var parsed = TryParse(bytes);
        if (parsed is null)
        {
            File.Move(_path, _path + ".bak", overwrite: true);
            return false;
        }

        foreach (var (key, verdict) in parsed)
        {
            _verdicts[key] = verdict;
        }

        return true;
    }

    /// <summary>Die Urteile aus dem Dateiinhalt, oder <c>null</c>, wenn der Inhalt kein JSON der erwarteten Form ist.</summary>
    private static List<KeyValuePair<string, SafetyVerdict>>? TryParse(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var result = new List<KeyValuePair<string, SafetyVerdict>>();
            if (!document.RootElement.TryGetProperty("verdicts", out var verdicts) || verdicts.ValueKind == JsonValueKind.Null)
            {
                return result;
            }

            if (verdicts.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in verdicts.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.Name))
                {
                    continue;
                }

                SafetyVerdict? verdict = property.Value.GetString()?.ToLowerInvariant() switch
                {
                    "harmless" => SafetyVerdict.Harmless,
                    "risky" => SafetyVerdict.Risky,
                    _ => null,
                };
                if (verdict is { } value)
                {
                    result.Add(new(property.Name.ToLowerInvariant(), value));
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Das Urteil zu Regel und Token (ohne Rücksicht auf Groß-/Kleinschreibung), oder <c>null</c>, wenn keines gesetzt ist.</summary>
    public SafetyVerdict? Get(string ruleId, string token) =>
        _verdicts.TryGetValue(KeyOf(ruleId, token), out var verdict) ? verdict : null;

    /// <summary>Setzt das Urteil im Speicher; <c>null</c> entfernt es. Gespeichert wird erst mit <see cref="Save"/>.</summary>
    public void Set(string ruleId, string token, SafetyVerdict? verdict)
    {
        var key = KeyOf(ruleId, token);
        if (verdict is { } value)
        {
            _verdicts[key] = value;
        }
        else
        {
            _verdicts.Remove(key);
        }
    }

    /// <summary>Wendet das Urteil auf die Strenge aus dem Profil an: unbedenklich ergibt <c>Allowed</c>, riskant <c>Risk</c>, ohne Urteil bleibt sie.</summary>
    public SafetySeverity Apply(string ruleId, string token, SafetySeverity profileSeverity) => Get(ruleId, token) switch
    {
        SafetyVerdict.Harmless => SafetySeverity.Allowed,
        SafetyVerdict.Risky => SafetySeverity.Risk,
        _ => profileSeverity,
    };

    /// <summary>Schreibt alle Urteile (Schlüssel nach Ordinal sortiert, damit Diffs stabil bleiben) über eine temporäre Datei, die die alte ersetzt.</summary>
    /// <exception cref="IOException">Ordner bzw. Datei lassen sich nicht schreiben.</exception>
    /// <exception cref="UnauthorizedAccessException">Kein Schreibrecht.</exception>
    public void Save()
    {
        if (Path.GetDirectoryName(_path) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            using (var writer = new Utf8JsonWriter(stream, WriterOptions))
            {
                writer.WriteStartObject();
                writer.WriteNumber("version", FormatVersion);
                writer.WriteStartObject("verdicts");
                foreach (var (key, verdict) in _verdicts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WriteString(key, verdict == SafetyVerdict.Harmless ? "harmless" : "risky");
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            File.Move(temporary, _path, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
