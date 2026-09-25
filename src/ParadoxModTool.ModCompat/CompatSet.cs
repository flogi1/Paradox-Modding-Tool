namespace ParadoxModTool.ModCompat;

/// <summary>Woher ein Mitglied des Sets kommt.</summary>
public enum CompatOrigin
{
    /// <summary>Eine eigene Mod aus dem Mod-Register.</summary>
    Register,

    /// <summary>Eine installierte Workshop-Mod (nur lesend).</summary>
    Workshop,

    /// <summary>Ein beliebiger Ordner.</summary>
    Folder,
}

public sealed class CompatSetMember
{
    public string Folder { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public CompatOrigin Origin { get; set; } = CompatOrigin.Folder;

    /// <summary>ID im Mod-Register, nur bei <see cref="CompatOrigin.Register"/>.</summary>
    public string? ModId { get; set; }
}

/// <summary>
/// Ein benanntes Kompatibilitätsset: Mods in simulierter Ladereihenfolge (spätere Position gewinnt). Unabhängig von <c>playsets.json</c>,
/// das Spiel sieht das Set nie. Gespeichert wird es von <see cref="CompatSetStore"/>.
/// </summary>
public sealed class CompatSet
{
    public int Version { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    /// <summary>Freier Erklärtext für dieses Set, zum Beispiel wofür es gedacht ist. Leer, wenn keiner gesetzt wurde.</summary>
    public string Description { get; set; } = string.Empty;

    public List<CompatSetMember> Members { get; set; } = new();

    public static string NormalizeFolder(string folder) => Path.GetFullPath(folder.Trim()).TrimEnd('\\', '/');

    private static bool Same(string a, string b) => string.Equals(NormalizeFolder(a), NormalizeFolder(b), StringComparison.OrdinalIgnoreCase);

    public bool Contains(string folder) => Members.Any(m => Same(m.Folder, folder));

    /// <summary>Hängt das Mitglied hinten an (Ordner normalisiert). <see langword="false"/>, wenn der Ordner schon im Set steht.</summary>
    public bool Add(CompatSetMember member)
    {
        if (Contains(member.Folder))
        {
            return false;
        }

        member.Folder = NormalizeFolder(member.Folder);
        Members.Add(member);
        return true;
    }

    public bool Remove(string folder) => Members.RemoveAll(m => Same(m.Folder, folder)) > 0;

    /// <summary>Verschiebt das Mitglied um <paramref name="delta"/> Plätze. <see langword="false"/>, wenn es nicht existiert oder am Rand steht.</summary>
    public bool Move(string folder, int delta)
    {
        var index = Members.FindIndex(m => Same(m.Folder, folder));
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Members.Count || delta == 0)
        {
            return false;
        }

        var member = Members[index];
        Members.RemoveAt(index);
        Members.Insert(target, member);
        return true;
    }
}
