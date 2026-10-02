using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Scopes;

/// <summary>
/// Liest die Namen benannter Script-Values (Schlüssel der obersten Ebene in <c>common/script_values/*.txt</c>), etwa für die Wert-Vorschläge im Scope Finder.
/// Gesucht wird in denselben Ordnern wie beim Scope-Lernen (<see cref="ScopeRuleLearner.FindScriptFolders"/>).
/// </summary>
public static partial class ScriptValueNames
{
    public const string Folder = "script_values";

    /// <summary>Alle Script-Value-Namen unter den Wurzeln (<c>common</c> und <c>in_game/common</c>), ohne Doppel und ordinal sortiert.</summary>
    public static IReadOnlyList<string> Read(IEnumerable<string> roots) =>
        FromFiles(ScopeRuleLearner.EnumerateFiles(roots).Where(f => f.Folder == Folder).Select(f => f.Path));

    /// <summary>
    /// Die Schlüssel der obersten Ebene dieser Dateien, nur Namen aus Buchstaben, Ziffern und <c>_</c> (<c>@konstanten</c> fallen weg). Fehlende oder
    /// nicht lesbare Dateien werden übersprungen.
    /// </summary>
    public static IReadOnlyList<string> FromFiles(IEnumerable<string> paths)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var node in ScriptTree.Parse(text))
            {
                if (node.Key is { } key && ValidName().IsMatch(key))
                {
                    names.Add(key);
                }
            }
        }

        return names.Order(StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex("^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidName();
}
