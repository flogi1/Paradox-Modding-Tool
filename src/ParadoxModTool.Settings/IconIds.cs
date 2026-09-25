using System.Text.RegularExpressions;

namespace ParadoxModTool.Settings;

/// <summary>Die Tab-Symbole mit wählbarer Farbe; die Namen entsprechen ParadoxModTool.Editor.TabIconKind.</summary>
public static class IconIds
{
    public const string Note = "Note";
    public const string Bookmark = "Bookmark";
    public const string Bug = "Bug";
    public const string Vanilla = "Vanilla";
    public const string Mod = "Mod";

    public static IReadOnlyList<string> All { get; } = new[] { Note, Bookmark, Bug, Vanilla, Mod };

    private static readonly Regex ColorPattern = new("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.CultureInvariant);

    public static bool IsKnown(string id) => All.Contains(id, StringComparer.Ordinal);

    /// <summary>Farbe als <c>#RRGGBB</c> oder <c>#AARRGGBB</c>.</summary>
    public static bool IsValidColor(string? hex) => hex is not null && ColorPattern.IsMatch(hex);
}
