namespace ParadoxModTool.SteamIntegration;

/// <summary>Findet die <c>steam_api64.dll</c> des Spiels: <c>appmanifest_&lt;AppID&gt;.acf</c> nennt den Ordner (<c>installdir</c>), die DLL liegt in <c>binaries</c> oder im Spielordner.</summary>
public static class SteamworksLocator
{
    public const string DllName = "steam_api64.dll";

    public static string? Find(IEnumerable<string> libraryFolders, string appId)
    {
        foreach (var library in libraryFolders)
        {
            var manifest = Path.Combine(library, "steamapps", $"appmanifest_{appId}.acf");
            if (!File.Exists(manifest))
            {
                continue;
            }

            string? installDir;
            try
            {
                installDir = VdfNode.Parse(File.ReadAllText(manifest))["AppState"]?["installdir"]?.Value;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(installDir) || installDir.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || installDir.Contains(".."))
            {
                continue;
            }

            var game = Path.Combine(library, "steamapps", "common", installDir);
            foreach (var candidate in new[] { Path.Combine(game, "binaries", DllName), Path.Combine(game, DllName) })
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
