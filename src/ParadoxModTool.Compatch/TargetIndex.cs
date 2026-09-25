namespace ParadoxModTool.Compatch;

public sealed record TargetLocation(string RelativePath, KeyPart Part);

public sealed record TargetUnreadable(string RelativePath, string Error);

/// <summary>
/// Was schon im Zielordner steht: PMT-eigene Patch-Dateien und, beim Ziel "Kompatch", die Schlüssel aller übrigen (fremden) Patch-Dateien
/// mit Textstelle. Grundlage für Ersetzen an Ort und Stelle, Doppelt-Schutz, Entfernen und "Aus bestehendem Patch". Liest nur.
/// </summary>
public sealed class TargetIndex
{
    private readonly Dictionary<EntryAddress, List<TargetLocation>> _keys = new();
    private readonly List<string> _own = new();
    private readonly Dictionary<string, FileParts> _foreign = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TargetUnreadable> _unreadable = new();

    private TargetIndex(string folder)
    {
        Folder = folder;
    }

    public string Folder { get; }

    public IReadOnlyList<string> OwnFiles => _own;

    public IReadOnlyDictionary<string, FileParts> ForeignFiles => _foreign;

    public IReadOnlyList<TargetUnreadable> Unreadable => _unreadable;

    public IReadOnlyList<TargetLocation> Find(EntryAddress address) =>
        _keys.TryGetValue(address, out var list) ? list : Array.Empty<TargetLocation>();

    public IReadOnlyList<TargetLocation> FindById(string addressId) =>
        _keys.Where(k => k.Key.Id == addressId).SelectMany(k => k.Value).ToList();

    /// <param name="readForeignKeys"><see langword="false"/> beim Ziel "Basis-Mod": deren übrige Dateien sind die Mod selbst, keine Patch-Dateien.</param>
    public static TargetIndex Build(string folder, CompatchInput input, bool readForeignKeys)
    {
        var index = new TargetIndex(folder);
        if (!Directory.Exists(folder))
        {
            return index;
        }

        foreach (var full in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(folder, full).Replace('\\', '/');
            if (relative.EndsWith(".pmt-tmp", StringComparison.OrdinalIgnoreCase)
                || input.Filter.Check(relative) is { Reason: SkipReason.Metadata or SkipReason.HiddenKind })
            {
                continue;
            }

            if (OwnPatchFiles.IsOwn(relative))
            {
                index._own.Add(relative);
                continue;
            }

            if (!readForeignKeys)
            {
                continue;
            }

            FileParts parts;
            try
            {
                parts = KeyPartReader.Read(MergeSide.B, relative, full, input.Rules.Match(relative), input.KeyOperators, input.Cache);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                index._unreadable.Add(new TargetUnreadable(relative, ex.Message));
                continue;
            }

            if (!parts.IsScript)
            {
                continue;
            }

            if (parts.Error is not null)
            {
                index._unreadable.Add(new TargetUnreadable(relative, parts.Error));
                continue;
            }

            index._foreign[relative] = parts;
            foreach (var part in parts.Keys)
            {
                if (!index._keys.TryGetValue(part.Address, out var list))
                {
                    list = new List<TargetLocation>();
                    index._keys[part.Address] = list;
                }

                list.Add(new TargetLocation(relative, part));
            }
        }

        return index;
    }
}
