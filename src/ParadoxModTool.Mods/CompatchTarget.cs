namespace ParadoxModTool.Mods;

/// <summary>
/// Ein Ziel eines Kompatchs: genau eins von <see cref="ModId"/> (Register), <see cref="WorkshopId"/> (Steam) oder <see cref="Folder"/>
/// (beliebiger Ordner). <see cref="DisplayName"/> dient nur der Anzeige, auch wenn das Ziel gerade fehlt.
/// </summary>
public sealed class CompatchTarget
{
    public string? ModId { get; set; }

    public string? WorkshopId { get; set; }

    public string? Folder { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gleichheitsschlüssel, z. B. <c>mod:&lt;id&gt;</c>, <c>workshop:&lt;id&gt;</c>, <c>folder:&lt;pfad klein&gt;</c>.</summary>
    public string Key => ModId is { Length: > 0 } ? "mod:" + ModId
        : WorkshopId is { Length: > 0 } ? "workshop:" + WorkshopId
        : "folder:" + (Folder ?? string.Empty).ToLowerInvariant();
}
