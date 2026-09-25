namespace ParadoxModTool.SteamIntegration;

/// <summary>Sucht den Accessor einer Steamworks-Schnittstelle (<c>SteamAPI_SteamUGC_v016</c> usw.), dessen Versionsnummer je nach DLL wechselt.</summary>
public static class AccessorSearch
{
    /// <summary>Der Name mit der höchsten Version von <paramref name="highest"/> abwärts, für den <paramref name="exists"/> wahr ist; sonst <see langword="null"/>.</summary>
    public static string? Find(string prefix, Func<string, bool> exists, int highest = 30)
    {
        for (var version = highest; version >= 1; version--)
        {
            var name = $"{prefix}_v{version:000}";
            if (exists(name))
            {
                return name;
            }
        }

        return null;
    }
}
