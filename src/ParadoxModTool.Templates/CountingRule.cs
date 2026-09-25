using System.Text.Json;
using System.Text.Json.Serialization;

namespace ParadoxModTool.Templates;

/// <summary>Wo eine Regel gilt.</summary>
public enum RuleWhere
{
    /// <summary>Für jeden Bezeichner und String.</summary>
    Anywhere = 0,

    /// <summary>Nur innerhalb eines Eintrags <c>X = …</c> (Wert und, bei Blöcken, alles darin).</summary>
    InsideKey = 1,
}

/// <summary>Was eine Regel mit den Zahlen eines getroffenen Namens macht.</summary>
public enum RuleAction
{
    /// <summary>Keine Zahl des Namens zählt.</summary>
    Ignore = 0,

    /// <summary>Nur die mit <c>[#]</c> markierte Zahl zählt, die anderen Zahlen des Namens nicht.</summary>
    OnlyMarkedCounts = 1,

    /// <summary>Alle Zahlen des Namens zählen.</summary>
    AllCount = 2,

    /// <summary>Nur die markierte Zahl zählt nicht; über die anderen entscheiden weiter die übrigen Regeln.</summary>
    MarkedDoesNotCount = 3,
}

/// <summary>Ebene einer Regel. Ein größerer Wert ist spezifischer und steht in der Rangfolge weiter oben.</summary>
public enum RuleLayer
{
    Shipped = 0,
    AllGames = 1,
    Game = 2,
    Mod = 3,
    Template = 4,
}

public static class RuleLayerNames
{
    public static string Of(RuleLayer layer) => layer switch
    {
        RuleLayer.Shipped => "mitgeliefert",
        RuleLayer.AllGames => "Alle Spiele",
        RuleLayer.Game => "Spiel",
        RuleLayer.Mod => "Mod",
        RuleLayer.Template => "Vorlage",
        _ => layer.ToString(),
    };
}

/// <summary>Eine Zählregel. Die Ebene ergibt sich aus der Datei, in der sie steht, und ist nicht Teil der Regel.</summary>
public sealed class CountingRule
{
    /// <summary>Eigene Regeln: GUID ohne Bindestriche. Mitgelieferte Regeln haben feste lesbare Kennungen.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Eigener Schalter der Regel.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Nur auf der Ebene "Alle Spiele" wirksam: <c>*</c> oder Spiel-Kennungen wie <c>eu5</c>.</summary>
    public List<string> Games { get; set; } = new() { "*" };

    public RuleWhere Where { get; set; }

    /// <summary>Bei <see cref="RuleWhere.InsideKey"/>: der Schlüssel, <c>*</c> erlaubt, ohne Beachtung der Groß-/Kleinschreibung.</summary>
    public string WhereKey { get; set; } = string.Empty;

    /// <summary>Ein Bezeichner trifft, wenn eines der Muster passt (Mustersprache: <c>*</c>, <c>#</c>, <c>[#]</c>).</summary>
    public List<string> Patterns { get; set; } = new();

    public bool CaseSensitive { get; set; }

    public RuleAction Action { get; set; } = RuleAction.OnlyMarkedCounts;

    public static CountingRule Create(string name) => new() { Id = Guid.NewGuid().ToString("N"), Name = name };

    public CountingRule Clone() => new()
    {
        Id = Id,
        Name = Name,
        Enabled = Enabled,
        Games = new List<string>(Games),
        Where = Where,
        WhereKey = WhereKey,
        Patterns = new List<string>(Patterns),
        CaseSensitive = CaseSensitive,
        Action = Action,
    };
}

/// <summary>Inhalt einer Regeldatei einer Ebene: die Regeln in Reihenfolge und die auf dieser Ebene abgeschalteten Kennungen.</summary>
public sealed class RuleFile
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public int Version { get; set; } = 1;

    public List<CountingRule> Rules { get; set; } = new();

    public List<string> DisabledRuleIds { get; set; } = new();
}

/// <summary>Eine Regel zusammen mit der Ebene, auf der sie liegt.</summary>
public sealed record EffectiveRule(CountingRule Rule, RuleLayer Layer)
{
    /// <summary>Text für die Anzeige, warum eine Zahl zählt oder nicht zählt.</summary>
    public string Reason => $"Regel „{Rule.Name}“ ({RuleLayerNames.Of(Layer)})";
}
