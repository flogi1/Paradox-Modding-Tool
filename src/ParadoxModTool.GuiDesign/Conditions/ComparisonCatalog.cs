using ParadoxModTool.GuiDesign.DataTypes;

namespace ParadoxModTool.GuiDesign.Conditions;

/// <summary>
/// Welche Typ-Varianten es je Vergleichsart gibt. Aus dem Datentyp-Dump gelernt (globale <c>bool</c>-Funktionen <c>&lt;Art&gt;_&lt;Typ&gt;</c>), damit neue Typen
/// nach einem Spielupdate von selbst dazukommen; ohne Dump gilt die eingebaute Liste.
/// </summary>
public sealed class ComparisonCatalog
{
    private static readonly string[] Numbers = { "int32", "int64", "uint32", "uint64", "float", "CFixedPoint" };

    private static readonly Dictionary<string, CompareKind> Kinds =
        Enum.GetValues<CompareKind>().ToDictionary(k => k.ToString(), k => k, StringComparer.Ordinal);

    private readonly Dictionary<CompareKind, List<string>> _variants;

    private ComparisonCatalog(Dictionary<CompareKind, List<string>> variants) => _variants = variants;

    public static ComparisonCatalog BuiltIn { get; } = new(Enum.GetValues<CompareKind>().ToDictionary(
        k => k,
        k => k is CompareKind.EqualTo or CompareKind.NotEqualTo ? Numbers.Append("string").ToList() : Numbers.ToList()));

    public static ComparisonCatalog From(DataTypeDatabase db)
    {
        var variants = new Dictionary<CompareKind, List<string>>();
        foreach (var entry in db.Globals.Where(g => g.Kind == DataTypeKind.GlobalFunction && g.ReturnType == "bool"))
        {
            if (Split(entry.Name) is ({ } kind, { } variant) && entry.Arguments.Count == Arity(kind))
            {
                if (!variants.TryGetValue(kind, out var list))
                {
                    variants[kind] = list = new List<string>();
                }

                if (!list.Contains(variant))
                {
                    list.Add(variant);
                }
            }
        }

        return variants.Count == 0 ? BuiltIn : new ComparisonCatalog(variants);
    }

    public bool TryParse(string functionName, out CompareKind kind, out string variant)
    {
        kind = default;
        variant = string.Empty;
        if (Split(functionName) is not ({ } k, { } v) || !Variants(k).Contains(v))
        {
            return false;
        }

        (kind, variant) = (k, v);
        return true;
    }

    /// <summary>Der Name gehört zu einer Vergleichsfamilie, unabhängig davon, ob es die Variante gibt.</summary>
    public static bool TryKind(string functionName, out CompareKind kind)
    {
        var (k, _) = Split(functionName);
        kind = k ?? default;
        return k is not null;
    }

    public IReadOnlyList<string> Variants(CompareKind kind) => _variants.TryGetValue(kind, out var list) ? list : Array.Empty<string>();

    public static string FunctionName(CompareKind kind, string variant) => variant.Length == 0 ? kind.ToString() : $"{kind}_{variant}";

    /// <summary>Anzahl der Argumente: 1 bei <c>IsOdd</c>/<c>IsEven</c>, 3 bei <c>Between…</c>, sonst 2.</summary>
    public static int Arity(CompareKind kind) => kind switch
    {
        CompareKind.IsOdd or CompareKind.IsEven => 1,
        CompareKind.Between or CompareKind.BetweenInclusive or CompareKind.BetweenInclusiveOfMin or CompareKind.BetweenInclusiveOfMax => 3,
        _ => 2,
    };

    private static (CompareKind? Kind, string? Variant) Split(string name)
    {
        var cut = name.LastIndexOf('_');
        if (cut <= 0 || cut == name.Length - 1 || !Kinds.TryGetValue(name[..cut], out var kind))
        {
            return (null, null);
        }

        return (kind, name[(cut + 1)..]);
    }
}
