using System.Security.Cryptography;

namespace ParadoxModTool.Mods;

/// <summary>Eine Datei im Index: Größe und Zeitpunkt der letzten Änderung (UTC).</summary>
public sealed record ModFileEntry(long Size, DateTime LastWriteUtc);

/// <summary>
/// Alle Dateien eines Mod-Ordners unter den Regeln von <see cref="ModStaging"/> (ohne <c>.git</c>, Snapshots, Archive usw.), mit relativem Pfad (<c>/</c>, Groß-/Kleinschreibung egal).
/// Der Index liest nur, kennt keine Paare und nie das Register. Deshalb taugt er auch für Mods anderer Ersteller und später für den Vergleich vieler Mods.
/// </summary>
public sealed class ModFileIndex
{
    private readonly Dictionary<string, ModFileEntry> _entries;

    private ModFileIndex(string folder, Dictionary<string, ModFileEntry> entries)
    {
        Folder = folder;
        _entries = entries;
    }

    public string Folder { get; }

    public IReadOnlyDictionary<string, ModFileEntry> Entries => _entries;

    /// <summary>Liest den Ordner. Wirft <see cref="DirectoryNotFoundException"/>, wenn er fehlt, und <see cref="OperationCanceledException"/> bei Abbruch.</summary>
    public static ModFileIndex Build(string folder, ModStagingRules? rules = null, CancellationToken cancellationToken = default)
    {
        var entries = new Dictionary<string, ModFileEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var relative in ModStaging.EnumerateFiles(folder, rules ?? ModStagingRules.Default))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar)));
            entries[relative] = new ModFileEntry(info.Length, info.LastWriteTimeUtc);
        }

        return new ModFileIndex(folder, entries);
    }

    public string FullPath(string relativePath) => Path.Combine(Folder, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>SHA-256 des Dateiinhalts (Hex). Erst bei Bedarf berechnet, damit der Index selbst schnell bleibt.</summary>
    public string HashOf(string relativePath)
    {
        using var stream = File.OpenRead(FullPath(relativePath));
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
