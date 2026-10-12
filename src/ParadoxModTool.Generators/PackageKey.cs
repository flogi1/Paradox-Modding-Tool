namespace ParadoxModTool.Generators;

/// <summary>
/// Schlüssel eines Arbeitsstands aus einem oder mehreren Kopf-Feldern (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 3): die Werte von
/// <see cref="GeneratorPackage.KeyFields"/> mit <see cref="GeneratorPackage.KeySeparator"/> verbunden (<c>adm</c> + <c>18</c> → <c>adm_18</c>).
/// Zerlegt wird von links; der letzte Teil behält den Rest.
/// </summary>
public static class PackageKey
{
    /// <summary>Der Schlüssel aus den Kopf-Werten; <see langword="null"/>, wenn ein Teil leer ist.</summary>
    public static string? Compose(GeneratorPackage package, IReadOnlyDictionary<string, string> head)
    {
        var parts = package.KeyFields.Select(f => head.GetValueOrDefault(f)?.Trim() ?? string.Empty).ToList();
        return parts.Any(p => p.Length == 0) ? null : string.Join(package.KeySeparator, parts);
    }

    /// <summary>Die Kopf-Werte eines Schlüssels; fehlende Teile sind leer.</summary>
    public static IReadOnlyDictionary<string, string> Split(GeneratorPackage package, string key)
    {
        var fields = package.KeyFields;
        var parts = fields.Count == 1 ? new[] { key } : key.Split(package.KeySeparator, fields.Count);
        return fields.Select((f, i) => (f, i)).ToDictionary(p => p.f, p => p.i < parts.Length ? parts[p.i] : string.Empty, StringComparer.Ordinal);
    }

    /// <summary>Setzt den Schlüssel des Stands und die Kopf-Werte seiner Teile.</summary>
    public static void Apply(GeneratorPackage package, GeneratorState state, string key)
    {
        state.Key = key;
        foreach (var (field, value) in Split(package, key))
        {
            state.Head.Values[field] = value;
        }
    }
}
