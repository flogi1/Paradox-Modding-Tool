using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParadoxModTool.ErrorLog;

public enum SuppressionScope
{
    Code,
    CodeAndName,
    Mod,
    File,
}

/// <summary>Code: <c>Value</c> = Code. CodeAndName: <c>Value</c> = Code, <c>Name</c> = Name. Mod: <c>Value</c> = Mod-Ordner. File: <c>Value</c> = voller Dateipfad.</summary>
public sealed record Suppression(SuppressionScope Scope, string Value, string? Name = null);

/// <summary>Unterdrückungen je Spiel (Datei <c>games\&lt;id&gt;\errorlog\suppressions.json</c>, Pfad übergibt die App). Einträge mit <c>defaultSuppressed</c> im Katalog gelten immer als unterdrückt (Plan-Ruling 7).</summary>
public sealed class SuppressionSet
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public List<Suppression> Items { get; set; } = new();

    public static SuppressionSet Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<SuppressionSet>(File.ReadAllText(path), Options) ?? new SuppressionSet() : new SuppressionSet();
        }
        catch (JsonException)
        {
            return new SuppressionSet();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Options));
        File.Move(temp, path, overwrite: true);
    }

    public bool Add(Suppression suppression)
    {
        if (Items.Contains(suppression))
        {
            return false;
        }

        Items.Add(suppression);
        return true;
    }

    public bool Remove(Suppression suppression) => Items.Remove(suppression);

    /// <summary>Die gespeicherten Unterdrückungen, die den Eintrag treffen (ohne die Katalog-Standardunterdrückung).</summary>
    public IReadOnlyList<Suppression> ItemsMatching(ErrorEntry entry, ErrorAttribution attribution) =>
        Items.Where(s => Matches(s, entry, attribution)).ToList();

    public bool IsSuppressed(ErrorEntry entry, ErrorAttribution attribution) =>
        entry.Kind?.DefaultSuppressed == true || Items.Any(s => Matches(s, entry, attribution));

    private static bool Matches(Suppression suppression, ErrorEntry entry, ErrorAttribution attribution) => suppression.Scope switch
    {
        SuppressionScope.Code => suppression.Value == entry.Code,
        SuppressionScope.CodeAndName => suppression.Value == entry.Code && suppression.Name == entry.Name,
        SuppressionScope.Mod => attribution.Mod is not null && SamePath(suppression.Value, attribution.Mod.Directory),
        SuppressionScope.File => attribution.Source is not null && SamePath(suppression.Value, attribution.Source.FullPath),
        _ => false,
    };

    private static bool SamePath(string a, string b) =>
        string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
