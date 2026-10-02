using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.Storage;

namespace ParadoxModTool.ModCompat;

/// <summary>
/// Ein als gelöst markierter Fund: Art, Datei, Schlüssel, die beteiligten Mods (ModId, sonst Ordner; sortiert) und ein
/// Fingerabdruck der Beiträge. Ändert eine Mod die umstrittene Stelle, passt der Fingerabdruck nicht mehr und der Fund erscheint wieder.
/// </summary>
public sealed record ResolvedFinding(CompatKind Kind, string Path, string Key, IReadOnlyList<string> Mods, string Fingerprint);

/// <summary>
/// Die als gelöst markierten Funde eines Spiels (<c>games\&lt;id&gt;\compat\resolved.json</c>), gültig in jedem Kompatibilitätsset.
/// Gedacht für Konflikte, die schon behoben sind (z. B. durch eine Dateiersetzung) und nicht mehr in der Liste stehen sollen.
/// </summary>
public sealed class ResolvedFindings
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<ResolvedFinding> _items;

    private ResolvedFindings(List<ResolvedFinding> items)
    {
        _items = items;
    }

    public int Count => _items.Count;

    /// <summary>Liest die Datei; fehlt sie oder ist sie unlesbar, ist die Liste leer.</summary>
    public static ResolvedFindings Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<List<ResolvedFinding>>(File.ReadAllText(path), Options) is { } items)
            {
                return new ResolvedFindings(items.Where(i => i is { Path: not null, Key: not null, Mods: not null, Fingerprint: not null }).ToList());
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Eine kaputte Datei blendet nichts aus; beim nächsten Markieren wird sie neu geschrieben.
        }

        return new ResolvedFindings(new List<ResolvedFinding>());
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        SharedFile.WriteAllTextAtomic(path, JsonSerializer.Serialize(_items, Options));
    }

    public bool IsResolved(CompatFinding finding)
    {
        var candidates = _items.Where(i => SameSpot(i, finding)).ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var fingerprint = Fingerprint(finding);
        return candidates.Any(i => i.Fingerprint == fingerprint);
    }

    /// <summary>Markiert den Fund als gelöst; eine ältere Markierung derselben Stelle wird ersetzt.</summary>
    public void Mark(CompatFinding finding)
    {
        _items.RemoveAll(i => SameSpot(i, finding));
        _items.Add(new ResolvedFinding(finding.Kind, finding.RelativePath, finding.Key, ModsOf(finding), Fingerprint(finding)));
    }

    public bool Unmark(CompatFinding finding) => _items.RemoveAll(i => SameSpot(i, finding)) > 0;

    private static bool SameSpot(ResolvedFinding item, CompatFinding finding) =>
        item.Kind == finding.Kind
        && string.Equals(item.Path, finding.RelativePath, StringComparison.OrdinalIgnoreCase)
        && item.Key == finding.Key
        && item.Mods.SequenceEqual(ModsOf(finding), StringComparer.OrdinalIgnoreCase);

    private static string Identity(CompatParticipant participant) =>
        participant.IsVanilla ? "vanilla" : participant.ModId is { Length: > 0 } id ? "id:" + id : "folder:" + participant.Id;

    private static List<string> ModsOf(CompatFinding finding) =>
        finding.Contributions
            .Where(c => !c.Participant.IsVanilla)
            .Select(c => Identity(c.Participant))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// SHA-256 über die Beiträge: je Teilnehmer der Text am Schlüssel, bei Funden auf Dateiebene der Dateiinhalt. Zeilennummern zählen
    /// nicht, damit Änderungen an anderer Stelle der Datei die Markierung nicht aufheben.
    /// </summary>
    private static string Fingerprint(CompatFinding finding)
    {
        var parts = finding.Contributions
            .Select(c => Identity(c.Participant) + "|" + c.RelativePath.Replace('\\', '/').ToLowerInvariant() + "|" + (c.Text ?? FileHash(c.FullPath)))
            .OrderBy(p => p, StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", parts))));
    }

    private static string FileHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return "file:" + Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "file:missing";
        }
    }
}
