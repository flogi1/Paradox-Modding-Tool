using System.Text.RegularExpressions;

namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Vergleicht Spielversionen nach Zahlen statt alphabetisch ("1.9" &lt; "1.10"). Gelesen wird die erste Folge aus Ziffern und Punkten; ein
/// <c>v</c> davor oder ein Codename danach ("v1.37.5.0 Inca", im Historie-Ordner "v1.37.5.0-Inca") zählen nicht. Fehlende Stellen zählen als 0.
/// </summary>
public sealed class GameVersionComparer : IComparer<string>
{
    public static readonly GameVersionComparer Instance = new();

    private static readonly Regex NumberRun = new(@"\d+(?:\.\d+)*", RegexOptions.CultureInvariant);
    private static readonly Regex PatternRun = new(@"[\d*]+(?:\.[\d*]+)*", RegexOptions.CultureInvariant);

    /// <summary>Die erste Ziffern-Punkt-Folge, z. B. "1.37.5.0" aus "v1.37.5.0-Inca"; <see langword="null"/> ohne Ziffern.</summary>
    public static string? Normalize(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var match = NumberRun.Match(version);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Passt die Version zum Muster (<c>supported_version</c>, z. B. "1.37.*" oder "1.*.*")? <c>*</c> passt auf diese und alle folgenden Stellen;
    /// ein leeres Muster passt immer.
    /// </summary>
    public static bool Matches(string version, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        var run = PatternRun.Match(pattern);
        if (!run.Success || Normalize(version) is not { } normalized)
        {
            return false;
        }

        var have = Segments(normalized);
        var want = run.Value.Split('.');
        for (var i = 0; i < want.Length; i++)
        {
            if (want[i].Contains('*'))
            {
                return true;
            }

            if (!int.TryParse(want[i], out var number) || At(have, i) != number)
            {
                return false;
            }
        }

        return have.Skip(want.Length).All(n => n == 0);
    }

    public int Compare(string? x, string? y)
    {
        var a = Normalize(x);
        var b = Normalize(y);
        if (a is null || b is null)
        {
            return (a is null ? 0 : 1) - (b is null ? 0 : 1);
        }

        var left = Segments(a);
        var right = Segments(b);
        for (var i = 0; i < Math.Max(left.Count, right.Count); i++)
        {
            var diff = At(left, i).CompareTo(At(right, i));
            if (diff != 0)
            {
                return diff;
            }
        }

        return 0;
    }

    private static List<long> Segments(string normalized) =>
        normalized.Split('.').Select(s => long.TryParse(s, out var n) ? n : 0).ToList();

    private static long At(IReadOnlyList<long> segments, int index) => index < segments.Count ? segments[index] : 0;
}
