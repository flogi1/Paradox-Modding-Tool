using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Patches;

/// <summary>Dateimuster mit <c>/</c>: <c>*</c> = beliebige Zeichen außer <c>/</c>, <c>**/</c> = null oder mehr Ordner; Groß-/Kleinschreibung egal.</summary>
public static class GlobMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> Cache = new(StringComparer.Ordinal);

    /// <summary>Mehrere Muster werden mit <c>;</c> getrennt; es genügt, wenn eines passt.</summary>
    public static bool IsMatch(string glob, string relativePath) =>
        Parts(glob).Any(part => IsMatchOne(part, relativePath));

    /// <summary>
    /// Relative Pfade (mit <c>/</c>) unter <paramref name="root"/>, die zum Muster passen, ordinal sortiert; Ordner mit Punkt am Anfang werden übersprungen.
    /// Mehrere Muster (mit <c>;</c> getrennt) ergeben die Vereinigung ohne Doppelte.
    /// </summary>
    public static IEnumerable<string> Enumerate(string root, string glob)
    {
        var parts = Parts(glob);
        return parts.Count == 1
            ? EnumerateOne(root, parts[0])
            : parts.SelectMany(part => EnumerateOne(root, part)).Distinct(StringComparer.Ordinal).OrderBy(rel => rel, StringComparer.Ordinal).ToList();
    }

    private static List<string> Parts(string glob)
    {
        var parts = glob.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        return parts.Count == 0 ? new List<string> { string.Empty } : parts;
    }

    private static bool IsMatchOne(string glob, string relativePath) =>
        Cache.GetOrAdd(glob.Replace('\\', '/'), ToRegex).IsMatch(relativePath.Replace('\\', '/'));

    private static IEnumerable<string> EnumerateOne(string root, string glob)
    {
        glob = glob.Replace('\\', '/');
        var wildcard = glob.IndexOf('*');
        var prefixEnd = (wildcard < 0 ? glob.Length : glob.LastIndexOf('/', wildcard)) ;
        var prefix = prefixEnd > 0 ? glob[..prefixEnd] : string.Empty;
        var start = prefix.Length > 0 ? Path.Combine(root, prefix.Replace('/', Path.DirectorySeparatorChar)) : root;
        if (wildcard < 0)
        {
            var single = Path.Combine(root, glob.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(single) && !glob.StartsWith('.') && !glob.Contains("/.", StringComparison.Ordinal) ? new[] { glob } : Array.Empty<string>();
        }

        if (!Directory.Exists(start))
        {
            return Array.Empty<string>();
        }

        var deep = glob.IndexOf('/', wildcard) >= 0 || glob.Contains("**", StringComparison.Ordinal);
        return Directory.EnumerateFiles(start, "*", deep ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(rel => !rel.StartsWith('.') && !rel.Contains("/.", StringComparison.Ordinal))
            .Where(rel => IsMatch(glob, rel))
            .OrderBy(rel => rel, StringComparer.Ordinal)
            .ToList();
    }

    private static Regex ToRegex(string glob)
    {
        var pattern = new StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            if (glob[i] == '*' && i + 2 < glob.Length && glob[i + 1] == '*' && glob[i + 2] == '/')
            {
                pattern.Append("(?:[^/]+/)*");
                i += 2;
            }
            else if (glob[i] == '*')
            {
                pattern.Append("[^/]*");
            }
            else
            {
                pattern.Append(Regex.Escape(glob[i].ToString()));
            }
        }

        return new Regex(pattern.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
