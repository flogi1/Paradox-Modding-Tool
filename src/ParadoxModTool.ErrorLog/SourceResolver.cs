using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Eine Mod für Zuordnung und Symbolindex. <c>LoadOrder</c> ist die Position im Playset (später geladen = höher = gewinnt);
/// -1 bedeutet: installiert, aber nicht im Playset.
/// </summary>
public sealed record ModSource(string Name, string Directory, bool IsOwn, int LoadOrder)
{
    public bool InPlayset => LoadOrder >= 0;
}

/// <summary>Aufgelöste Datei. <c>Mod</c> null = Vanilla. <c>ChangedSinceRun</c>: Datei wurde nach dem Spiellauf geändert, die Zeilennummer aus dem Log kann veraltet sein.</summary>
public sealed record ResolvedSource(string FullPath, ModSource? Mod, bool ChangedSinceRun)
{
    public bool IsVanilla => Mod is null;

    public bool IsOwn => Mod?.IsOwn == true;
}

/// <summary>
/// Findet zu einem Pfad aus dem Log (ohne Phasenordner, ohne Mod) die Datei, die im Spiel gewonnen hat: Mods des Playsets in umgekehrter
/// Ladereihenfolge, dann Vanilla (game, jomini, clausewitz). In jeder Wurzel werden die Phasenordner und danach der Pfad ohne Phase versucht.
/// </summary>
public sealed class SourceResolver
{
    public static readonly IReadOnlyList<string> Phases = new[] { "in_game", "main_menu", "loading_screen" };

    private readonly IReadOnlyList<ModSource> _playset;
    private readonly IReadOnlyList<string> _vanillaRoots;
    private readonly DateTime _runTimeUtc;
    private readonly Func<string, bool> _exists;
    private readonly Func<string, DateTime> _lastWriteUtc;

    public SourceResolver(
        IEnumerable<ModSource> mods,
        IReadOnlyList<string> vanillaRoots,
        DateTime runTimeUtc,
        Func<string, bool>? fileExists = null,
        Func<string, DateTime>? lastWriteUtc = null)
    {
        _playset = mods.Where(m => m.InPlayset).OrderByDescending(m => m.LoadOrder).ToList();
        _vanillaRoots = vanillaRoots;
        _runTimeUtc = runTimeUtc;
        _exists = fileExists ?? File.Exists;
        _lastWriteUtc = lastWriteUtc ?? File.GetLastWriteTimeUtc;
    }

    public static IReadOnlyList<string> VanillaRootsFor(string installDirectory) =>
        new[] { "game", "jomini", "clausewitz" }.Select(folder => Path.Combine(installDirectory, folder)).ToList();

    public ResolvedSource? Resolve(string logPath)
    {
        var relative = logPath.Replace('\\', '/').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        foreach (var mod in _playset)
        {
            if (Find(mod.Directory, relative) is { } path)
            {
                return new ResolvedSource(path, mod, _lastWriteUtc(path) > _runTimeUtc);
            }
        }

        foreach (var root in _vanillaRoots)
        {
            if (Find(root, relative) is { } path)
            {
                return new ResolvedSource(path, null, _lastWriteUtc(path) > _runTimeUtc);
            }
        }

        return null;
    }

    /// <summary>1-basierte Zeile mit dem Namen als ganzes Wort, die der Log-Zeile am nächsten liegt; null, wenn der Name fehlt.</summary>
    public static int? FindNearestLine(IReadOnlyList<string> fileLines, int loggedLine, string name)
    {
        var pattern = new Regex(@"(?<![A-Za-z0-9_])" + Regex.Escape(name) + @"(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);
        int? best = null;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < fileLines.Count; i++)
        {
            if (!pattern.IsMatch(fileLines[i]))
            {
                continue;
            }

            var distance = Math.Abs(i + 1 - loggedLine);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i + 1;
            }
        }

        return best;
    }

    private string? Find(string root, string relative)
    {
        foreach (var phase in Phases)
        {
            var candidate = Path.Combine(root, phase, relative);
            if (_exists(candidate))
            {
                return candidate;
            }
        }

        var plain = Path.Combine(root, relative);
        return _exists(plain) ? plain : null;
    }
}
