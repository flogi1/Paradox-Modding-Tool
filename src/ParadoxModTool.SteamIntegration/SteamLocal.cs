using System.Text;

namespace ParadoxModTool.SteamIntegration;

/// <summary>
/// Ein Knoten einer VDF-Datei (Valves Textformat, z. B. <c>loginusers.vdf</c>, <c>libraryfolders.vdf</c>): entweder ein Wert oder ein Objekt mit benannten Kindern. Schlüssel
/// unterscheiden nicht zwischen Groß- und Kleinschreibung. Der Leser ist tolerant: ein abgeschnittener Text ergibt, was bis dahin gelesen wurde.
/// </summary>
public sealed class VdfNode
{
    private readonly Dictionary<string, VdfNode> _children = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = new();

    private VdfNode()
    {
    }

    private VdfNode(string value)
    {
        Value = value;
    }

    /// <summary>Der Wert eines Werte-Knotens; <see langword="null"/> bei einem Objekt.</summary>
    public string? Value { get; }

    /// <summary>Die Schlüssel der Kinder in der Reihenfolge der Datei.</summary>
    public IReadOnlyList<string> Keys => _order;

    public VdfNode? this[string key] => _children.TryGetValue(key, out var node) ? node : null;

    public static VdfNode Parse(string text)
    {
        var index = 0;
        return ParseObject(text, ref index, isRoot: true);
    }

    private void Add(string key, VdfNode node)
    {
        if (!_children.ContainsKey(key))
        {
            _order.Add(key);
        }

        _children[key] = node;
    }

    private static VdfNode ParseObject(string text, ref int index, bool isRoot)
    {
        var node = new VdfNode();
        while (true)
        {
            SkipSpace(text, ref index);
            if (index >= text.Length)
            {
                break;
            }

            if (text[index] == '}')
            {
                index++;
                if (isRoot)
                {
                    continue;
                }

                break;
            }

            if (ReadToken(text, ref index) is not { } key)
            {
                break;
            }

            SkipSpace(text, ref index);
            if (index >= text.Length)
            {
                break;
            }

            if (text[index] == '{')
            {
                index++;
                node.Add(key, ParseObject(text, ref index, isRoot: false));
            }
            else if (ReadToken(text, ref index) is { } value)
            {
                node.Add(key, new VdfNode(value));
            }
            else
            {
                break;
            }
        }

        return node;
    }

    private static void SkipSpace(string text, ref int index)
    {
        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
            }
            else if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }
            }
            else
            {
                break;
            }
        }
    }

    private static string? ReadToken(string text, ref int index)
    {
        if (index >= text.Length || text[index] is '{' or '}')
        {
            return null;
        }

        var token = new StringBuilder();
        if (text[index] == '"')
        {
            index++;
            while (index < text.Length && text[index] != '"')
            {
                if (text[index] == '\\' && index + 1 < text.Length)
                {
                    index++;
                    token.Append(text[index] switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        var other => other,
                    });
                }
                else
                {
                    token.Append(text[index]);
                }

                index++;
            }

            index++;
            return token.ToString();
        }

        while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] is not ('{' or '}' or '"'))
        {
            token.Append(text[index]);
            index++;
        }

        return token.ToString();
    }
}

/// <summary>Ein bei Steam auf diesem Rechner bekanntes Konto (aus <c>config\loginusers.vdf</c>). Die Datei enthält kein Passwort und keine Token; das Programm liest nur diese Felder.</summary>
public sealed record SteamLoginUser(string SteamId64, string AccountName, string PersonaName, long Timestamp, bool MostRecent);

/// <summary>Liest <c>loginusers.vdf</c> des Steam-Clients.</summary>
public static class SteamLoginUsers
{
    public static IReadOnlyList<SteamLoginUser> Parse(string vdfText)
    {
        var result = new List<SteamLoginUser>();
        if (VdfNode.Parse(vdfText)["users"] is not { } users)
        {
            return result;
        }

        foreach (var id in users.Keys)
        {
            var user = users[id];
            var account = user?["AccountName"]?.Value;
            if (user is null || string.IsNullOrWhiteSpace(account) || id.Length == 0 || !id.All(char.IsAsciiDigit))
            {
                continue;
            }

            long.TryParse(user["Timestamp"]?.Value, out var timestamp);
            result.Add(new SteamLoginUser(id, account, user["PersonaName"]?.Value ?? account, timestamp, user["MostRecent"]?.Value == "1"));
        }

        return result;
    }

    /// <summary>Das Konto mit dem gegebenen Kontonamen, sonst das zuletzt angemeldete (Flag <c>MostRecent</c>, sonst der neueste Zeitstempel); <see langword="null"/> ohne Konten.</summary>
    public static SteamLoginUser? Pick(IReadOnlyList<SteamLoginUser> users, string? accountName)
    {
        if (!string.IsNullOrWhiteSpace(accountName)
            && users.FirstOrDefault(u => string.Equals(u.AccountName, accountName.Trim(), StringComparison.OrdinalIgnoreCase)) is { } named)
        {
            return named;
        }

        return users.FirstOrDefault(u => u.MostRecent) ?? users.OrderByDescending(u => u.Timestamp).FirstOrDefault();
    }
}

/// <summary>Findet den Steam-Ordner und seine Bibliotheken auf diesem Rechner.</summary>
public static class SteamFolders
{
    /// <summary>Der Steam-Ordner aus der Registry (<c>HKCU\Software\Valve\Steam</c>), sonst der Standardordner; <see langword="null"/>, wenn keiner existiert.</summary>
    public static string? Find()
    {
        var candidates = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string path && path.Length > 0)
                {
                    candidates.Add(path);
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // Ohne Registry-Zugriff bleiben die Standardordner.
            }
        }

        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
        return candidates.Select(Normalize).FirstOrDefault(Directory.Exists);
    }

    public static string LoginUsersFile(string steamFolder) => Path.Combine(steamFolder, "config", "loginusers.vdf");

    /// <summary>Der Steam-Ordner selbst und alle Bibliotheken aus <c>steamapps\libraryfolders.vdf</c>; nur vorhandene Ordner, ohne Doppelte.</summary>
    public static IReadOnlyList<string> LibraryFolders(string steamFolder)
    {
        var folders = new List<string> { Normalize(steamFolder) };
        var file = Path.Combine(steamFolder, "steamapps", "libraryfolders.vdf");
        if (File.Exists(file))
        {
            try
            {
                var root = VdfNode.Parse(File.ReadAllText(file));
                if (root["libraryfolders"] is { } libraries)
                {
                    foreach (var key in libraries.Keys)
                    {
                        if (libraries[key]?["path"]?.Value is { Length: > 0 } path)
                        {
                            folders.Add(Normalize(path));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ohne lesbare Liste bleibt der Steam-Ordner selbst.
            }
        }

        return folders.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists).ToList();
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
