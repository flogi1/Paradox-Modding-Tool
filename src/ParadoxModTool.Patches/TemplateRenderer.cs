using System.Text.RegularExpressions;

namespace ParadoxModTool.Patches;

/// <summary>Eine gerenderte Instanz einer Vorlage; <see cref="Suffix"/> ist leer oder der Listenwert (mehrere mit Leerzeichen).</summary>
public sealed record RenderedInstance(string Suffix, string Text);

/// <summary>
/// Füllt Vorlagen: <c>{spalte}</c> setzt den Wert ein, <c>{?spalte}…{/spalte}</c> behält den Text nur bei nicht leerem Wert,
/// <c>{!spalte}…{/spalte}</c> nur bei leerem Wert, <c>{*spalte}…{/spalte}</c> wiederholt den Text je Listenwert. <c>{name}</c> ohne passende Spalte bleibt als Text stehen, weil Paradox-Skript
/// wie <c>{castle_01}</c> gültig ist; <see cref="UnknownPlaceholders"/> meldet solche Stellen als Warnung.
/// </summary>
public static class TemplateRenderer
{
    private const string Name = "[A-Za-z_][A-Za-z0-9_]*";

    private static readonly Regex Section = new(@"\{(?<op>[?!])(?<name>" + Name + @")\}(?<body>.*?)\{/\k<name>\}", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>
    /// <c>{*spalte}…{/spalte}</c>: der Text je Listenwert einmal, darin ist <c>{spalte}</c> der einzelne Wert. Wird vor den Bedingungen aufgelöst,
    /// damit ein Wiederholabschnitt in einer gleichnamigen Bedingung (<c>{?barony}…{*barony}…{/barony}…{/barony}</c>) sein eigenes Ende findet.
    /// </summary>
    private static readonly Regex Repeat = new(@"\{\*(?<name>" + Name + @")\}(?<body>.*?)\{/\k<name>\}", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex Placeholder = new(@"\{(?<name>" + Name + @")\}", RegexOptions.CultureInvariant);

    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        var text = template;
        while (true)
        {
            var replaced = Repeat.Replace(text, m =>
            {
                var name = m.Groups["name"].Value;
                var items = SplitList(values.TryGetValue(name, out var raw) ? raw : string.Empty);
                return string.Concat(items.Select(item => Render(m.Groups["body"].Value, With(values, name, item))));
            });
            replaced = Section.Replace(replaced, m =>
            {
                var filled = values.TryGetValue(m.Groups["name"].Value, out var value) && value.Trim().Length > 0;
                return filled == (m.Groups["op"].Value == "?") ? m.Groups["body"].Value : string.Empty;
            });
            if (replaced == text)
            {
                break;
            }

            text = replaced;
        }

        return Placeholder.Replace(text, m => values.TryGetValue(m.Groups["name"].Value, out var value) ? value : m.Value);
    }

    /// <summary>Eine Instanz je Wert jeder in der Vorlage genutzten Listen-Spalte (Kreuzprodukt bei mehreren); sonst genau eine.</summary>
    public static IReadOnlyList<RenderedInstance> RenderEach(string template, IReadOnlyDictionary<string, string> values, IReadOnlySet<string> listColumns)
    {
        // Listen in einem Wiederholabschnitt werden dort aufgelöst, nicht zu eigenen Instanzen.
        var used = UsedNames(Repeat.Replace(template, string.Empty)).Where(listColumns.Contains).Distinct(StringComparer.Ordinal).ToList();
        var combinations = new List<List<(string Column, string Value)>> { new() };
        foreach (var column in used)
        {
            var items = SplitList(values.TryGetValue(column, out var raw) ? raw : string.Empty);
            combinations = combinations.SelectMany(c => items.Select(item => new List<(string, string)>(c) { (column, item) })).ToList();
        }

        var result = new List<RenderedInstance>();
        foreach (var combination in combinations)
        {
            var instanceValues = new Dictionary<string, string>(values.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal);
            foreach (var (column, value) in combination)
            {
                instanceValues[column] = value;
            }

            result.Add(new RenderedInstance(string.Join(" ", combination.Select(c => c.Value)), Render(template, instanceValues)));
        }

        return result;
    }

    /// <summary>
    /// Namen in <c>{name}</c> sowie in Abschnitten <c>{?name}</c>, <c>{!name}</c>, <c>{*name}</c> und <c>{/name}</c>, die keine Spalte sind
    /// (<c>key</c> ist immer eine), in Reihenfolge des ersten Auftretens. Ein Tippfehler im Abschnitt ließe ihn sonst still als Text stehen.
    /// </summary>
    public static IReadOnlyList<string> UnknownPlaceholders(string template, IEnumerable<string> columnNames)
    {
        var known = new HashSet<string>(columnNames, StringComparer.Ordinal) { "key" };
        return AnyName.Matches(template).Select(m => m.Groups["name"].Value).Where(n => !known.Contains(n)).Distinct(StringComparer.Ordinal).ToList();
    }

    private static readonly Regex AnyName = new(@"\{[?!*/]?(?<name>" + Name + @")\}", RegexOptions.CultureInvariant);

    /// <summary>Platzhalter außerhalb von <c>{?…}</c>/<c>{!…}</c>-Abschnitten, deren Wert für diese Zeile leer ist.</summary>
    public static IReadOnlyList<string> EmptyPlaceholders(string template, IReadOnlyDictionary<string, string> values)
    {
        var withoutRepeats = Repeat.Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            var first = SplitList(values.TryGetValue(name, out var raw) ? raw : string.Empty).FirstOrDefault();
            return first is null ? string.Empty : m.Groups["body"].Value.Replace("{" + name + "}", first, StringComparison.Ordinal);
        });
        var withoutSections = Section.Replace(withoutRepeats, m =>
        {
            var filled = values.TryGetValue(m.Groups["name"].Value, out var value) && value.Trim().Length > 0;
            return filled == (m.Groups["op"].Value == "?") ? m.Groups["body"].Value : string.Empty;
        });
        return Placeholder.Matches(withoutSections)
            .Select(m => m.Groups["name"].Value)
            .Where(name => values.TryGetValue(name, out var value) && value.Trim().Length == 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<string> SplitList(string raw) =>
        raw.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToList();

    private static IEnumerable<string> UsedNames(string template) =>
        Placeholder.Matches(template).Select(m => m.Groups["name"].Value)
            .Concat(Section.Matches(template).Select(m => m.Groups["name"].Value))
            .Concat(Repeat.Matches(template).Select(m => m.Groups["name"].Value));

    private static Dictionary<string, string> With(IReadOnlyDictionary<string, string> values, string name, string value) =>
        new(values.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal) { [name] = value };
}
