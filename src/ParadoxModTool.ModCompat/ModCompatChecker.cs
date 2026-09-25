using ParadoxModTool.GameProfiles;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>
/// Mod-Kompatibilitäts-Checker (Abschnitt 8): vergleicht zwei Mods gegen dieselbe
/// Vanilla-Basis. Baut bewusst auf den bereits vorhandenen Bausteinen aus
/// <c>ParadoxModTool.VanillaDiff</c> auf (Abschnitt 7) — der eigentliche 3-Wege-Vergleich
/// zwischen einer gemeinsamen Basis und zwei divergierenden Fassungen ist genau dasselbe
/// Problem wie beim Vanilla-Update-Merge (dort: Vanilla-alt/Vanilla-neu/Mod, hier:
/// Vanilla/Mod A/Mod B), nur mit anderer Bedeutung der beiden divergierenden Seiten.
/// </summary>
public static class ModCompatChecker
{
    /// <summary>
    /// Abschnitt 8, Punkt 1 ("Übersicht"): Schnittmenge der Dateien, die sowohl Mod A als
    /// auch Mod B gegenüber Vanilla überschreiben (gemäß Lade-Regel des Spielprofils,
    /// siehe <see cref="VanillaFileTracker"/>).
    /// </summary>
    public static IReadOnlyList<CommonlyModifiedFile> FindCommonlyModifiedFiles(
        string vanillaRoot, string modARoot, string modBRoot, GameProfile profile)
    {
        var overriddenByA = VanillaFileTracker.FindOverriddenFiles(vanillaRoot, modARoot, profile)
            .ToDictionary(f => f.RelativePath, StringComparer.Ordinal);
        var overriddenByB = VanillaFileTracker.FindOverriddenFiles(vanillaRoot, modBRoot, profile);

        var result = new List<CommonlyModifiedFile>();

        foreach (var fileB in overriddenByB)
        {
            if (overriddenByA.TryGetValue(fileB.RelativePath, out var fileA))
            {
                result.Add(new CommonlyModifiedFile
                {
                    RelativePath = fileB.RelativePath,
                    VanillaFullPath = fileB.VanillaFullPath,
                    ModAFullPath = fileA.ModFullPath,
                    ModBFullPath = fileB.ModFullPath
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Abschnitt 8, Punkt 2 ("Paralleler Diff"): zeigt für eine gemeinsam betroffene Datei,
    /// wie stark jeder der beiden Mods von Vanilla abweicht.
    /// </summary>
    public static ParallelDiffResult ComputeParallelDiff(
        ScriptBlock vanilla, ScriptBlock modA, ScriptBlock modB, DiffOptions? options = null)
    {
        return new ParallelDiffResult
        {
            ModADiff = ScriptDiffer.Diff(vanilla, modA, options),
            ModBDiff = ScriptDiffer.Diff(vanilla, modB, options)
        };
    }

    /// <summary>
    /// Abschnitt 8, Stufe 1 (MVP): führt Mod A und Mod B automatisch zusammen, wo sich ihre
    /// Änderungen gegenüber Vanilla nicht überlappen, und meldet überlappende Stellen als
    /// <see cref="ModCompatConflict"/> mit BEIDEN Werten zur manuellen Entscheidung — statt
    /// wie bei <see cref="MergeEngine"/> im Vanilla-Update-Fall eine Seite automatisch
    /// gewinnen zu lassen. Der eigentliche Abgleich wird an <see cref="MergeEngine.Merge"/>
    /// delegiert (Basis = Vanilla, "linke" Seite = Mod A, "rechte" Seite = Mod B).
    /// </summary>
    public static ModCompatMergeResult AutoMergeAgainstVanilla(ScriptBlock vanilla, ScriptBlock modA, ScriptBlock modB)
    {
        var raw = MergeEngine.Merge(vanilla, modA, modB);

        var conflicts = raw.Conflicts
            .Select(c => new ModCompatConflict
            {
                Path = c.Path,
                Description = BuildConflictDescription(c.Path, c.LeftValue, c.RightValue),
                ModAValue = c.LeftValue,
                ModBValue = c.RightValue
            })
            .ToList();

        return new ModCompatMergeResult
        {
            MergedRoot = raw.MergedRoot,
            Conflicts = conflicts
        };
    }

    private static string BuildConflictDescription(string path, ScriptValue? modAValue, ScriptValue? modBValue)
    {
        var a = DescribeValue(modAValue);
        var b = DescribeValue(modBValue);
        return $"\"{path}\" wird von Mod A und Mod B unterschiedlich gesetzt: Mod A = {a}, Mod B = {b}. Bitte manuell entscheiden.";
    }

    private static string DescribeValue(ScriptValue? value) => value switch
    {
        null => "(entfernt)",
        ScriptScalar scalar => scalar.ToString(),
        ScriptBlock => "{ ... }",
        _ => "?"
    };
}
