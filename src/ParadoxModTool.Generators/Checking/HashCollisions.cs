namespace ParadoxModTool.Generators;

/// <summary>Ein neuer Schlüssel, dessen Hash schon ein anderer Schlüssel hat (<see cref="ExistingKey"/>: aus dem Index oder ein anderer neuer Schlüssel).</summary>
public sealed record HashCollision(string NewKey, string ExistingKey, uint Hash);

/// <summary>Findet Hash-Kollisionen neuer Lokalisierungsschlüssel eines Plans.</summary>
public static class HashCollisions
{
    /// <summary>
    /// Neu sind die Schlüssel der geplanten <c>.yml</c>-Dateien, die in <c>NewText</c> stehen, aber nicht in <c>OriginalText ?? ModBaseText</c>.
    /// Gemeldet wird eine Kollision mit einem indizierten Schlüssel und eine zwischen zwei neuen Schlüsseln (beim später gefundenen).
    /// </summary>
    public static IReadOnlyList<HashCollision> Find(GeneratorPlan plan, LocalizationKeyIndex index) => Find(NewKeys(plan), index);

    /// <summary>
    /// Die neuen Schlüssel des Plans (siehe <see cref="Find(GeneratorPlan, LocalizationKeyIndex)"/>), jeder einmal, in Fundreihenfolge. Ohne neue Schlüssel
    /// braucht es keinen Index; das Fenster baut ihn dann gar nicht erst.
    /// </summary>
    public static IReadOnlyList<string> NewKeys(GeneratorPlan plan)
    {
        var newKeys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in plan.Files)
        {
            if (file.NewText is null || !file.RelativePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var before = YmlReader.Values(file.OriginalText ?? file.ModBaseText ?? string.Empty);
            foreach (var key in YmlReader.Values(file.NewText).Keys)
            {
                if (!before.ContainsKey(key) && seen.Add(key))
                {
                    newKeys.Add(key);
                }
            }
        }

        return newKeys;
    }

    /// <summary>Kollisionen der neuen Schlüssel <paramref name="newKeys"/> mit dem Index und untereinander.</summary>
    public static IReadOnlyList<HashCollision> Find(IReadOnlyList<string> newKeys, LocalizationKeyIndex index)
    {
        var collisions = new List<HashCollision>();
        var firstByHash = new Dictionary<uint, string>();
        foreach (var key in newKeys)
        {
            var hash = LocalizationHash.Murmur3(key);
            foreach (var other in index.Others(key))
            {
                collisions.Add(new HashCollision(key, other, hash));
            }

            if (firstByHash.TryGetValue(hash, out var earlier))
            {
                collisions.Add(new HashCollision(key, earlier, hash));
            }
            else
            {
                firstByHash[hash] = key;
            }
        }

        return collisions;
    }
}
