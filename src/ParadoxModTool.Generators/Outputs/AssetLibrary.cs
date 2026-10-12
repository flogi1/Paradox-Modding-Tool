using System.IO.Compression;

namespace ParadoxModTool.Generators;

/// <summary>Eine Asset-Bibliothek eines Pakets: ein Zip (z. B. coa_library.zip, direkt gelesen) oder ein Ordner. Einträge mit "/" getrennt, sortiert.</summary>
public sealed class AssetLibrary
{
    private readonly string _path;
    private readonly bool _isZip;

    private AssetLibrary(string path, bool isZip, IReadOnlyList<string> entries)
    {
        _path = path;
        _isZip = isZip;
        Entries = entries;
    }

    public IReadOnlyList<string> Entries { get; }

    public static AssetLibrary Open(string path)
    {
        if (File.Exists(path))
        {
            using var zip = ZipFile.OpenRead(path);
            var entries = zip.Entries.Where(e => e.Name.Length > 0).Select(e => e.FullName).OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList();
            return new AssetLibrary(path, true, entries);
        }

        if (Directory.Exists(path))
        {
            var entries = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(path, f).Replace(Path.DirectorySeparatorChar, '/'))
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new AssetLibrary(path, false, entries);
        }

        throw new GeneratorOutputException(GeneratorsTexts.AssetLibraryMissing(path));
    }

    /// <summary>Der erste Eintrag mit genau diesen Bytes (Einleser: Wappen der Mod in der Bibliothek wiederfinden); vorher wird die Größe verglichen.</summary>
    public string? FindEqual(byte[] data)
    {
        if (!_isZip)
        {
            return Entries.FirstOrDefault(e =>
            {
                var path = Path.Combine(_path, e.Replace('/', Path.DirectorySeparatorChar));
                return new FileInfo(path).Length == data.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(data);
            });
        }

        using var zip = ZipFile.OpenRead(_path);
        foreach (var zipEntry in zip.Entries.Where(e => e.Name.Length > 0 && e.Length == data.Length).OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
        {
            using var stream = zipEntry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            if (buffer.GetBuffer().AsSpan(0, (int)buffer.Length).SequenceEqual(data))
            {
                return zipEntry.FullName;
            }
        }

        return null;
    }

    public byte[] Read(string entry)
    {
        if (!Entries.Contains(entry, StringComparer.OrdinalIgnoreCase))
        {
            throw new GeneratorOutputException(GeneratorsTexts.AssetMissing(entry));
        }

        if (!_isZip)
        {
            return File.ReadAllBytes(Path.Combine(_path, entry.Replace('/', Path.DirectorySeparatorChar)));
        }

        using var zip = ZipFile.OpenRead(_path);
        var zipEntry = zip.Entries.First(e => string.Equals(e.FullName, entry, StringComparison.OrdinalIgnoreCase));
        using var stream = zipEntry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
