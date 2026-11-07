namespace ParadoxModTool.FlagTracker;

/// <summary>Fundstelle, deren Name noch $PARAM$ enthält. DefinitionName = Schlüssel des Scripted Effects/Triggers, in dem sie steht, sonst null.</summary>
public sealed record TemplateSite(string NameTemplate, FlagKind Kind, string? Family, FlagRole Role, int Line, string? DefinitionName);

/// <summary>Aufruf eines Scripted Effects/Triggers. Params ist null bei "eff = yes"; ein Block als Parameterwert wird null. CallerDefinition = umgebende Definition (für Durchreichen).</summary>
public sealed record CallSite(string Callee, IReadOnlyDictionary<string, string?>? Params, int Line, string? CallerDefinition);

/// <summary>Wie FlagSite ohne Herkunft und Wurzel (die ergänzt der Builder).</summary>
public sealed record RawSite(string Name, FlagKind Kind, string? Family, FlagRole Role, int Line, bool IsPattern = false, bool IsCandidate = false);

/// <summary>Definition eines Scripted Effects oder Triggers (oberster Schlüssel der Datei).</summary>
public sealed record DefinitionInfo(string Name, bool IsTrigger, int Line);

/// <summary>Schlüssel einer wirksamen Definition: Scripted Effects und Triggers haben getrennte Namensräume, gleiche Namen stören sich nicht.</summary>
public readonly record struct DefinitionKey(string Name, bool IsTrigger);

/// <summary>Ergebnis des Lesens einer Datei.</summary>
public sealed class FileScan
{
    public List<RawSite> Sites { get; init; } = new();
    public List<TemplateSite> Templates { get; init; } = new();

    /// <summary>Jeder oberste Schlüssel in common/scripted_effects oder common/scripted_triggers.</summary>
    public List<DefinitionInfo> Definitions { get; init; } = new();

    /// <summary>Wird von ScriptReader.ReadCalls gefüllt (Task 4).</summary>
    public List<CallSite> Calls { get; init; } = new();
}

/// <summary>
/// Vergleich der Namen von Aufgerufenen und Definitionen: Stellaris-Inline-Skripte (<c>inline_script:pfad</c>, <see cref="ScriptReader.InlineScriptPrefix"/>)
/// ohne Rücksicht auf Groß/Klein, denn der Pfad ist ein Dateipfad (<c>Contracts/harvestresource</c> ruft <c>contracts/HarvestResource.txt</c>);
/// alle anderen Namen genau.
/// </summary>
public sealed class CalleeNameComparer : IEqualityComparer<string>
{
    public static CalleeNameComparer Instance { get; } = new();

    /// <summary>Vergleich von <see cref="DefinitionKey"/>: Name wie <see cref="Instance"/>, Art genau.</summary>
    public static IEqualityComparer<DefinitionKey> DefinitionKeys { get; } = new DefinitionKeyComparer();

    private CalleeNameComparer()
    {
    }

    public bool Equals(string? x, string? y)
    {
        if (x is null || y is null)
        {
            return x is null && y is null;
        }

        return IsInline(x) && IsInline(y) ? string.Equals(x, y, StringComparison.OrdinalIgnoreCase) : string.Equals(x, y, StringComparison.Ordinal);
    }

    public int GetHashCode(string obj) =>
        IsInline(obj) ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj) : StringComparer.Ordinal.GetHashCode(obj);

    private static bool IsInline(string name) => name.StartsWith(ScriptReader.InlineScriptPrefix, StringComparison.Ordinal);

    private sealed class DefinitionKeyComparer : IEqualityComparer<DefinitionKey>
    {
        public bool Equals(DefinitionKey x, DefinitionKey y) => x.IsTrigger == y.IsTrigger && Instance.Equals(x.Name, y.Name);

        public int GetHashCode(DefinitionKey obj) => HashCode.Combine(obj.IsTrigger, Instance.GetHashCode(obj.Name));
    }
}
