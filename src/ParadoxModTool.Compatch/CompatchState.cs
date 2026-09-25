using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Compatch;

/// <summary>Eine gespeicherte Entscheidung, gebunden an die Prüfsumme der A- und B-Werte zum Entscheidungszeitpunkt (<see cref="MergeAction.ValueHash"/>).</summary>
public sealed record StoredDecision(DecisionKind Kind, string? ManualText, string ValueHash);

/// <summary>Arbeitsstand eines Kompatchs (<c>games\&lt;id&gt;\compatch\&lt;mod-id&gt;\state.json</c>), nie im Mod-Ordner.</summary>
public sealed class CompatchState
{
    public int Version { get; set; } = 1;

    public string ModAFolder { get; set; } = string.Empty;

    public string ModBFolder { get; set; } = string.Empty;

    public string? ModAId { get; set; }

    public string? ModBId { get; set; }

    public string ModAName { get; set; } = string.Empty;

    public string ModBName { get; set; } = string.Empty;

    public bool BLoadsAfterA { get; set; } = true;

    public PatchTarget Target { get; set; }

    public string TargetFolder { get; set; } = string.Empty;

    public string? TargetModId { get; set; }

    public bool Cautious { get; set; }

    /// <summary>Ausgeblendete Dateiarten (Namen aus <see cref="CompatchFileFilter.DefaultHiddenExtensions"/>, z. B. "Grafik").</summary>
    public List<string> HiddenKinds { get; set; } = new();

    public bool SkipLocked { get; set; }

    /// <summary>Ziel "Basis-Mod", Option 1: nur gegen die Hauptmod vergleichen. Fehlt in älteren Ständen → aus.</summary>
    public bool MainReplacesVanilla { get; set; }

    /// <summary>Ziel "Basis-Mod", Option 2: Blöcke der Hauptmod übernehmen, die im Ziel ganz fehlen. Fehlt in älteren Ständen → aus.</summary>
    public bool FillMissingFromMain { get; set; }

    /// <summary>Aktions-ID → Entscheidung.</summary>
    public Dictionary<string, StoredDecision> Decisions { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Adress-ID (<see cref="EntryAddress.Id"/>) → gewählte Form.</summary>
    public Dictionary<string, PatchForm> Forms { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Aktions-IDs, die der Nutzer als gesehen markiert hat.</summary>
    public HashSet<string> Seen { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Vergleichsbasis (<c>base.json</c>): wie A und B beim letzten Schreiben aussahen und was PMT geschrieben hat.</summary>
public sealed class CompatchBase
{
    public int Version { get; set; } = 1;

    public DateTime WrittenUtc { get; set; }

    public string? ModAVersion { get; set; }

    public string? ModBVersion { get; set; }

    /// <summary>Adress-ID → Prüfsumme der wirksamen Einträge von A und B (<see cref="CompatchSession.AddressHash"/>).</summary>
    public Dictionary<string, string> AddressHashes { get; set; } = new(StringComparer.Ordinal);

    public List<WrittenEntry> ForeignEntries { get; set; } = new();

    /// <summary>Geschriebene ganze Dateien: relativer Pfad (klein, <c>/</c>) → SHA-256.</summary>
    public Dictionary<string, string> WrittenFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Liest und schreibt <c>state.json</c> und <c>base.json</c> eines Kompatchs. Eine unlesbare Datei wird als <c>.bad</c> gesichert.</summary>
public sealed class CompatchStateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public CompatchStateStore(string directory)
    {
        Directory = directory;
    }

    public string Directory { get; }

    /// <summary>Hinweis nach einem unlesbaren Stand (<see cref="CompatchTexts.StateUnreadable"/>); sonst <see langword="null"/>.</summary>
    public string? Warning { get; private set; }

    public static string DirectoryFor(string gameDirectory, string modId) => Path.Combine(gameDirectory, "compatch", modId);

    public CompatchState? Load() => Read<CompatchState>("state.json");

    public void Save(CompatchState state) => Write("state.json", state);

    public CompatchBase? LoadBase() => Read<CompatchBase>("base.json");

    public void SaveBase(CompatchBase baseline) => Write("base.json", baseline);

    private T? Read<T>(string name) where T : class
    {
        var path = Path.Combine(Directory, name);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Warning = CompatchTexts.StateUnreadable;
            try
            {
                File.Copy(path, path + ".bad", overwrite: true);
            }
            catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
            {
                // Die Kopie ist nur ein Versuch.
            }

            return null;
        }
    }

    private void Write<T>(string name, T value)
    {
        System.IO.Directory.CreateDirectory(Directory);
        SharedFile.WriteAllTextAtomic(Path.Combine(Directory, name), JsonSerializer.Serialize(value, Options));
    }
}
