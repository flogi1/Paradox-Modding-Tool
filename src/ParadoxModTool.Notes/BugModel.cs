using System.Text.Json.Serialization;

namespace ParadoxModTool.Notes;

/// <summary>Woher eine Meldung stammt. Plan 1 kennt nur <see cref="Local"/>; GitHub und Discord kommen mit Plan 2 und 3.</summary>
public enum BugSource
{
    Local,
    GitHub,
    Discord,
}

/// <summary>Status einer Meldung. Erledigt und Verworfen gelten beide als geschlossen.</summary>
public enum BugStatus
{
    Open,
    Done,
    Rejected,
}

/// <summary>
/// Bezug einer Meldung auf eine Datei, optional mit Zeile oder Zeilenbereich. <see cref="Key"/> ist der Notiz-Schlüssel der Datei
/// (<see cref="NoteKeys.ForFile"/>): <c>mod:&lt;id&gt;/&lt;relativer Pfad&gt;</c> für Dateien geführter Mods, sonst der volle Pfad.
/// Zeilen wandern bei Einfügungen nicht mit; sie sind ein Hinweis.
/// </summary>
public sealed class BugLink
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Erste Zeile (1-basiert); <see langword="null"/> = Bezug auf die ganze Datei.</summary>
    public int? Line { get; set; }

    /// <summary>Letzte Zeile bei einem Bereich, nur größer als <see cref="Line"/>.</summary>
    public int? EndLine { get; set; }

    [JsonIgnore]
    public string LineText => BugTexts.LineText(Line, EndLine);

    /// <summary>Relativer Pfad bei Mod-Dateien, sonst der volle Pfad.</summary>
    [JsonIgnore]
    public string DisplayPath => NoteKeys.TryParseModKey(Key, out _, out var relative) ? relative : Key;

    public static BugLink Create(string key, int? line, int? endLine) => new()
    {
        Key = key,
        Line = line,
        EndLine = line is int start && endLine is int end && end > start ? end : null,
    };

    public static BugLink ForFile(string filePath, int? line, int? endLine) => Create(NoteKeys.ForFile(filePath), line, endLine);

    /// <summary>Die Mod eines Mod-Schlüssels; <see langword="null"/> bei einem vollen Pfad.</summary>
    public static string? ModIdOf(string key) => NoteKeys.TryParseModKey(key, out var modId, out _) ? modId : null;

    public bool SameAs(BugLink other) =>
        string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase) && Line == other.Line && EndLine == other.EndLine;
}

/// <summary>
/// Eine Meldung: lokaler Bug oder (ab Plan 2) Bugreport aus GitHub/Discord. Felder mit Eigentümer "Dienst" (Titel, Text, Autor, Status,
/// Zeiten, Url, ExternalId) überschreibt ein Abgleich; <see cref="Links"/> und <see cref="Seen"/> gehören dem Nutzer.
/// <see cref="ModId"/> ist die Besitzer-Mod; das Projekt ist ihr Hauptmod und wird bei jedem Lesen neu bestimmt.
/// </summary>
public sealed class BugReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Besitzer-Mod; <see langword="null"/> = ohne Projekt.</summary>
    public string? ModId { get; set; }

    public BugSource Source { get; set; } = BugSource.Local;

    /// <summary>Verknüpfte Quelle (z. B. <c>owner/repo</c>); leer bei lokalen Bugs.</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary>Issue-Nummer bzw. Thread-ID; leer bei lokalen Bugs.</summary>
    public string ExternalId { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public BugStatus Status { get; set; } = BugStatus.Open;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? StatusChangedUtc { get; set; }

    /// <summary>Beim Dienst nicht mehr vorhanden oder Quelle nicht mehr verknüpft (ab Plan 2).</summary>
    public bool Removed { get; set; }

    public List<BugLink> Links { get; set; } = new();

    /// <summary>Anhänge (Screenshots, Dateien) der ersten Nachricht bzw. des Issue-Texts; gehören dem Dienst.</summary>
    public List<BugAttachment> Attachments { get; set; } = new();

    /// <summary>Schon angesehen; lokale Bugs sind immer gesehen.</summary>
    public bool Seen { get; set; } = true;

    [JsonIgnore]
    public bool IsOpen => Status == BugStatus.Open;

    /// <summary>Erste nicht leere Zeile des Texts, zur Vorschau in Listen.</summary>
    [JsonIgnore]
    public string BodyPreview => (Body ?? string.Empty)
        .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault() ?? string.Empty;
}

/// <summary>Inhalt von <c>bugreports.json</c>.</summary>
public sealed class BugData
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public List<BugReport> Reports { get; set; } = new();
}
