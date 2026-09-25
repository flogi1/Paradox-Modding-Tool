using System.Text;
using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// Schreibt ein aufgelöstes Element als einen Codeblock ohne Verweise ("Aufgelöster Code" im Inspektor): Typkette, Templates und
/// blockoverrides sind eingesetzt. Typ-Instanzen erscheinen als ihr Engine-Widget, die Typkette steht als Kommentar dahinter; geerbte
/// Eigenschaften tragen ihre Herkunft als Kommentar. Innerhalb eines Elements stehen die Eigenschaften vor den Kind-Elementen
/// (<see cref="ResolvedElement"/> hält beide getrennt, die ursprüngliche Mischung ist nicht mehr bekannt; Plan-Ruling 9).
/// </summary>
public static class GuiResolvedCode
{
    public static string Write(ResolvedElement element, bool withOrigins = true)
    {
        var builder = new StringBuilder();
        WriteElement(builder, element, 0, withOrigins);
        return builder.ToString();
    }

    private static void WriteElement(StringBuilder builder, ResolvedElement element, int depth, bool withOrigins)
    {
        var indent = new string('\t', depth);
        var key = element.Role == GuiRole.TypeInstance ? element.BaseWidget : element.Key;
        builder.Append(indent).Append(key).Append(" = {");
        var chain = element.TypeChain.Distinct(StringComparer.Ordinal).ToList();
        if (chain.Count > 1)
        {
            builder.Append(" # ").Append(string.Join(" → ", chain));
        }

        builder.Append('\n');
        foreach (var property in element.Properties)
        {
            WriteProperty(builder, property, depth + 1, withOrigins);
        }

        foreach (var child in element.Children)
        {
            WriteElement(builder, child, depth + 1, withOrigins);
        }

        builder.Append(indent).Append("}\n");
    }

    private static void WriteProperty(StringBuilder builder, ResolvedProperty property, int depth, bool withOrigins)
    {
        var indent = new string('\t', depth);
        var origin = withOrigins ? OriginComment(property.Origin) : string.Empty;
        switch (property.Role)
        {
            case GuiRole.StructuredBlock:
                var lines = property.Value.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
                if (lines.Count == 0)
                {
                    builder.Append(indent).Append(property.Name).Append(" = { }").Append(origin).Append('\n');
                }
                else if (lines.Count == 1 && !lines[0].Contains('#'))
                {
                    builder.Append(indent).Append(property.Name).Append(" = { ").Append(lines[0]).Append(" }").Append(origin).Append('\n');
                }
                else
                {
                    // Mehrzeilig (oder mit Kommentar, der bis zum Zeilenende reicht): je Zeile eingerückt, schließende Klammer auf eigener Zeile.
                    builder.Append(indent).Append(property.Name).Append(" = {").Append(origin).Append('\n');
                    foreach (var line in lines)
                    {
                        builder.Append(indent).Append('\t').Append(line).Append('\n');
                    }

                    builder.Append(indent).Append("}\n");
                }

                break;

            case GuiRole.ValueBlock:
                var items = string.Join(" ", property.Items.Select(item => NeedsQuotes(item) ? $"\"{item}\"" : item));
                builder.Append(indent).Append(property.Name).Append(" = { ").Append(items).Append(items.Length > 0 ? " }" : "}").Append(origin).Append('\n');
                break;

            default:
                var value = property.IsQuoted || NeedsQuotes(property.Value) ? $"\"{property.Value}\"" : property.Value;
                builder.Append(indent).Append(property.Name).Append(" = ").Append(value).Append(origin).Append('\n');
                break;
        }
    }

    /// <summary>Unquotierte Werte (z.B. aus Konstanten eingesetzt) mit Leerraum oder Syntaxzeichen würden beim Neu-Parsen zerfallen.</summary>
    private static bool NeedsQuotes(string value) =>
        value.Length == 0 || value.Any(c => char.IsWhiteSpace(c) || c is '#' or '{' or '}' or '=');

    private static string OriginComment(GuiOrigin origin) => origin.Kind == GuiOriginKind.Here
        ? string.Empty
        : origin.FilePath is null
            ? $" # {origin.Definition}"
            : $" # {origin.Definition} ({Path.GetFileName(origin.FilePath)}:{origin.Line})";
}
