namespace ParadoxModTool.Compatch;

public enum SkipReason
{
    /// <summary>Ausgeblendete Dateiart (Grafik, Audio, Modelle, Schriften).</summary>
    HiddenKind,

    /// <summary>Gesperrter Pfad einer eigenen Mod, Einstellung "Gesperrte Dateien nicht einlesen" ist an.</summary>
    Locked,

    /// <summary>Metadaten der Mod (<c>.metadata/</c>, Dateien im Wurzelordner). Werden nie verglichen.</summary>
    Metadata,

    /// <summary>PMT-eigene Patch-Datei der Basis-Mod (Ziel "Basis-Mod"); sie gehört nicht zum Spielzustand ohne Patch.</summary>
    OwnPatch,

    /// <summary>Nicht Teil des Laufs (Update-Modus: nur die Dateien, die an die neue Vanilla-Fassung angepasst werden).</summary>
    NotInRun,
}

public sealed record SkippedFile(string RelativePath, SkipReason Reason, string Kind);

/// <summary>Welche Dateien der Kompatch-Merge nicht liest: ausgeblendete Dateiarten, Metadaten und, falls gewünscht, gesperrte Pfade.</summary>
public sealed class CompatchFileFilter
{
    public static readonly IReadOnlyDictionary<string, string> DefaultHiddenExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".dds"] = "Grafik",
        [".png"] = "Grafik",
        [".tga"] = "Grafik",
        [".mesh"] = "Modelle",
        [".anim"] = "Modelle",
        [".asset"] = "Modelle",
        [".wav"] = "Audio",
        [".ogg"] = "Audio",
        [".bank"] = "Audio",
        [".fnt"] = "Schriften",
        [".ttf"] = "Schriften",
    };

    private readonly Dictionary<string, string> _hidden;
    private readonly List<string> _locked;
    private readonly HashSet<string>? _onlyPaths;

    /// <param name="onlyPaths">Nur diese Dateien lesen (Update-Modus); <see langword="null"/> = alle.</param>
    public CompatchFileFilter(IReadOnlyDictionary<string, string> hiddenExtensions, IEnumerable<string> lockedPaths, bool skipLocked, IReadOnlyCollection<string>? onlyPaths = null)
    {
        _hidden = new Dictionary<string, string>(hiddenExtensions, StringComparer.OrdinalIgnoreCase);
        _locked = lockedPaths.Select(Normalize).Distinct().ToList();
        SkipLocked = skipLocked;
        _onlyPaths = onlyPaths?.Select(Normalize).ToHashSet(StringComparer.Ordinal);
    }

    public static CompatchFileFilter Default => new(DefaultHiddenExtensions, Array.Empty<string>(), skipLocked: false);

    public bool SkipLocked { get; }

    public IReadOnlyDictionary<string, string> HiddenExtensions => _hidden;

    public IReadOnlyList<string> LockedPaths => _locked;

    public IReadOnlyCollection<string>? OnlyPaths => _onlyPaths;

    /// <summary>Grund, warum die Datei nicht gelesen wird, oder <see langword="null"/>.</summary>
    public SkippedFile? Check(string relativePath)
    {
        var path = Normalize(relativePath);
        if (ParadoxModTool.Mods.ModMetadata.IsDescriptionFile(path))
        {
            return new SkippedFile(relativePath, SkipReason.Metadata, "Metadaten");
        }

        if (_onlyPaths is not null && !_onlyPaths.Contains(path))
        {
            return new SkippedFile(relativePath, SkipReason.NotInRun, "Update");
        }

        if (_hidden.TryGetValue(Path.GetExtension(path), out var kind))
        {
            return new SkippedFile(relativePath, SkipReason.HiddenKind, kind);
        }

        return SkipLocked && IsLocked(relativePath) ? new SkippedFile(relativePath, SkipReason.Locked, "Gesperrt") : null;
    }

    public bool IsHiddenKind(string relativePath) => _hidden.ContainsKey(Path.GetExtension(relativePath));

    /// <summary>Dateiart nach <see cref="DefaultHiddenExtensions"/> ("Grafik", "Audio" …) oder <see langword="null"/> für Skript- und Textdateien.</summary>
    public static string? KindOf(string relativePath) => DefaultHiddenExtensions.TryGetValue(Path.GetExtension(relativePath), out var kind) ? kind : null;

    /// <summary>Gehört die Datei zu einer der ausgeblendeten Dateiarten (Dateilisten des Compare und Update Tools, Update-Lauf)?</summary>
    public static bool IsHiddenKind(string relativePath, IEnumerable<string> hiddenKinds) =>
        KindOf(relativePath) is { } kind && hiddenKinds.Contains(kind, StringComparer.Ordinal);

    /// <summary>Liegt der Pfad in einem gesperrten Pfad? Ein Ordner sperrt alles darunter, ein leerer Eintrag die ganze Mod.</summary>
    public bool IsLocked(string relativePath)
    {
        var path = Normalize(relativePath);
        return _locked.Any(l => l.Length == 0 || path == l || path.StartsWith(l + "/", StringComparison.Ordinal));
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/').ToLowerInvariant();
}
