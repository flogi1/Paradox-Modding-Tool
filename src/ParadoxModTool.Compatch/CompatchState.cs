using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Compatch;

/// <summary>Eine gespeicherte Entscheidung, gebunden an die Prüfsumme der A- und B-Werte zum Entscheidungszeitpunkt (<see cref="MergeAction.ValueHash"/>).</summary>
public sealed record StoredDecision(DecisionKind Kind, string? ManualText, string ValueHash);

/// <summary>Eigener Text für einen ganzen Block (markierte Zeilen übernommen); <see cref="AddressHash"/> bindet ihn an die A/B-Fassung.</summary>
public sealed record StoredBlockText(string Text, string AddressHash);

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

    /// <summary>Filter im Merge-Fenster: übernommene fehlende Blöcke der Hauptmod ausblenden. Fehlt in älteren Ständen → aus.</summary>
    public bool HideFilledMissing { get; set; }

    /// <summary>Filter im Merge-Fenster: Dateien ausblenden, die in beiden Mods liegen und nur automatisch ersetzt werden. Fehlt in älteren Ständen → aus.</summary>
    public bool HideReplacedFiles { get; set; }

    /// <summary>Aktions-ID → Entscheidung.</summary>
    public Dictionary<string, StoredDecision> Decisions { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Adress-ID (<see cref="EntryAddress.Id"/>) → gewählte Form.</summary>
    public Dictionary<string, PatchForm> Forms { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Aktions-IDs, die der Nutzer als gesehen markiert hat.</summary>
    public HashSet<string> Seen { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Adress-ID → eigener Blocktext. Gewinnt über die Einzelentscheidungen des Blocks, bis eine davon neu entschieden wird.</summary>
    public Dictionary<string, StoredBlockText> BlockTexts { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Filter im Merge-Fenster: entschiedene Stellen ausblenden. Fehlt in älteren Ständen → aus.</summary>
    public bool HideDecided { get; set; }

    /// <summary>
    /// Filter im Merge-Fenster: Stellen ausblenden, an denen A und B gleich sind (Herkunft "A und B gleich"). <see langword="null"/> = Vorgabe:
    /// an im Update-Modus (nach einem fertigen Update sind dort fast alle Stellen gleich), sonst aus.
    /// </summary>
    public bool? HideEqual { get; set; }

    /// <summary>Filter im Merge-Fenster: nur unsichere Stellen zeigen. Fehlt in älteren Ständen → aus.</summary>
    public bool UncertainOnly { get; set; }

    /// <summary>Update-Modus: installierte Vanilla-Version, auf die angepasst wird (Ziel <see cref="PatchTarget.UpdateMod"/>).</summary>
    public string? UpdateNewVersion { get; set; }

    /// <summary>Update-Modus: je relativem Pfad die alte Vanilla-Version, gegen die verglichen wird; fehlt ein Pfad, gab es keine.</summary>
    public Dictionary<string, string> UpdateOldVersions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Update-Modus: alle Dateien des Laufs (relativer Pfad), auch die ohne alte Fassung; zum Fortsetzen eines gespeicherten Laufs.</summary>
    public List<string> UpdatePaths { get; set; } = new();
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

    /// <summary>
    /// Update-Modus: geschriebene Dateien der Mod, relativer Pfad (klein, <c>/</c>) → SHA-256 des geschriebenen Inhalts. Hat eine Datei beim
    /// nächsten Laden einen anderen Inhalt, verwirft <see cref="CompatchSession"/> ihre Entscheidungen (<see cref="CompatchSession.ForgottenFiles"/>).
    /// </summary>
    public Dictionary<string, string> UpdateFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Ein gespeicherter Arbeitsstand mit seinem Speicherort und dem Zeitpunkt des letzten Speicherns.</summary>
public sealed record SavedCompatch(CompatchStateStore Store, CompatchState State, DateTime SavedUtc);

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

    /// <summary>Arbeitsstand eines Vanilla-Update-Laufs der Mod, getrennt vom Kompatch-Stand derselben Mod.</summary>
    public static string DirectoryForUpdate(string gameDirectory, string modId) => Path.Combine(DirectoryFor(gameDirectory, modId), "update");

    /// <summary>
    /// Update-Stand, dessen Zielversion nicht mehr die installierte ist (das Spiel wurde seither erneut aktualisiert). Unbekannte installierte
    /// Version zählt nicht als veraltet.
    /// </summary>
    public static bool IsOutdatedUpdate(CompatchState state, string? installedVersion) =>
        state.Target == PatchTarget.UpdateMod
        && installedVersion is not null
        && ParadoxModTool.VanillaDiff.GameVersionComparer.Instance.Compare(state.UpdateNewVersion, installedVersion) != 0;

    /// <summary>Datei des Arbeitsstands (Startfenster: Arbeitsstand löschen).</summary>
    public string StatePath => Path.Combine(Directory, "state.json");

    public CompatchState? Load() => Read<CompatchState>("state.json");

    /// <summary>
    /// Alle gespeicherten Arbeitsstände eines Spiels (Startfenster, "Arbeitsstand fortsetzen"), der zuletzt gespeicherte zuerst. Nur lesend:
    /// unlesbare Stände werden übergangen, ohne eine <c>.bad</c>-Kopie anzulegen.
    /// </summary>
    public static IReadOnlyList<SavedCompatch> Saved(string gameDirectory)
    {
        var root = Path.Combine(gameDirectory, "compatch");
        if (!System.IO.Directory.Exists(root))
        {
            return Array.Empty<SavedCompatch>();
        }

        var result = new List<SavedCompatch>();
        foreach (var folder in System.IO.Directory.EnumerateDirectories(root).SelectMany(d => new[] { d, Path.Combine(d, "update") }))
        {
            var path = Path.Combine(folder, "state.json");
            try
            {
                if (File.Exists(path) && JsonSerializer.Deserialize<CompatchState>(File.ReadAllText(path), Options) is { } state)
                {
                    result.Add(new SavedCompatch(new CompatchStateStore(folder), state, File.GetLastWriteTimeUtc(path)));
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Unlesbar: im Startfenster nicht anbieten; beim normalen Laden sichert Load() die Datei.
            }
        }

        return result.OrderByDescending(s => s.SavedUtc).ToList();
    }

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
