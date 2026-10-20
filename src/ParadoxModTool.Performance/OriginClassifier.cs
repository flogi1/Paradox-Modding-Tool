using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Performance;

/// <summary>
/// Ordnet dem Pfad einer Dump-Zeile eine <see cref="RowOrigin"/> zu. Eine Wurzel „enthält“ den Pfad,
/// wenn <c>wurzel/&lt;phase&gt;/&lt;pfad&gt;</c> für eine Phase aus <see cref="SourceResolver.Phases"/> oder <c>wurzel/&lt;pfad&gt;</c> existiert.
/// Ergebnisse werden je Pfad zwischengespeichert; es wird nichts geschrieben.
/// </summary>
public sealed class OriginClassifier
{
    private readonly IReadOnlyList<string> _activeModRoots;
    private readonly IReadOnlyList<string> _otherModRoots;
    private readonly IReadOnlyList<string> _vanillaRoots;
    private readonly Func<string, bool> _fileExists;
    private readonly Dictionary<string, RowOrigin> _cache = new(StringComparer.Ordinal);

    /// <param name="activeModRoot">Wurzel der aktiven Mod; leer oder nur Leerraum gilt als nicht vorhanden. Das gilt auch für leere Einträge in den beiden anderen Listen.</param>
    /// <param name="otherModRoots">Wurzeln der übrigen Mods des Playsets.</param>
    /// <param name="vanillaRoots">Vanilla-Wurzeln, z. B. aus <c>SourceResolver.VanillaRootsFor</c>.</param>
    /// <param name="fileExists">Dateiprüfung; Standard ist <see cref="File.Exists(string?)"/>.</param>
    public OriginClassifier(
        string activeModRoot,
        IReadOnlyList<string> otherModRoots,
        IReadOnlyList<string> vanillaRoots,
        Func<string, bool>? fileExists = null)
    {
        _activeModRoots = NonBlank(new[] { activeModRoot });
        _otherModRoots = NonBlank(otherModRoots);
        _vanillaRoots = NonBlank(vanillaRoots);
        _fileExists = fileExists ?? File.Exists;
    }

    /// <summary>Die Herkünfte der Mod-Seite: <see cref="RowOrigin.Mod"/> und <see cref="RowOrigin.Both"/>. Einzige Definition; Zusammenfassung, Berichte und Ranglisten benutzen sie.</summary>
    public static readonly IReadOnlySet<RowOrigin> ModSide = new HashSet<RowOrigin> { RowOrigin.Mod, RowOrigin.Both };

    /// <summary>True für <see cref="RowOrigin.Mod"/> und <see cref="RowOrigin.Both"/>.</summary>
    public static bool IsModSide(RowOrigin o) => ModSide.Contains(o);

    /// <summary>Herkunft des Dump-Pfads (mit <c>/</c>, ohne führenden Schrägstrich); je Pfad zwischengespeichert.</summary>
    public RowOrigin Classify(string dumpPath)
    {
        if (_cache.TryGetValue(dumpPath, out var cached))
        {
            return cached;
        }

        var relative = dumpPath.Replace('/', Path.DirectorySeparatorChar);
        RowOrigin origin;
        if (ContainsAny(_activeModRoots, relative))
        {
            origin = ContainsAny(_vanillaRoots, relative) ? RowOrigin.Both : RowOrigin.Mod;
        }
        else if (ContainsAny(_otherModRoots, relative))
        {
            origin = RowOrigin.OtherMod;
        }
        else
        {
            origin = ContainsAny(_vanillaRoots, relative) ? RowOrigin.Vanilla : RowOrigin.Unknown;
        }

        _cache[dumpPath] = origin;
        return origin;
    }

    // Eine leere Wurzel würde über Path.Combine das aktuelle Arbeitsverzeichnis absuchen und falsche Treffer liefern.
    private static IReadOnlyList<string> NonBlank(IEnumerable<string> roots) => roots.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();

    private bool ContainsAny(IReadOnlyList<string> roots, string relative)
    {
        foreach (var root in roots)
        {
            foreach (var phase in SourceResolver.Phases)
            {
                if (_fileExists(Path.Combine(root, phase, relative)))
                {
                    return true;
                }
            }

            if (_fileExists(Path.Combine(root, relative)))
            {
                return true;
            }
        }

        return false;
    }
}
