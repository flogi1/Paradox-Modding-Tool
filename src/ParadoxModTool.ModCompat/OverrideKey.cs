namespace ParadoxModTool.ModCompat;

/// <summary>Präfix vor einem Eintrag der obersten Ebene (siehe EU-V-Wiki "Mod compatibility").</summary>
public enum OverrideOperator
{
    None,
    InjectOrCreate,
    ReplaceOrCreate,
    TryInject,
    TryReplace,
    Inject,
    Replace,
}

/// <summary>Ein Eintragsschlüssel zerlegt in Operator und eigentlichen Namen, z. B. <c>INJECT:building_x</c>.</summary>
public readonly record struct OverrideKey(OverrideOperator Operator, string Key)
{
    private static readonly (string Prefix, OverrideOperator Operator)[] Prefixes =
    {
        ("INJECT_OR_CREATE:", OverrideOperator.InjectOrCreate),
        ("REPLACE_OR_CREATE:", OverrideOperator.ReplaceOrCreate),
        ("TRY_INJECT:", OverrideOperator.TryInject),
        ("TRY_REPLACE:", OverrideOperator.TryReplace),
        ("INJECT:", OverrideOperator.Inject),
        ("REPLACE:", OverrideOperator.Replace),
    };

    public static OverrideKey Parse(string raw)
    {
        foreach (var (prefix, op) in Prefixes)
        {
            if (raw.StartsWith(prefix, StringComparison.Ordinal) && raw.Length > prefix.Length)
            {
                return new OverrideKey(op, raw[prefix.Length..]);
            }
        }

        return new OverrideKey(OverrideOperator.None, raw);
    }

    /// <summary>Wie <see cref="Parse"/>, erkennt aber nur Präfixe, die das Spiel kennt (<paramref name="keyOperators"/>). Unbekannte Präfixe bleiben Teil des Schlüssels.</summary>
    public static OverrideKey ParseFor(string raw, IReadOnlyCollection<string> keyOperators)
    {
        foreach (var (prefix, op) in Prefixes)
        {
            if (raw.StartsWith(prefix, StringComparison.Ordinal) && raw.Length > prefix.Length)
            {
                return keyOperators.Contains(prefix.TrimEnd(':'), StringComparer.OrdinalIgnoreCase)
                    ? new OverrideKey(op, raw[prefix.Length..])
                    : new OverrideKey(OverrideOperator.None, raw);
            }
        }

        return new OverrideKey(OverrideOperator.None, raw);
    }

    /// <summary>Verarbeitungsreihenfolge laut Wiki (kleiner = früher). Eine einfache Definition ohne Präfix hat -1 und kommt vor allen Operatoren.</summary>
    public int Order => Operator switch
    {
        OverrideOperator.InjectOrCreate => 0,
        OverrideOperator.ReplaceOrCreate => 1,
        OverrideOperator.TryInject => 2,
        OverrideOperator.TryReplace => 3,
        OverrideOperator.Inject => 4,
        OverrideOperator.Replace => 5,
        _ => -1,
    };

    public bool IsInject => Operator is OverrideOperator.Inject or OverrideOperator.TryInject or OverrideOperator.InjectOrCreate;

    public bool IsReplace => Operator is OverrideOperator.Replace or OverrideOperator.TryReplace or OverrideOperator.ReplaceOrCreate;
}
