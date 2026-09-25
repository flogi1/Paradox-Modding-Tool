using System.IO;

namespace ParadoxModTool.Notes;

/// <summary>
/// Wie Mods zu Projekten gehören, geliefert von der App aus dem Mod-Register des Spiels (die Bibliothek kennt das Register nicht).
/// <c>rootOf</c>: Mod → Id ihres Hauptmods, <see langword="null"/> für Mods, die das Register dieses Spiels nicht kennt.
/// <c>foldersOf</c>: Mod → ihre Ordner. <c>membersOf</c>: Hauptmod → alle Mods des Projekts, Hauptmod zuerst.
/// </summary>
public sealed class BugProjects
{
    private readonly Func<string, string?> _rootOf;
    private readonly Func<string, IReadOnlyList<string>> _foldersOf;
    private readonly Func<string, IReadOnlyList<string>> _membersOf;

    public BugProjects(Func<string, string?> rootOf, Func<string, IReadOnlyList<string>> foldersOf, Func<string, IReadOnlyList<string>> membersOf)
    {
        _rootOf = rootOf;
        _foldersOf = foldersOf;
        _membersOf = membersOf;
    }

    /// <summary>Ohne Mod-Register: keine Mod ist bekannt, es gibt nur Meldungen ohne Projekt und volle Pfade.</summary>
    public static BugProjects None { get; } = new(_ => null, _ => Array.Empty<string>(), _ => Array.Empty<string>());

    public string? RootOf(string? modId) => modId is null ? null : _rootOf(modId);

    /// <summary>
    /// Vorhandene Dateien zu einem Bezug. Bei einem Mod-Schlüssel wird der relative Pfad in den Ordnern gesucht: zuerst
    /// <paramref name="preferredModId"/> (falls im selben Projekt), dann die Mod des Schlüssels, dann alle Mods des Projekts.
    /// </summary>
    public IReadOnlyList<string> FilesFor(BugLink link, string? preferredModId)
    {
        if (!NoteKeys.TryParseModKey(link.Key, out var modId, out var relative))
        {
            return File.Exists(link.Key) ? new[] { link.Key } : Array.Empty<string>();
        }

        var root = RootOf(modId);
        var order = new List<string>();
        if (preferredModId is not null && root is not null && RootOf(preferredModId) == root)
        {
            order.Add(preferredModId);
        }

        order.Add(modId);
        if (root is not null)
        {
            order.AddRange(_membersOf(root));
        }

        var relativePath = relative.Replace('/', Path.DirectorySeparatorChar);
        return order
            .Distinct(StringComparer.Ordinal)
            .SelectMany(id => _foldersOf(id))
            .Select(folder => Path.Combine(folder, relativePath))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
