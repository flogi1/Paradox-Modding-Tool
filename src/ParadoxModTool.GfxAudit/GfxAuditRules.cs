using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.GfxAudit;

/// <summary>
/// Regeln der GFX-Prüfung für ein Spiel (Datei <c>profiles/gfxaudit/&lt;spiel&gt;.json</c>): welche Textdateien Bildnamen nennen können, wie Sprites
/// definiert und referenziert werden, welche Namensvarianten die Spiele selbst bilden und welche Ordner die Engine von sich aus lädt.
/// Die Regeln sind reine Daten, der Code kennt kein Spiel.
/// </summary>
public sealed class GfxAuditRules
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions NormalizeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Endungen der Textdateien, in denen Bild- und Spritenamen vorkommen können (mit Punkt, klein).</summary>
    public IReadOnlyList<string> TextExtensions { get; init; } = Array.Empty<string>();

    /// <summary>Namen der Blöcke, die ein Sprite definieren (<c>spriteType</c> …); leer = das Spiel hat keine Sprites.</summary>
    public IReadOnlyList<string> SpriteBlocks { get; init; } = Array.Empty<string>();

    /// <summary>Schlüssel in einem Sprite-Block, deren Wert ein Bild nennt (<c>texturefile</c> …).</summary>
    public IReadOnlyList<string> SpriteTextureKeys { get; init; } = Array.Empty<string>();

    /// <summary>Präfix der Sprite-Namen (<c>GFX_</c>); null = das Spiel referenziert keine Sprites, es gibt also auch keinen Fund „Sprite fehlt“.</summary>
    public string? SpriteRefPrefix { get; init; }

    /// <summary>Präfixe, die das Spiel selbst vor einen Namen setzt (<c>GFX_idea_</c>, <c>mod_</c> …); ein Bild gilt auch mit ihnen als genannt.</summary>
    public IReadOnlyList<string> NamePrefixes { get; init; } = Array.Empty<string>();

    /// <summary>Suffixe, die das Spiel selbst an einen Namen hängt (<c>_shine</c>, <c>_grey</c> …).</summary>
    public IReadOnlyList<string> NameSuffixes { get; init; } = Array.Empty<string>();

    /// <summary>Ordner (Schrägstriche, relativ zum logischen Pfad), die die Engine selbst lädt; Bilder darin gelten immer als benutzt.</summary>
    public IReadOnlyList<string> AlwaysUsedFolders { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Logische Ordnerpfade (Schrägstriche, ohne Schrägstrich am Ende), die als Verweis ohne Endung nicht zählen: Ein Pfad aus genau diesem Ordner hält die Bilder darunter nicht als benutzt
    /// (EU V: <c>ICON_PATH</c> der Defines). Der Vergleich ist exakt, engere Pfade darunter zählen weiter.
    /// </summary>
    public IReadOnlyList<string> IgnoredFolderReferences { get; init; } = Array.Empty<string>();

    /// <summary>Ordner, unter denen ein <c>gfx/…</c>-Verweis aufgelöst werden darf; <c>""</c> = der Stamm selbst (EU V: dazu die Phasenordner).</summary>
    public IReadOnlyList<string> PathRoots { get; init; } = Array.Empty<string>();

    /// <summary>Quelle und Datum der Messung; nur zur Information.</summary>
    public string? Notes { get; init; }

    /// <summary>SHA-256 des normalisierten JSON, die ersten 16 Hex-Zeichen; ändert sich mit jeder Regeländerung (Cache-Schlüssel).</summary>
    [JsonIgnore]
    public string Fingerprint
    {
        get
        {
            var json = JsonSerializer.Serialize(this, NormalizeOptions);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(hash).ToLowerInvariant()[..16];
        }
    }

    /// <summary>Liest eine Regeldatei; wirft bei ungültigem JSON.</summary>
    public static GfxAuditRules Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GfxAuditRules>(json, ReadOptions)
            ?? throw new JsonException("Leere Regeldatei: " + path);
    }

    /// <summary>Regeln des Spielprofils; null ohne Verweis im Profil oder ohne Datei.</summary>
    public static GfxAuditRules? ForProfile(GameProfile profile, string profilesDirectory)
    {
        if (string.IsNullOrWhiteSpace(profile.GfxAuditRules))
        {
            return null;
        }

        var path = Path.Combine(profilesDirectory, profile.GfxAuditRules.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? Load(path) : null;
    }

    /// <summary>
    /// Der logische Pfad einer Datei: Schrägstriche (mehrere hintereinander als einer), klein, und der erste passende nicht leere Eintrag aus <see cref="PathRoots"/> als Ordnerpräfix
    /// entfernt (EU V: <c>in_game/gfx/A.dds</c> wird zu <c>gfx/a.dds</c>). Nur ein Präfix wird entfernt.
    /// </summary>
    public string LogicalPath(string relativePath)
    {
        var path = GfxNames.CollapseSlashes(relativePath.Replace('\\', '/')).TrimStart('/').ToLowerInvariant();
        foreach (var root in PathRoots)
        {
            var prefix = root.Replace('\\', '/').Trim('/').ToLowerInvariant();
            if (prefix.Length == 0)
            {
                continue;
            }

            prefix += "/";
            if (path.StartsWith(prefix, StringComparison.Ordinal) && path.Length > prefix.Length)
            {
                return path[prefix.Length..];
            }
        }

        return path;
    }

    /// <summary>Ist der logische Pfad (nach Schrägstrich- und Kleinschreibung-Normalisierung) genau einer der <see cref="IgnoredFolderReferences"/>?</summary>
    public bool IsIgnoredFolderReference(string logicalPath)
    {
        var path = logicalPath.Replace('\\', '/').Trim('/').ToLowerInvariant();
        foreach (var folder in IgnoredFolderReferences)
        {
            if (path == folder.Replace('\\', '/').Trim('/').ToLowerInvariant())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Liegt der logische Pfad in einem der <see cref="AlwaysUsedFolders"/>?</summary>
    public bool IsAlwaysUsed(string relativePath)
    {
        var logical = LogicalPath(relativePath);
        foreach (var folder in AlwaysUsedFolders)
        {
            var prefix = folder.Replace('\\', '/').Trim('/').ToLowerInvariant() + "/";
            if (prefix.Length > 1 && logical.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
