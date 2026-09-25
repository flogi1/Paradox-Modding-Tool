using System.Globalization;

namespace ParadoxModTool.Templates;

/// <summary>Woher ein Zählpunkt stammt; bestimmt, ob er standardmäßig zählt.</summary>
public enum PointKind
{
    /// <summary>Ziffern in einem Bezeichner wie <c>idea_1</c> oder <c>my_mod.0001</c>. Standardmäßig an.</summary>
    Identifier,

    /// <summary>Ziffern in einem Text in Anführungszeichen wie <c>"Idee 1"</c>. Standardmäßig an.</summary>
    Text,

    /// <summary>Eine alleinstehende Zahl wie <c>cost = 10</c>. Standardmäßig aus, weil sie im Einzelbeispiel zu oft keine Zählung meint.</summary>
    Number,
}

/// <summary>Ein Ziffernblock im Vorlagentext: Startposition und Länge.</summary>
public readonly record struct PointOccurrence(int Start, int Length);

/// <summary>
/// Eine Stelle der Vorlage, die beim Erzeugen hochgezählt wird. Alle Fundstellen mit gleicher Zeichenfolge (z. B. <c>idea_1</c> an drei Stellen) bilden einen Punkt.
/// Der Wert im Durchlauf <c>i</c> und der Wiederholung <c>j</c> einer Wiederholungsgruppe ist <c>Start + Step * i + InnerStep * j</c>.
/// </summary>
public sealed class VariationPoint
{
    /// <summary>Stabile Kennung, mit der Änderungen des Nutzers einer neuen Analyse desselben Snippets zugeordnet werden.</summary>
    public string Key { get; init; } = string.Empty;

    public PointKind Kind { get; init; }

    /// <summary>Anzeige für die Prüfliste, z. B. <c>idea_1</c> oder <c>cost = 10</c>.</summary>
    public string Label { get; init; } = string.Empty;

    public List<PointOccurrence> Occurrences { get; init; } = new();

    /// <summary>Index der innersten Wiederholungsgruppe, in der die Fundstellen liegen; -1 = keine.</summary>
    public int RepeatGroup { get; init; } = -1;

    public bool Enabled { get; set; }

    public long Start { get; set; }

    /// <summary>Schrittweite von einem Durchlauf zum nächsten.</summary>
    public long Step { get; set; } = 1;

    /// <summary>Schrittweite von einer Wiederholung der Gruppe zur nächsten; nur bei <see cref="RepeatGroup"/> &gt;= 0 wirksam.</summary>
    public long InnerStep { get; set; }

    /// <summary>Mindestbreite mit führenden Nullen (<c>0001</c> = 4); 0 = keine Auffüllung.</summary>
    public int Width { get; set; }

    /// <summary>Die Beispiele im Snippet zählten uneinheitlich; der Punkt ist deshalb ausgeschaltet und wird in der Prüfliste markiert.</summary>
    public bool Irregular { get; set; }

    /// <summary>Die Beispiele im Snippet hatten an dieser Stelle immer denselben Wert; der Punkt ist deshalb ausgeschaltet.</summary>
    public bool ConstantInExamples { get; set; }

    /// <summary>Der Bezeichner enthält mehrere Zahlen und dieser Block zählt nach der Regel nicht (z. B. die Stufe in <c>ARA_1_1</c>); der Punkt ist deshalb standardmäßig aus.</summary>
    public bool RuleDisabled { get; set; }

    /// <summary>Grund-Text ("Regel „Name“ (Ebene)"), wenn eine Regel diesen Punkt einschaltet; sonst leer.</summary>
    public string CountedByRule { get; init; } = string.Empty;

    /// <summary>Form des ersten Bezeichners, in dem der Punkt steht, mit <c>#</c> statt der Zahlen (<c>iv#_ga_ARA_#</c>); Schlüssel für eigene Regeln.</summary>
    public string Shape { get; init; } = string.Empty;

    /// <summary>Der Punkt zählt, weil die Beispiele im Snippet eine gleichmäßige Zählung zeigten.</summary>
    public bool LearnedFromExamples { get; init; }

    /// <summary>Grund, aus dem der Punkt standardmäßig nicht zählt, weil er an einer ignorierten Stelle steht (siehe <see cref="IgnoreRules"/>); sonst leer.</summary>
    public string IgnoredReason { get; init; } = string.Empty;

    /// <summary>Anzahl der Zahlen in diesem Bezeichner.</summary>
    public int RunCount { get; init; } = 1;

    /// <summary>Die wievielte Zahl (ab 0, von links) des Bezeichners dieser Punkt ist.</summary>
    public int RunIndex { get; init; }

    /// <summary>Für die Form dieses Bezeichners gilt eine eigene Regel.</summary>
    public bool HasCustomRule { get; init; }

    public string Format(int instance, int repeat)
    {
        var value = Start + Step * instance + InnerStep * repeat;
        var digits = Math.Abs(value).ToString(CultureInfo.InvariantCulture).PadLeft(Width, '0');
        return value < 0 ? "-" + digits : digits;
    }
}

/// <summary>
/// Ein Unterblock, der im Snippet mehrfach gleichförmig vorkam (z. B. drei <c>option = { … }</c>). Der Vorlagentext enthält nur das erste Exemplar,
/// <see cref="Count"/> bestimmt, wie oft es beim Erzeugen wiederholt wird.
/// </summary>
public sealed class RepeatGroup
{
    public int Start { get; init; }

    public int End { get; init; }

    public int Count { get; set; }

    /// <summary>Text, der die Exemplare im Snippet trennte (meist Zeilenumbruch mit Einrückung); steht zwischen den Wiederholungen.</summary>
    public string Separator { get; init; } = "\n";
}

/// <summary>Ergebnis der Analyse: der Vorlagentext (Beispiele zusammengefasst) samt Zählpunkten und Wiederholungsgruppen.</summary>
public sealed class TemplateModel
{
    public string Text { get; init; } = string.Empty;

    public List<VariationPoint> Points { get; init; } = new();

    public List<RepeatGroup> Groups { get; init; } = new();

    /// <summary>Zeilenumbruch des Snippets (<c>\n</c> oder <c>\r\n</c>); wird beim Erzeugen wiederverwendet.</summary>
    public string Newline { get; init; } = "\n";

    /// <summary>Für jede Position im Vorlagentext die Position im eingegebenen Snippet (für Markierungen im Editor); -1 = keine Entsprechung.</summary>
    public int[] SourceOffsets { get; init; } = Array.Empty<int>();

    /// <summary>Die Formen von Bezeichnern mit mehreren Zahlen, für die in der Prüfliste einzelne Zahlen an- oder abgehakt werden können.</summary>
    public List<ShapeInfo> Shapes { get; init; } = new();

    /// <summary>Anzahl der Beispiele, die das Snippet enthielt und die zusammengefasst wurden (größte Anzahl); 0, wenn keine erkannt wurden.</summary>
    public int ExampleCount { get; init; }

    /// <summary>Je Regelkennung die Zahl der Ziffernblöcke im Vorlagentext, über die die Regel entschieden hat (für die Spalte "Treffer im Snippet").</summary>
    public IReadOnlyDictionary<string, int> RuleHits { get; set; } = new Dictionary<string, int>();
}

/// <summary>Änderung des Nutzers an einem Zählpunkt; wird über <see cref="VariationPoint.Key"/> einer neuen Analyse zugeordnet.</summary>
public sealed record PointOverride(string Key, bool? Enabled = null, long? Start = null, long? Step = null, long? InnerStep = null);

/// <summary>Eine Form von Bezeichnern mit mehreren Zahlen (<c>iv#_ga_ARA_#_#</c>): die Zahlen des ersten Vorkommens und ob sie zählen.</summary>
public sealed record ShapeInfo(string Shape, IReadOnlyList<string> Digits, IReadOnlyList<bool> Counted, bool HasCustomRule);
