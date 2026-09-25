using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.VanillaDiff;

/// <summary>Eine Vanilla-Datei, die von einer gleichnamigen Datei im Mod-Ordner überschrieben wird.</summary>
public sealed class OverriddenFile
{
    /// <summary>Pfad relativ zur Spiel- bzw. Mod-Wurzel (identisch auf beiden Seiten), mit "/" als Trenner.</summary>
    public required string RelativePath { get; init; }

    public required string VanillaFullPath { get; init; }
    public required string ModFullPath { get; init; }
}

/// <summary>
/// Erkennt, welche Vanilla-Dateien der aktuelle Mod direkt überschreibt (Abschnitt 7,
/// erster Punkt: "Bei Aktivierung erkennt das Tool automatisch, welche Vanilla-Dateien der
/// aktuelle Mod direkt überschreibt"). Der Abgleich richtet sich nach der Lade-Regel aus dem
/// jeweiligen Spielprofil (Abschnitt 4, <see cref="GameProfile.LoadOrderRule"/>).
/// </summary>
public static class VanillaFileTracker
{
    /// <summary>
    /// Für EU5 bestätigte Regel: Eine Mod-Datei überschreibt die Vanilla-Datei mit exakt
    /// demselben relativen Pfad (Ordnerkategorie + Dateiname) vollständig; bei mehreren Mods
    /// gewinnt bei Namensgleichheit alphabetisch der zuletzt sortierte Dateiname — das betrifft
    /// aber den Mod-Kompatibilitäts-Checker (Abschnitt 8), nicht diesen einfachen 1:1-Abgleich
    /// zwischen einem Mod und Vanilla.
    /// </summary>
    public const string AlphabeticalFilenameWinsRule = "alphabetical-filename-wins";

    /// <summary>
    /// Ermittelt alle Dateien im Mod-Ordner, zu denen es eine gleichnamige Vanilla-Datei
    /// (relativ zur Spielwurzel) gibt. Wirft <see cref="NotSupportedException"/>, wenn das
    /// Spielprofil eine Lade-Regel verwendet, die hier noch nicht implementiert ist — bewusst
    /// explizit statt eines stillschweigend falschen Ergebnisses.
    /// </summary>
    public static IReadOnlyList<OverriddenFile> FindOverriddenFiles(string vanillaRoot, string modRoot, GameProfile profile)
    {
        if (!string.Equals(profile.LoadOrderRule, AlphabeticalFilenameWinsRule, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Lade-Regel \"{profile.LoadOrderRule}\" von Spielprofil \"{profile.Id}\" wird vom Vanilla-Diff-Modul noch nicht unterstützt.");
        }

        var result = new List<OverriddenFile>();

        if (!Directory.Exists(modRoot))
        {
            return result;
        }

        foreach (var modFile in Directory.EnumerateFiles(modRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(modRoot, modFile).Replace('\\', '/');

            if (relative.StartsWith(".", StringComparison.Ordinal) || relative.Contains("/.", StringComparison.Ordinal))
            {
                // interne Tool-Ordner wie .pmt-versions/, .pmt-vanilla-history/, .git/ überspringen
                continue;
            }

            var vanillaCandidate = Path.Combine(vanillaRoot, relative.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(vanillaCandidate))
            {
                result.Add(new OverriddenFile
                {
                    RelativePath = relative,
                    VanillaFullPath = vanillaCandidate,
                    ModFullPath = modFile
                });
            }
        }

        return result;
    }
}
