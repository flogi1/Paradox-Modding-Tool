using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.GfxAudit;

/// <summary>Eine gelesene Textdatei mit Ort (Zeile 0), Herkunft, Anzeigename der Wurzel und ihren Sprites und Verweisen.</summary>
public sealed record GfxFile(SiteLocation File, FlagOrigin Origin, string RootName, GfxScan Scan);

/// <summary>Ein wirksames Bild (nach Überschreibung und <c>replace_path</c>).</summary>
/// <param name="File">Ort des Bildes (Zeile 0).</param>
/// <param name="Origin">Herkunft der Wurzel.</param>
/// <param name="LogicalPath">Logischer Pfad (<see cref="GfxAuditRules.LogicalPath"/>): Schrägstriche, klein, ohne Phasenordner.</param>
/// <param name="ReplacesOriginal">
/// Nur bei Bildern der gewählten Mod: Eine niedriger gerankte Wurzel hat eine Datei mit demselben relativen Pfad (auf Windows ohne Rücksicht auf
/// Groß-/Kleinschreibung); die Mod ersetzt also ein vorhandenes Bild.
/// </param>
public sealed record GfxImage(SiteLocation File, FlagOrigin Origin, string LogicalPath, bool ReplacesOriginal);

/// <summary>Eine Erwähnung eines Mod-Namens (logischer Bildpfad oder Sprite-Name) mit ihrem Ort (Wurzel, relativer Pfad, Zeile).</summary>
public sealed record GfxMentionSite(string Name, SiteLocation Location);

/// <summary>
/// Ergebnis eines Aufbaus der GFX-Prüfung: alle gelesenen Textdateien, alle wirksamen Bilder und die Erwähnungen der Namen der gewählten Mod
/// in allen Textdateien. Listen in Reihenfolge der Dateien, Erwähnungen darin nach Zeile.
/// </summary>
public sealed class GfxAuditIndex
{
    public GfxAuditIndex(
        FlagSourceSet sources,
        FlagRoot? modRoot,
        IReadOnlyList<GfxFile> files,
        IReadOnlyList<GfxImage> images,
        IReadOnlyList<GfxMentionSite> mentions,
        IReadOnlyList<string> unreadFiles,
        DateTime builtUtc)
    {
        Sources = sources;
        ModRoot = modRoot;
        Files = files;
        Images = images;
        Mentions = mentions;
        UnreadFiles = unreadFiles;
        BuiltUtc = builtUtc;
    }

    /// <summary>Die Quellen, aus denen gebaut wurde.</summary>
    public FlagSourceSet Sources { get; }

    /// <summary>Die Wurzel der gewählten Mod (<see cref="FlagOrigin.ActiveMod"/>); null, wenn keine gewählt ist.</summary>
    public FlagRoot? ModRoot { get; }

    /// <summary>Die Textdateien (Endungen aus <see cref="GfxAuditRules.TextExtensions"/>) aller Wurzeln.</summary>
    public IReadOnlyList<GfxFile> Files { get; }

    /// <summary>Die wirksamen Bilder (<c>.dds</c>, <c>.png</c>, <c>.tga</c>) aller Wurzeln.</summary>
    public IReadOnlyList<GfxImage> Images { get; }

    /// <summary>Erwähnungen der Namen der gewählten Mod (Bilder und Sprites) aus allen Textdateien.</summary>
    public IReadOnlyList<GfxMentionSite> Mentions { get; }

    /// <summary>Volle Pfade der Dateien, die nicht gelesen werden konnten (sortiert).</summary>
    public IReadOnlyList<string> UnreadFiles { get; }

    public DateTime BuiltUtc { get; }
}
