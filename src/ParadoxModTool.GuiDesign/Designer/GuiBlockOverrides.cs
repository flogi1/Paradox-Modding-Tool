using System.Text;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>Block, über den sich ein geerbtes Element anpassen lässt: Name, Knoten mit dem wirksamen Inhalt und die Definition, aus der er stammt.</summary>
public sealed record GuiBlockSource(string BlockName, GuiNode Owner, string DefinitionLabel);

/// <summary>
/// blockoverride als Werkzeug zum Anpassen von Typen an einer Instanz (Plan-Ruling 14): welcher Inhalt eines Blocks an einer Typ-Instanz gerade
/// wirkt, in welchem Block ein geerbtes Element liegt und wie ein blockoverride mit übernommenem Inhalt aussieht. Vorrang wie im Resolver:
/// blockoverride des am weitesten abgeleiteten Typs der Kette vor dem block selbst.
/// </summary>
public static class GuiBlockOverrides
{
    /// <summary>Knoten, dessen Inhalt für <c>block "name"</c> an dieser Instanz gerade wirkt; null, wenn die Typkette den Block nicht hat.</summary>
    public static GuiNode? EffectiveContent(GuiNode instance, string blockName, GuiResolver resolver)
    {
        var chain = resolver.TypeChain(instance.Name);
        var definitions = chain.Take(chain.Count - 1).Select(resolver.FindType).OfType<GuiNode>().Distinct().ToList();
        foreach (var definition in definitions)
        {
            if (definition.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.BlockOverride && c.Name == blockName) is { } overridden)
            {
                return overridden;
            }
        }

        return definitions.SelectMany(d => d.Descendants()).FirstOrDefault(n => n.Kind == GuiNodeKind.Block && n.Name == blockName);
    }

    /// <summary>Das Element stammt nicht aus der Datei der Instanz (sondern aus einem Typ oder Template anderswo).</summary>
    public static bool IsInherited(ResolvedElement element, GuiNode instance) => !ReferenceEquals(element.Origin.Node.Root, instance.Root);

    /// <summary>Nächster block oder blockoverride über der Herkunft des Elements in seiner Definition; null, wenn es in keinem liegt.</summary>
    public static GuiBlockSource? ContainingBlock(ResolvedElement element)
    {
        for (var node = element.Origin.Node.Parent; node is not null; node = node.Parent)
        {
            if (node.Kind is GuiNodeKind.Block or GuiNodeKind.BlockOverride)
            {
                return new GuiBlockSource(node.Name, node, DefinitionLabelOf(node));
            }

            if (node.Kind is GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition or GuiNodeKind.Document)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Beschriftung der Typ- oder Template-Definition, in der der Knoten steht (z.B. <c>type x = y</c>); "" außerhalb.</summary>
    public static string DefinitionLabelOf(GuiNode node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.Kind is GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition)
            {
                return GuiElementTree.LabelOf(current);
            }
        }

        return string.Empty;
    }

    /// <summary>Inhalt zwischen den Klammern eines block oder blockoverride, relativ eingerückt (siehe <see cref="Relative"/>).</summary>
    public static string ContentOf(GuiNode owner, GuiResolver resolver)
    {
        if (owner.OpenBrace < 0 || resolver.TextOf(owner) is not { } text)
        {
            return string.Empty;
        }

        var start = owner.OpenBrace - owner.Span.Start + 1;
        var end = (owner.CloseBrace >= 0 ? owner.CloseBrace : owner.Span.End) - owner.Span.Start;
        return Relative(text[start..Math.Max(start, end)]);
    }

    /// <summary>
    /// Blockinhalt als Schnipsel: leere Zeilen am Anfang und Ende weg, gemeinsame Einrückung abgezogen (übrige führende Tabs bleiben und
    /// werden beim Einfügen zu Einrückungseinheiten); ein Inhalt auf der Zeile der "{" kommt als erste Zeile.
    /// </summary>
    public static string Relative(string body)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n').Select(line => line.TrimEnd()).ToList();
        var first = lines[0].Trim();
        var rest = lines.Skip(1).ToList();
        while (rest.Count > 0 && rest[0].Length == 0)
        {
            rest.RemoveAt(0);
        }

        while (rest.Count > 0 && rest[^1].Length == 0)
        {
            rest.RemoveAt(rest.Count - 1);
        }

        var prefix = CommonIndent(rest.Where(line => line.Length > 0));
        var result = rest.Select(line => line.Length == 0 ? line : line[prefix.Length..]).ToList();
        if (first.Length > 0)
        {
            result.Insert(0, first);
        }

        return string.Join("\n", result);
    }

    /// <summary>Code <c>blockoverride "name" { … }</c> mit dem Inhalt eine Ebene tiefer; leerer Inhalt ergibt nur die Klammern.</summary>
    public static string Snippet(string blockName, string content)
    {
        var builder = new StringBuilder($"blockoverride \"{blockName}\" {{");
        if (content.Length > 0)
        {
            foreach (var line in content.Replace("\r\n", "\n").Split('\n'))
            {
                builder.Append('\n').Append(line.Length == 0 ? string.Empty : "\t" + line);
            }
        }

        builder.Append("\n}");
        return builder.ToString();
    }

    private static string CommonIndent(IEnumerable<string> lines)
    {
        string? prefix = null;
        foreach (var line in lines)
        {
            var indent = line[..(line.Length - line.TrimStart(' ', '\t').Length)];
            if (prefix is null)
            {
                prefix = indent;
                continue;
            }

            var length = 0;
            while (length < prefix.Length && length < indent.Length && prefix[length] == indent[length])
            {
                length++;
            }

            prefix = prefix[..length];
        }

        return prefix ?? string.Empty;
    }
}
