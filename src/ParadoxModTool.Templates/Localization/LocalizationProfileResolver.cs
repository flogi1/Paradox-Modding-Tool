namespace ParadoxModTool.Templates;

/// <summary>Ein Paar aus Präfix und Endung im aufgelösten Profil; <see cref="Extra"/> steht für eine weitere Endung der Vorlage.</summary>
public sealed record ResolvedObjectPattern(string Prefix, string Suffix, double? Share, bool Preselected, bool Active, bool Extra);

public sealed record ResolvedFieldPattern(string Name, double? Share, bool Preselected, bool Active);

/// <summary>Das Profil, das für eine Vorlage gilt, mit den Haken (eingeschaltet oder nicht) aller Paare und Felder.</summary>
public sealed class ResolvedLocProfile
{
    /// <summary>Pfad des gelernten Profils oder <see cref="LocalizationProfileResolver.Fallback"/>.</summary>
    public string Path { get; init; } = LocalizationProfileResolver.Fallback;

    public bool IsFallback { get; init; } = true;

    /// <summary>Text für die Anzeige, z. B. <c>automatisch: common/building_types</c>.</summary>
    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<ResolvedObjectPattern> Objects { get; init; } = Array.Empty<ResolvedObjectPattern>();

    public IReadOnlyList<ResolvedFieldPattern> Fields { get; init; } = Array.Empty<ResolvedFieldPattern>();

    /// <summary>Ändert sich, wenn sich Profil oder Haken ändern; das Fenster erkennt daran, dass es neu vorschlagen muss.</summary>
    public string Signature =>
        Path + "|" + string.Join(",", Objects.Select(o => $"{o.Prefix}/{o.Suffix}/{o.Active}")) + "|" + string.Join(",", Fields.Select(f => $"{f.Name}/{f.Active}"));
}

/// <summary>Bestimmt, welches Profil für eine Vorlage gilt: das gewählte, sonst das des Ordners der Quelldatei, sonst das Rückfallprofil.</summary>
public static class LocalizationProfileResolver
{
    /// <summary>Wert von <see cref="LocalizationSettings.ProfilePath"/> für "automatisch".</summary>
    public const string Automatic = "";

    /// <summary>Das Rückfallprofil: Objektnamen mit den Paaren (<c>X</c>) und (<c>X</c>, <c>_desc</c>), keine Felder.</summary>
    public const string Fallback = "*fallback";

    /// <summary>Als Pfad in einem Haken: gilt für jedes Profil.</summary>
    public const string AllProfiles = "*";

    public static string ObjectKey(string path, string prefix, string suffix) => $"o:{path}|{prefix}|{suffix}";

    public static string FieldKey(string path, string name) => $"f:{path}|{name}";

    /// <summary>Der Pfad des Profils, das "automatisch" für diesen Ordner wählt; <see langword="null"/>, wenn keines passt.</summary>
    public static string? FindAutomatic(LocalizationProfiles? learned, string? sourceFolder) => learned?.Find(sourceFolder)?.Path;

    public static ResolvedLocProfile Resolve(LocalizationProfiles? learned, LocalizationSettings settings, string? sourceFolder)
    {
        var selected = settings.ProfilePath ?? Automatic;
        var automatic = selected == Automatic;
        LocalizationFolderProfile? profile;
        if (automatic)
        {
            profile = learned?.Find(sourceFolder);
        }
        else if (selected == Fallback)
        {
            profile = null;
        }
        else
        {
            profile = learned?.Profiles.FirstOrDefault(p => string.Equals(p.Path, selected, StringComparison.OrdinalIgnoreCase));
        }

        var path = profile?.Path ?? Fallback;
        var choices = settings.ProfileChoices ?? new Dictionary<string, bool>();

        bool Active(string key, string wildcardKey, bool preselected) =>
            choices.TryGetValue(key, out var own) ? own : choices.TryGetValue(wildcardKey, out var all) ? all : preselected;

        var objects = new List<ResolvedObjectPattern>();
        if (profile is null)
        {
            foreach (var suffix in new[] { string.Empty, "_desc" })
            {
                objects.Add(new ResolvedObjectPattern(string.Empty, suffix, null, true, Active(ObjectKey(path, string.Empty, suffix), ObjectKey(AllProfiles, string.Empty, suffix), true), false));
            }
        }
        else
        {
            foreach (var o in profile.Objects)
            {
                objects.Add(new ResolvedObjectPattern(o.Prefix, o.Suffix, o.Share, o.Preselected, Active(ObjectKey(path, o.Prefix, o.Suffix), ObjectKey(AllProfiles, o.Prefix, o.Suffix), o.Preselected), false));
            }
        }

        foreach (var extra in (settings.ExtraSuffixes ?? new List<string>()).Select(s => s?.Trim() ?? string.Empty).Where(s => s.Length > 0).Distinct(StringComparer.Ordinal))
        {
            if (!objects.Any(o => o.Prefix.Length == 0 && o.Suffix == extra))
            {
                objects.Add(new ResolvedObjectPattern(string.Empty, extra, null, true, true, true));
            }
        }

        var fields = (profile?.Fields ?? new List<LocalizationFieldPattern>())
            .Select(f => new ResolvedFieldPattern(f.Name, f.Share, f.Preselected, Active(FieldKey(path, f.Name), FieldKey(AllProfiles, f.Name), f.Preselected)))
            .ToList();

        var name = profile is null ? TemplatesTexts.ProfileFallback : profile.Path;
        return new ResolvedLocProfile
        {
            Path = path,
            IsFallback = profile is null,
            Description = automatic ? TemplatesTexts.ProfileAutomatic(name) : name,
            Objects = objects,
            Fields = fields,
        };
    }

    /// <summary>
    /// Der Ordner der Datei relativ zu einer Wurzel (ohne <c>in_game</c>), z. B. <c>common/building_types</c>. Liegt die Datei unter keiner der Wurzeln, gilt der Pfad
    /// ab dem ersten Ordner <c>common</c>, <c>events</c> oder <c>gui</c> (hinter dem letzten <c>in_game</c>). <see langword="null"/>, wenn sich kein Ordner ergibt.
    /// </summary>
    public static string? RelativeFolder(string? filePath, IEnumerable<string> roots)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var path = filePath.Replace('\\', '/');
        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            var prefix = root.Replace('\\', '/').TrimEnd('/') + "/";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = path[prefix.Length..];
            if (relative.StartsWith("in_game/", StringComparison.OrdinalIgnoreCase))
            {
                relative = relative["in_game/".Length..];
            }

            var slash = relative.LastIndexOf('/');
            return slash <= 0 ? null : relative[..slash];
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var from = Array.FindLastIndex(segments, s => s.Equals("in_game", StringComparison.OrdinalIgnoreCase)) + 1;
        for (var i = from; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("common", StringComparison.OrdinalIgnoreCase)
                || segments[i].Equals("events", StringComparison.OrdinalIgnoreCase)
                || segments[i].Equals("gui", StringComparison.OrdinalIgnoreCase))
            {
                return string.Join("/", segments[i..^1]);
            }
        }

        return null;
    }
}
