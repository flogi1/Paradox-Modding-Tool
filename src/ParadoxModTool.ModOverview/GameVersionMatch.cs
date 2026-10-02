namespace ParadoxModTool.ModOverview;

/// <summary>Ob eine Mod laut ihrer Metadaten zur installierten Spielversion passt.</summary>
public enum VersionSupport
{
    /// <summary>Eine der beiden Versionen fehlt oder ist nicht lesbar: kein Hinweis.</summary>
    Unknown,
    Supported,
    Outdated,
}

/// <summary>
/// Vergleicht die unterstützte Spielversion einer Mod (z. B. <c>1.37.*</c>) mit der installierten (z. B. <c>v1.37.5.0</c>). Verglichen werden nur so
/// viele Stellen, wie die unterstützte Version hat; <c>*</c> passt auf alles. Ein Präfix wie <c>v</c> oder <c>EU4 v</c> zählt nicht.
/// </summary>
public static class GameVersionMatch
{
    public static VersionSupport Check(string? supported, string? installed)
    {
        var wanted = Segments(supported);
        var actual = Segments(installed);
        if (wanted is null || actual is null || wanted.Any(s => s != "*" && !s.All(char.IsAsciiDigit)))
        {
            return VersionSupport.Unknown;
        }

        for (var i = 0; i < wanted.Length; i++)
        {
            if (wanted[i] == "*")
            {
                continue;
            }

            if (i >= actual.Length || !int.TryParse(wanted[i], out var expected) || !int.TryParse(actual[i], out var found) || expected != found)
            {
                return VersionSupport.Outdated;
            }
        }

        return VersionSupport.Supported;
    }

    /// <summary>Die Stellen ab der ersten Ziffer, am Punkt getrennt; <see langword="null"/>, wenn keine Ziffer vorkommt.</summary>
    private static string[]? Segments(string? version)
    {
        var text = version?.Trim() ?? string.Empty;
        var start = text.IndexOfAny("0123456789".ToCharArray());
        if (start < 0)
        {
            return null;
        }

        var segments = text[start..].Split('.', StringSplitOptions.TrimEntries);
        return segments.Any(s => s.Length == 0) ? null : segments;
    }
}
