using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ParadoxModTool.GitIntegration;

public sealed record GhDownload(string Version, string ZipName, string ZipUrl, string ChecksumsUrl);

/// <summary>Wählt das Windows-Paket von <c>gh</c> aus der GitHub-API, prüft die SHA-256-Summe und packt es aus. Den Download selbst macht die App nach Rückfrage beim Nutzer.</summary>
public static class GhInstaller
{
    public const string LatestReleaseApi = "https://api.github.com/repos/cli/cli/releases/latest";

    public const string ExecutableName = "gh.exe";

    /// <summary>Der Standardordner im Programmordner des Nutzers: <c>%APPDATA%\ParadoxModTool\tools\gh</c>.</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParadoxModTool", "tools", "gh");

    public static string DefaultExecutable => Path.Combine(DefaultFolder, "bin", ExecutableName);

    public static GhDownload SelectDownload(string apiJson)
    {
        try
        {
            using var document = JsonDocument.Parse(apiJson);
            var root = document.RootElement;
            var version = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? string.Empty;
            string? zipName = null;
            string? zipUrl = null;
            string? sumsUrl = null;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                var url = asset.GetProperty("browser_download_url").GetString();
                if (name.EndsWith("_windows_amd64.zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipName = name;
                    zipUrl = url;
                }
                else if (name.EndsWith("_checksums.txt", StringComparison.OrdinalIgnoreCase))
                {
                    sumsUrl = url;
                }
            }

            if (zipName is null || zipUrl is null || sumsUrl is null || version.Length == 0)
            {
                throw new InvalidDataException(GitTexts.InstallNoPackage);
            }

            return new GhDownload(version, zipName, zipUrl, sumsUrl);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException(GitTexts.InstallUnreadable, ex);
        }
    }

    public static string ExpectedSha256(string checksumsText, string fileName)
    {
        foreach (var line in checksumsText.Replace("\r\n", "\n").Split('\n'))
        {
            var parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && string.Equals(parts[1].TrimStart('*'), fileName, StringComparison.OrdinalIgnoreCase))
            {
                return parts[0].ToLowerInvariant();
            }
        }

        throw new InvalidDataException(GitTexts.InstallNoChecksum(fileName));
    }

    /// <summary>Prüft die Summe, entpackt in einen Nachbarordner, verlangt <c>bin\gh.exe</c> und legt die Dateien dann in <paramref name="folder"/> ab. Scheitert etwas, bleibt nichts zurück.</summary>
    public static string Install(byte[] zip, string expectedSha256, string folder)
    {
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(zip)), expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(GitTexts.InstallChecksumMismatch);
        }

        var target = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(target) ?? throw new ArgumentException(GitTexts.InstallNoParent, nameof(folder));
        Directory.CreateDirectory(parent);
        var temp = Path.Combine(parent, Path.GetFileName(target) + ".partial-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            try
            {
                using var stream = new MemoryStream(zip);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                archive.ExtractToDirectory(temp);
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException(GitTexts.InstallNotZip);
            }

            if (!File.Exists(Path.Combine(temp, "bin", ExecutableName)))
            {
                throw new InvalidDataException(GitTexts.InstallNoExe);
            }

            foreach (var file in Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(target, Path.GetRelativePath(temp, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Move(file, destination, overwrite: true);
            }

            return Path.Combine(target, "bin", ExecutableName);
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                try
                {
                    Directory.Delete(temp, recursive: true);
                }
                catch (IOException)
                {
                    // Aufräumen ist nur ein Versuch.
                }
            }
        }
    }
}
