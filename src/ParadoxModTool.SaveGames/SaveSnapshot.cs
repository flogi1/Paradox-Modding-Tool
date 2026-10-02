using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.SaveGames;

public enum SaveNameKind
{
    Variable,
    List,

    /// <summary>Flag-Name, der als Wert einer Variable steht (Werttyp mit Darstellung <c>flagName</c>, z.B. <c>flag:X</c>).</summary>
    FlagValue,
}

/// <summary>Ein Name im Spielstand mit Art und Anzahl der Objekte, bei denen er vorkommt.</summary>
public sealed record SaveName(string Name, SaveNameKind Kind, int ObjectCount);

/// <summary>Index eines geladenen Spielstands: Einträge nach Name (Variable, Liste, Flag-Wert) und nach Objekt.</summary>
public sealed class SaveSnapshot
{
    private readonly Dictionary<(string Name, SaveNameKind Kind), List<SaveVariableEntry>> _byName;
    private readonly Dictionary<string, List<SaveVariableEntry>> _byOwner;
    private readonly HashSet<string> _allNames;

    private SaveSnapshot(SaveFileInfo file, SaveReadResult read, SaveGameSettings settings)
    {
        File = file;
        Status = read.Status;
        Settings = settings;
        Entries = read.Entries;
        Objects = new SaveObjectNames(settings, read.Captured);

        var flagTypes = settings.ValueTypes.Where(p => p.Value.Format == "flagName").Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        _byName = new();
        _byOwner = new(StringComparer.Ordinal);
        foreach (var entry in read.Entries)
        {
            Add(_byName, (entry.Name, entry.Kind == SaveEntryKind.Variable ? SaveNameKind.Variable : SaveNameKind.List), entry);
            if (flagTypes.Contains(entry.Type) && entry.RawValue.Length > 0)
            {
                Add(_byName, (entry.RawValue, SaveNameKind.FlagValue), entry);
            }

            Add(_byOwner, entry.OwnerPath, entry);
        }

        Names = _byName
            .Select(p => new SaveName(p.Key.Name, p.Key.Kind, p.Value.Select(e => e.OwnerPath).Distinct(StringComparer.Ordinal).Count()))
            .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(n => n.Kind)
            .ToList();
        _allNames = _byName.Keys.Select(k => k.Name).ToHashSet(StringComparer.Ordinal);
        ObjectsWithVariables = _byOwner.Keys
            .Select(Objects.Resolve)
            .OrderBy(o => o.Kind.Order)
            .ThenBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public SaveFileInfo File { get; }

    public SaveReadStatus Status { get; }

    public SaveGameSettings Settings { get; }

    public IReadOnlyList<SaveVariableEntry> Entries { get; }

    public SaveObjectNames Objects { get; }

    /// <summary>Alle Namen, sortiert ohne Groß-/Kleinschreibung; derselbe Name kann mit mehreren Arten vorkommen.</summary>
    public IReadOnlyList<SaveName> Names { get; }

    /// <summary>Alle Objekte mit Variablen, nach Objektart (Global zuerst, Sonstige zuletzt), dann Anzeigename.</summary>
    public IReadOnlyList<SaveObjectRef> ObjectsWithVariables { get; }

    public static SaveSnapshot Build(SaveFileInfo file, SaveReadResult read, SaveGameSettings settings) => new(file, read, settings);

    /// <summary>Prüft, liest und indiziert einen Stand. Nicht lesbare (komprimierte oder binäre) Stände werden nie gelesen.</summary>
    public static SaveSnapshot Load(string path, SaveGameSettings settings, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var file = SaveFiles.Inspect(path, settings.TextMarker, settings.MetadataBlock, settings.DateField);
        if (!file.Readable)
        {
            throw new InvalidOperationException(SaveExplorerTexts.DebugHint);
        }

        var read = SaveVariableReader.ReadFile(path, SaveObjectNames.CaptureRequestFor(settings), progress, cancellationToken, file.Compressed);
        return Build(file, read, settings);
    }

    /// <summary>Einträge zu einem Namen; bei <see cref="SaveNameKind.FlagValue"/> die Variablen, deren Wert dieser Flag ist.</summary>
    public IReadOnlyList<SaveVariableEntry> EntriesFor(string name, SaveNameKind kind) =>
        _byName.TryGetValue((name, kind), out var entries) ? entries : Array.Empty<SaveVariableEntry>();

    public bool Contains(string name) => _allNames.Contains(name);

    public IReadOnlyList<SaveVariableEntry> EntriesOf(string ownerPath) =>
        _byOwner.TryGetValue(ownerPath, out var entries) ? entries : Array.Empty<SaveVariableEntry>();

    private static void Add<TKey>(Dictionary<TKey, List<SaveVariableEntry>> index, TKey key, SaveVariableEntry entry)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = new List<SaveVariableEntry>();
            index[key] = list;
        }

        list.Add(entry);
    }
}
