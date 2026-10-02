using System.Globalization;
using System.Text;

namespace ParadoxModTool.GuiDesign.Blocks;

/// <summary>Im Rumpf eines Entwurfs benutzter Schlüssel: Parameter oder Wiederholung.</summary>
public sealed record GuiBlockDraftKey(string Key, bool IsRepeat);

public sealed record GuiBlockDraftParameter(string Key, string Label, string Default, GuiBlockParameterKind Kind);

public sealed record GuiBlockDraftRepeat(string Key, string Label, int Count);

/// <summary>
/// Entwurf eines eigenen Bausteins ("Als Baustein speichern"): Code übernehmen, die vom Modder geschriebenen Platzhalter und Wiederholungen
/// erkennen und den Dateikopf zusammensetzen.
/// </summary>
public static class GuiBlockDraft
{
    /// <summary>
    /// Code als Rumpf übernehmen: Zeilenenden <c>\n</c>, erste Zeile ohne führenden Leerraum, die übrigen um ihre kleinste gemeinsame Einrückung
    /// gekürzt, <c>$</c> als <c>$$</c>.
    /// </summary>
    public static string FromCode(string code)
    {
        var lines = code.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        lines[0] = lines[0].TrimStart(' ', '\t');
        var indents = lines.Skip(1)
            .Where(l => l.Trim().Length > 0)
            .Select(l => l.Length - l.TrimStart(' ', '\t').Length)
            .ToList();
        var cut = indents.Count == 0 ? 0 : indents.Min();
        for (var i = 1; i < lines.Length; i++)
        {
            lines[i] = lines[i].Trim().Length == 0 ? string.Empty : lines[i][cut..];
        }

        return string.Join("\n", lines).Replace("$", "$$", StringComparison.Ordinal);
    }

    /// <summary>
    /// Schlüssel im Rumpf in Reihenfolge des ersten Auftretens; eine Wiederholung steht vor ihren Parametern, fehlende äußere Wiederholungen werden
    /// ergänzt. <c>index</c> und kaputte Stellen zählen nicht.
    /// </summary>
    public static IReadOnlyList<GuiBlockDraftKey> KeysIn(string body)
    {
        var keys = new List<GuiBlockDraftKey>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddRepeat(string key)
        {
            var parent = GuiBlockParser.ParentOf(key);
            if (parent.Length > 0)
            {
                AddRepeat(parent);
            }

            if (seen.Add(key))
            {
                keys.Add(new GuiBlockDraftKey(key, true));
            }
        }

        foreach (var line in body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            foreach (var token in GuiBlockParser.Tokenize(line))
            {
                switch (token.Kind)
                {
                    case GuiBlockTokenKind.RepeatStart:
                        AddRepeat(token.Value);
                        break;
                    case GuiBlockTokenKind.Placeholder when token.Value != GuiBlockParser.IndexKey:
                        var parent = GuiBlockParser.ParentOf(token.Value);
                        if (parent.Length > 0)
                        {
                            AddRepeat(parent);
                        }

                        if (seen.Add(token.Value))
                        {
                            keys.Add(new GuiBlockDraftKey(token.Value, false));
                        }

                        break;
                }
            }
        }

        return keys;
    }

    /// <summary>
    /// Setzt die Datei zusammen: <c>block</c>, <c>group</c> (leer = weglassen), <c>description</c>, Parameter oberster Ebene, dann je Wiederholung
    /// (Tiefensuche) ihre Zeile, ihre Parameter und ihre Unterwiederholungen; danach eine Leerzeile und der Rumpf. In Namen, Beschriftungen und
    /// Vorgaben werden <c>|</c> zu <c>/</c> und Zeilenumbrüche zu Leerzeichen; eine leere Beschriftung wird der Schlüssel.
    /// </summary>
    public static string Compose(
        string name,
        string group,
        string description,
        IReadOnlyList<GuiBlockDraftParameter> parameters,
        IReadOnlyList<GuiBlockDraftRepeat> repeats,
        string body)
    {
        var text = new StringBuilder();
        text.Append("# block: ").Append(Clean(name)).Append('\n');
        if (Clean(group).Length > 0)
        {
            text.Append("# group: ").Append(Clean(group)).Append('\n');
        }

        if (Clean(description).Length > 0)
        {
            text.Append("# description: ").Append(Clean(description)).Append('\n');
        }

        void AppendLevel(string parent)
        {
            foreach (var parameter in parameters.Where(p => GuiBlockParser.ParentOf(p.Key) == parent))
            {
                text.Append("# param: ").Append(parameter.Key)
                    .Append(" | ").Append(Label(parameter.Label, parameter.Key))
                    .Append(" | ").Append(Clean(parameter.Default));
                if (parameter.Kind == GuiBlockParameterKind.Modifier)
                {
                    text.Append(" | modifier");
                }

                text.Append('\n');
            }

            foreach (var repeat in repeats.Where(r => GuiBlockParser.ParentOf(r.Key) == parent))
            {
                text.Append("# repeat: ").Append(repeat.Key)
                    .Append(" | ").Append(Label(repeat.Label, repeat.Key))
                    .Append(" | ").Append(Math.Max(0, repeat.Count).ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
                AppendLevel(repeat.Key);
            }
        }

        AppendLevel(string.Empty);
        text.Append('\n').Append(body);
        return text.ToString();
    }

    private static string Label(string label, string key) => Clean(label) is { Length: > 0 } clean ? clean : key;

    private static string Clean(string text) =>
        text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ').Replace('|', '/').Trim();
}
