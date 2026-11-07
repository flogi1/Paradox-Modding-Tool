using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.ErrorLog;

/// <summary>
/// PMT-eigene Prüfung zu ENC-001: Dateien einer Mod, die mit UTF-8-BOM beginnen, obwohl das Spielprofil ihre Endung ohne BOM schreibt
/// (<see cref="ModFileEncoding.NewFileEncodingName"/> ist <c>utf-8</c>, heute Stellaris-Skript, .gui und .gfx). Stellaris liest solche Dateien
/// falsch, ein Scripted Effect fiel dabei ohne jede Zeile im error.log aus (gemessen 2026-10-16); der Katalog allein findet also nicht alle.
/// Lokalisierung (<c>.yml</c>) braucht ein BOM und zählt nie. Übersprungen werden Dateien direkt im Mod-Ordner (readme.txt, changelog.txt
/// lädt das Spiel nicht als Skript), Ordner mit Punkt am Anfang (<c>.git</c>, <c>.pmt-…</c>) und verknüpfte Ordner (Symlink, Junction).
/// </summary>
public static class BomCheck
{
    /// <summary>Katalogcode, dessen Name und Erklärung die Funde tragen.</summary>
    public const string Code = "ENC-001";

    private static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

    /// <summary>
    /// Relative Pfade (mit "/", sortiert) der betroffenen Dateien unter <paramref name="modRoot"/>. Liest je Datei nur die ersten drei Bytes;
    /// unlesbare Dateien und Ordner werden ausgelassen. <paramref name="excludedDirectories"/>: Ordner, die nicht zur Mod gehören, z. B. der
    /// Sicherungsordner im Mod-Ordner. Die Sicherungen dort sind .zip-Dateien und würden nicht gefunden; der Ausschluss spart nur das Durchsuchen.
    /// </summary>
    public static IReadOnlyList<string> Find(GameProfile? profile, string modRoot, IEnumerable<string>? excludedDirectories = null)
    {
        var extensions = ((IEnumerable<FileTypeMapping>?)profile?.FileTypes ?? Array.Empty<FileTypeMapping>())
            .Select(f => f.Extension)
            .Where(e => !string.IsNullOrWhiteSpace(e) && ModFileEncoding.NewFileEncodingName(profile, e) == ModFileEncoding.Utf8)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (extensions.Count == 0 || !Directory.Exists(modRoot))
        {
            return Array.Empty<string>();
        }

        var excluded = (excludedDirectories ?? Array.Empty<string>())
            .Select(d => Path.TrimEndingDirectorySeparator(Path.GetFullPath(d)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        var pending = new Stack<string>();
        pending.Push(modRoot);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            try
            {
                var isRoot = ReferenceEquals(directory, modRoot);
                foreach (var file in isRoot ? Enumerable.Empty<string>() : Directory.EnumerateFiles(directory))
                {
                    if (extensions.Contains(Path.GetExtension(file)) && StartsWithBom(file))
                    {
                        found.Add(Path.GetRelativePath(modRoot, file).Replace('\\', '/'));
                    }
                }

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    if (!Path.GetFileName(child).StartsWith('.')
                        && !new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint)
                        && !excluded.Contains(Path.TrimEndingDirectorySeparator(Path.GetFullPath(child))))
                    {
                        pending.Push(child);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ordner nicht lesbar: auslassen, die übrigen weiter prüfen.
            }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    /// <summary>
    /// Prüft alle eigenen Mods (Florians Wunsch nach dem App-Test 2026-10-28: nicht nur die aktive). Jeder Ordner wie <see cref="Find"/> mit seinen
    /// Ausschlüssen; fehlende Ordner liefern nichts. Liegt ein Mod-Ordner in einem anderen (verschachtelt) oder ist er doppelt eingetragen, erscheint
    /// jede Datei nur einmal, bei der Mod mit dem tiefsten Ordner. Sortiert nach Mod-Name, dann Pfad. Die Wurzeln baut der Aufrufer im UI-Thread
    /// (das Mod-Register ist nicht threadsicher); diese Methode darf im Hintergrund laufen.
    /// </summary>
    public static IReadOnlyList<BomFinding> FindInMods(GameProfile? profile, IReadOnlyList<BomScanRoot> roots)
    {
        // Ein ungültiger Pfad im Mod-Register überspringt nur diese Mod, nicht die ganze Prüfung.
        var normalized = new List<BomScanRoot>();
        foreach (var r in roots)
        {
            try
            {
                normalized.Add(r with { Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(r.Root)) });
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
            }
        }

        var rootPaths = normalized.Select(r => r.Root).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<BomFinding>();
        foreach (var root in normalized)
        {
            foreach (var relative in Find(profile, root.Root, root.Excluded))
            {
                var fullPath = Path.GetFullPath(Path.Combine(root.Root, relative.Replace('/', Path.DirectorySeparatorChar)));

                // Eine tiefere eingetragene Mod umfasst die Datei: Die gehört zu ihr (deren eigener Durchlauf entscheidet, auch über Ausschlüsse).
                var deeper = rootPaths.Any(other => other.Length > root.Root.Length
                    && fullPath.StartsWith(other + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                if (!deeper && seen.Add(fullPath))
                {
                    result.Add(new BomFinding(root.ModName, root.Root, relative, fullPath));
                }
            }
        }

        return result
            .OrderBy(f => f.ModName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Inhalt ohne die ersten drei BOM-Bytes, alle übrigen Bytes unverändert; <see langword="null"/>, wenn der Inhalt kein BOM hat.</summary>
    public static byte[]? WithoutBom(byte[] bytes) => ModFileEncoding.HasUtf8Bom(bytes) ? bytes[Bom.Length..] : null;

    /// <summary>
    /// Neuer Inhalt der Datei für die Korrektur: alles nach dem BOM, Byte für Byte; <see langword="null"/>, wenn die Datei (inzwischen) kein BOM
    /// hat. Liest nur, schreibt nichts: Geschrieben wird in PMT über eine rückgängig machbare Binär-Schreibaktion mit Sicherung der Mod.
    /// </summary>
    public static byte[]? ReadWithoutBom(string path) => WithoutBom(File.ReadAllBytes(path));

    /// <summary>
    /// Wie <see cref="ReadWithoutBom(string)"/>, dazu Änderungszeit und Länge der Datei, gelesen vor dem Inhalt. Das Entfernen läuft im Hintergrund:
    /// Unmittelbar vor dem Schreiben prüft <see cref="Unchanged"/>, ob die Datei seitdem gespeichert wurde (dann bleibt sie unberührt).
    /// </summary>
    public static byte[]? ReadWithoutBom(string path, out BomFileStamp stamp)
    {
        stamp = BomFileStamp.Of(path);
        return ReadWithoutBom(path);
    }

    /// <summary>
    /// <see langword="true"/>, wenn die Datei noch dieselbe Änderungszeit und Länge hat wie <paramref name="stamp"/>; eine fehlende oder unlesbare
    /// Datei gilt als geändert.
    /// </summary>
    public static bool Unchanged(string path, BomFileStamp stamp)
    {
        try
        {
            var now = BomFileStamp.Of(path);
            return now.Length >= 0 && now == stamp;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool StartsWithBom(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> head = stackalloc byte[3];
            return stream.ReadAtLeast(head, 3, throwOnEndOfStream: false) == 3 && ModFileEncoding.HasUtf8Bom(head);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>Ein zu prüfender Mod-Ordner: Name der Mod, Ordner und Ordner darin, die nicht zur Mod gehören (z. B. ihr Sicherungsordner).</summary>
public sealed record BomScanRoot(string ModName, string Root, IReadOnlyList<string> Excluded);

/// <summary>Ein Fund der BOM-Prüfung: Mod, ihr Ordner (normalisiert), Pfad relativ dazu (mit "/") und voller Pfad.</summary>
public sealed record BomFinding(string ModName, string ModRoot, string RelativePath, string FullPath);

/// <summary>Änderungszeit (UTC) und Länge einer Datei; Länge -1, wenn sie fehlt.</summary>
public readonly record struct BomFileStamp(DateTime LastWriteUtc, long Length)
{
    public static BomFileStamp Of(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? new BomFileStamp(info.LastWriteTimeUtc, info.Length) : new BomFileStamp(DateTime.MinValue, -1);
    }
}
