namespace ParadoxModTool.Mods;

/// <summary>Arten von Bug-Quellen (stehen in mods.json, daher nie umbenennen).</summary>
public static class BugSourceKinds
{
    public const string GitHub = "github";
    public const string Discord = "discord";
}

/// <summary>
/// Eine verknüpfte Quelle für Bugreports am Hauptmod eines Projekts. GitHub: <see cref="Key"/> = <c>owner/name</c>, <see cref="Labels"/> = Label-Namen
/// als Filter. Discord: <see cref="Key"/> = <c>guildId/channelId</c> eines Forum-Channels, <see cref="Labels"/> = Tag-IDs als Filter, dazu die drei
/// Status-Tags. Filter: leer = alles, sonst mindestens einer.
/// </summary>
public sealed class BugSourceEntry
{
    public string Kind { get; set; } = BugSourceKinds.GitHub;

    public string Key { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public List<string> Labels { get; set; } = new();

    /// <summary>Discord: Anzeigename ("Server / #forum").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Discord: Tag-ID für Offen.</summary>
    public string OpenTagId { get; set; } = string.Empty;

    /// <summary>Discord: Tag-ID für Erledigt.</summary>
    public string DoneTagId { get; set; } = string.Empty;

    /// <summary>Discord: Tag-ID für Verworfen.</summary>
    public string RejectedTagId { get; set; } = string.Empty;

    /// <summary>Discord: erledigte und verworfene Beiträge nach dem Statuswechsel archivieren.</summary>
    public bool ArchiveClosed { get; set; } = true;

    public BugSourceEntry Clone() => new()
    {
        Kind = Kind,
        Key = Key,
        Enabled = Enabled,
        Labels = new List<string>(Labels),
        Name = Name,
        OpenTagId = OpenTagId,
        DoneTagId = DoneTagId,
        RejectedTagId = RejectedTagId,
        ArchiveClosed = ArchiveClosed,
    };
}
