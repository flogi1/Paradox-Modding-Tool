using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.SaveGames;

/// <summary>Objektart für Baum und Spalte: <see cref="Order"/> 0 = Global, 1..n = Profil-Reihenfolge, n+1 = Sonstige.</summary>
public sealed record SaveObjectKind(int Order, string Label, string? Pattern);

/// <summary>
/// Ein Besitzer von Variablen mit Art, Anzeigename und Id (null, wenn der Pfad keine Id hat). <see cref="Tag"/>: Land-Tag des Objekts (bei
/// Ländern der eigene, bei Objekten mit <c>tagField</c> der ihres Landes, sonst null). <see cref="ScriptReference"/>: Verweis im Skript
/// (z.B. <c>character:38106</c>), wenn das Profil einen kennt.
/// </summary>
public sealed record SaveObjectRef(string OwnerPath, SaveObjectKind Kind, string DisplayName, string? Id, string? Tag = null, string? ScriptReference = null);

/// <summary>
/// Ordnet Besitzer-Pfade den Objektarten des Spielprofils zu und bildet Anzeigenamen aus den erfassten Namensfeldern. Kennt keine
/// Spielabschnitte selbst: Muster, Namensquelle und Texte kommen aus <see cref="SaveGameSettings.ObjectKinds"/>.
/// </summary>
public sealed class SaveObjectNames
{
    private readonly IReadOnlyList<(SaveObjectKindSetting Setting, SaveObjectKind Kind, string[] Pattern)> _kinds;
    private readonly IReadOnlyDictionary<string, string> _captured;
    private readonly SaveObjectKind _global;
    private readonly SaveObjectKind _other;
    private readonly Dictionary<string, SaveObjectRef> _cache = new(StringComparer.Ordinal);

    public SaveObjectNames(SaveGameSettings settings, IReadOnlyDictionary<string, string> captured)
    {
        _captured = captured;
        _global = new SaveObjectKind(0, SaveExplorerTexts.Global, null);
        _kinds = settings.ObjectKinds
            .Select((setting, index) => (setting, new SaveObjectKind(index + 1, SaveExplorerTexts.KindLabel(setting), setting.Path), setting.Path.Split('/')))
            .ToList();
        _other = new SaveObjectKind(_kinds.Count + 1, SaveExplorerTexts.Other, null);
        Kinds = new[] { _global }.Concat(_kinds.Select(k => k.Kind)).Append(_other).ToList();
    }

    public IReadOnlyList<SaveObjectKind> Kinds { get; }

    /// <summary>Tabellen und Felder, die der Leser für die Namensquellen des Profils erfassen muss.</summary>
    public static SaveCaptureRequest CaptureRequestFor(SaveGameSettings settings) =>
        new(
            settings.ObjectKinds.Where(k => k.NameSource == "table" && !string.IsNullOrEmpty(k.NameTable)).Select(k => k.NameTable!)
                .Concat(settings.ObjectKinds.Where(k => !string.IsNullOrEmpty(k.TagField) && !string.IsNullOrEmpty(k.TagTable)).Select(k => k.TagTable!))
                .Distinct()
                .ToList(),
            settings.ObjectKinds.Where(k => k.NameSource == "field" && !string.IsNullOrEmpty(k.NameField)).Select(k => new SaveFieldCapture(k.Path, k.NameField!))
                .Concat(settings.ObjectKinds.Where(k => !string.IsNullOrEmpty(k.TagField)).Select(k => new SaveFieldCapture(k.Path, k.TagField!)))
                .ToList());

    public SaveObjectRef Resolve(string ownerPath)
    {
        if (_cache.TryGetValue(ownerPath, out var cached))
        {
            return cached;
        }

        var resolved = ResolveUncached(ownerPath);
        _cache[ownerPath] = resolved;
        return resolved;
    }

    /// <summary>Anzeigename des Objekts <paramref name="objectPath"/>/<paramref name="id"/>; null, wenn es zu keiner Objektart des Profils passt.</summary>
    public string? NameOf(string objectPath, string id)
    {
        var resolved = Resolve(objectPath + "/" + id);
        return resolved.Kind == _other ? null : resolved.DisplayName;
    }

    private SaveObjectRef ResolveUncached(string ownerPath)
    {
        if (ownerPath.Length == 0)
        {
            return new SaveObjectRef(ownerPath, _global, SaveExplorerTexts.Global, null);
        }

        foreach (var (setting, kind, pattern) in _kinds)
        {
            // Liegen die Variablen unter dem Objekt (CK III: living/<id>/alive_data), zählt das Objekt selbst.
            var objectPath = !string.IsNullOrEmpty(setting.VariablesPath) && ownerPath.EndsWith("/" + setting.VariablesPath, StringComparison.Ordinal)
                ? ownerPath[..^(setting.VariablesPath.Length + 1)]
                : ownerPath;
            var segments = objectPath.Split('/');
            if (!Matches(pattern, segments))
            {
                continue;
            }

            var last = segments[^1];
            var id = pattern[^1] == "*" && setting.NameSource != "pathSegment" ? last : null;
            var name = setting.NameSource switch
            {
                "table" => _captured.GetValueOrDefault(setting.NameTable + "/" + last) ?? last,
                "field" => _captured.TryGetValue(objectPath + "/" + setting.NameField, out var field) ? $"{field} ({last})" : last,
                _ => last,
            };
            return new SaveObjectRef(ownerPath, kind, name, id, TagOf(setting, objectPath, last), ScriptReferenceOf(setting, id));
        }

        var ownerSegments = ownerPath.Split('/');

        var otherId = ownerSegments[^1].All(char.IsAsciiDigit) ? ownerSegments[^1] : null;
        return new SaveObjectRef(ownerPath, _other, ownerPath, otherId);
    }

    private string? TagOf(SaveObjectKindSetting setting, string ownerPath, string last)
    {
        if (!string.IsNullOrEmpty(setting.TagField))
        {
            return _captured.TryGetValue(ownerPath + "/" + setting.TagField, out var country)
                ? _captured.GetValueOrDefault(setting.TagTable + "/" + country) ?? country
                : null;
        }

        return setting.NameSource == "table" ? _captured.GetValueOrDefault(setting.NameTable + "/" + last) : null;
    }

    private static string? ScriptReferenceOf(SaveObjectKindSetting setting, string? id) =>
        string.IsNullOrEmpty(setting.ScriptReference) || id is null
            ? null
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, setting.ScriptReference, id);

    private static bool Matches(string[] pattern, string[] segments)
    {
        if (pattern.Length != segments.Length)
        {
            return false;
        }

        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != "*" && pattern[i] != segments[i])
            {
                return false;
            }
        }

        return true;
    }
}
