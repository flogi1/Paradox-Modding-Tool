using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParadoxModTool.Desync;

/// <summary>
/// Zwischenspeicher ungewichteter Vergleichsergebnisse: eine Datei <c>&lt;Ordner&gt;/&lt;oosId16&gt;.json</c> je OOS, gültig, solange
/// der Stempel (Größe und Schreibzeit beider Spielstände, Formatstand) gleich bleibt. Geschrieben wird über eine temporäre Datei,
/// die die alte ersetzt; eine unlesbare Datei gilt als nicht vorhanden.
/// </summary>
public sealed class DesyncCache
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _directory;

    public DesyncCache(string directory) => _directory = directory;

    /// <summary>
    /// Formatstand, Größe und Schreibzeit (UTC-Ticks) des Referenz- und des abweichenden Spielstands und der
    /// <see cref="DesyncProfile.Fingerprint"/> des Profils, mit <c>|</c> verbunden: ein geändertes Profil (Tiefe, Größengrenze, Objektmuster)
    /// ergibt andere Einheiten und macht das gespeicherte Ergebnis ungültig.
    /// </summary>
    public static string StampOf(OosDump reference, OosDump desynced, DesyncProfile profile)
    {
        var (referenceSize, referenceTime) = reference.StampOf(DumpFileKind.OosSave);
        var (desyncedSize, desyncedTime) = desynced.StampOf(DumpFileKind.OosSave);
        return string.Join('|',
            DesyncComparison.FormatVersion.ToString(CultureInfo.InvariantCulture),
            referenceSize.ToString(CultureInfo.InvariantCulture),
            referenceTime.Ticks.ToString(CultureInfo.InvariantCulture),
            desyncedSize.ToString(CultureInfo.InvariantCulture),
            desyncedTime.Ticks.ToString(CultureInfo.InvariantCulture),
            profile.Fingerprint);
    }

    /// <summary>
    /// Das gespeicherte Ergebnis; null, wenn es fehlt, unlesbar ist, eine leere Einheit (<c>null</c>) enthält oder Stempel, Id oder
    /// Formatstand nicht passen.
    /// </summary>
    public DesyncDiffResult? TryLoad(string oosId16, string stamp)
    {
        try
        {
            using var stream = File.OpenRead(PathOf(oosId16));
            var entry = JsonSerializer.Deserialize<CacheFile>(stream, Options);
            var result = entry?.Result;
            return entry is not null
                && result is not null
                && string.Equals(entry.Stamp, stamp, StringComparison.Ordinal)
                && result.FormatVersion == DesyncComparison.FormatVersion
                && string.Equals(result.OosId16, oosId16, StringComparison.Ordinal)
                && result.Units is not null
                && result.Units.All(unit => unit is not null && unit.Fields is not null)
                && result.Names?.Tables is not null
                    ? result
                    : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Speichert das Ergebnis unter seiner OOS-Id; eine vorhandene Datei wird ersetzt.</summary>
    /// <exception cref="IOException">Ordner oder Datei lassen sich nicht schreiben.</exception>
    /// <exception cref="UnauthorizedAccessException">Kein Schreibrecht im Ordner.</exception>
    public void Save(DesyncDiffResult result, string stamp)
    {
        Directory.CreateDirectory(_directory);
        var target = PathOf(result.OosId16);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, new CacheFile(stamp, result), Options);
            }

            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private string PathOf(string oosId16) => Path.Combine(_directory, oosId16 + ".json");

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

    /// <summary>Form der Datei: Stempel und Ergebnis.</summary>
    private sealed record CacheFile(string Stamp, DesyncDiffResult? Result);
}
