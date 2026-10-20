namespace ParadoxModTool.GameProfiles;

/// <summary>
/// Ladereihenfolge relativer Pfade: alphabetisch nach ASCII, die Dateien eines Ordners vor seinen Unterordnern. <see cref="Instance"/> vergleicht in
/// Kleinschreibung ("_" vor Buchstaben; Victoria-3-Wiki "Mod compatibility", für GUI gemessen 2026-10-11 mit PMT Order Probe C, siehe GameLoadOrderTests),
/// <see cref="CaseSensitive"/> mit Groß-/Kleinschreibung (Großbuchstaben vor "_" vor Kleinbuchstaben; Stellaris, gemessen 2026-10-16 mit PMT Order Probe A/B).
/// </summary>
public sealed class GameLoadOrder : IComparer<string>
{
    private readonly bool _caseSensitive;

    private GameLoadOrder(bool caseSensitive) => _caseSensitive = caseSensitive;

    public static GameLoadOrder Instance { get; } = new(caseSensitive: false);

    public static GameLoadOrder CaseSensitive { get; } = new(caseSensitive: true);

    /// <summary>Die Reihenfolge des Spiels (<see cref="GameProfile.LoadOrderCaseSensitive"/>).</summary>
    public static GameLoadOrder For(bool caseSensitive) => caseSensitive ? CaseSensitive : Instance;

    public int Compare(string? x, string? y)
    {
        var a = (x ?? string.Empty).Replace('\\', '/').Split('/');
        var b = (y ?? string.Empty).Replace('\\', '/').Split('/');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var aIsFile = i == a.Length - 1;
            var bIsFile = i == b.Length - 1;
            if (aIsFile != bIsFile)
            {
                return aIsFile ? -1 : 1;
            }

            // ASCII in Kleinschreibung: "_" (95) vor Buchstaben (gemessen 2026-10-11); OrdinalIgnoreCase faltet nach Großbuchstaben und sortiert "_" danach.
            var result = _caseSensitive
                ? string.CompareOrdinal(a[i], b[i])
                : string.CompareOrdinal(a[i].ToLowerInvariant(), b[i].ToLowerInvariant());
            if (result != 0)
            {
                return result;
            }
        }

        return a.Length.CompareTo(b.Length);
    }
}
