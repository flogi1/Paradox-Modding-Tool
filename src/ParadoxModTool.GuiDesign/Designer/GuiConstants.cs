using System.Text;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// @-Konstanten beim Verschieben einer Definition in eine andere Datei (Konstanten gelten nur in ihrer Datei): welche die Definition braucht
/// und wie Verweise umbenannt werden. Verweise sind ganze Werte <c>@name</c> (auch Listeneinträge wie <c>{ @w 20 }</c>) und Namen in
/// <c>@[ … ]</c>; Werte in Anführungszeichen zählen nicht.
/// </summary>
public static class GuiConstants
{
    /// <summary>Namen, auf die ein Wert verweist.</summary>
    public static IReadOnlyList<string> NamesIn(string value)
    {
        if (value.StartsWith("@[", StringComparison.Ordinal) && value.EndsWith(']'))
        {
            var names = new List<string>();
            ForEachIdentifier(value[2..^1], (start, length) => names.Add(value.Substring(2 + start, length)));
            return names;
        }

        return value.Length > 1 && value[0] == '@' && IsIdentifier(value[1..]) ? new[] { value[1..] } : Array.Empty<string>();
    }

    /// <summary>
    /// Konstanten außerhalb der Definition, die sie (auch über andere Konstanten) braucht; Abhängigkeiten zuerst. Konstanten in der Definition
    /// selbst wandern mit ihr und fehlen hier; Namen ohne Konstante werden übergangen.
    /// </summary>
    /// <param name="home">
    /// Die Definition in der Datei, aus der ihr Text stammt (Mod-Fassung in einem Vanilla-Entwurf): Namen der Definition gelten zuerst dort, erst
    /// danach in der Datei von <paramref name="definition"/>. Konstanten gelten nur in ihrer Datei.
    /// </param>
    public static IReadOnlyList<GuiNode> RequiredBy(GuiNode definition, GuiNode? home = null)
    {
        var result = new List<GuiNode>();
        var seen = new HashSet<GuiNode>();

        void Visit(GuiNode at, string name, bool fromDefinition)
        {
            var constant = Find(name, at);
            if (constant is not null && constant.Ancestors().Contains(definition))
            {
                return;
            }

            if (fromDefinition && home is not null && Find(name, home) is { } own)
            {
                constant = own;
            }

            if (constant is null || !seen.Add(constant))
            {
                return;
            }

            foreach (var inner in NamesIn(constant.Value ?? string.Empty))
            {
                Visit(constant, inner, false);
            }

            result.Add(constant);
        }

        foreach (var node in new[] { definition }.Concat(definition.Descendants()))
        {
            if (node.Value is { } value && !node.IsQuoted)
            {
                foreach (var name in NamesIn(value))
                {
                    Visit(node, name, true);
                }
            }
        }

        return result;
    }

    /// <summary>Ersetzt Verweise auf umbenannte Konstanten (<c>@alt</c> und Namen in <c>@[ … ]</c>), außerhalb von Zeichenketten und Kommentaren.</summary>
    public static string Rename(string text, IReadOnlyDictionary<string, string> renames)
    {
        if (renames.Count == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '"')
            {
                var end = text.IndexOf('"', i + 1);
                end = end < 0 ? text.Length : end + 1;
                builder.Append(text, i, end - i);
                i = end;
            }
            else if (c == '#')
            {
                var end = text.IndexOf('\n', i);
                end = end < 0 ? text.Length : end;
                builder.Append(text, i, end - i);
                i = end;
            }
            else if (c == '@' && i + 1 < text.Length && text[i + 1] == '[')
            {
                var end = text.IndexOf(']', i);
                var newline = text.IndexOf('\n', i);
                if (end < 0 || (newline >= 0 && newline < end))
                {
                    builder.Append(c);
                    i++;
                    continue;
                }

                var inner = text[(i + 2)..end];
                var renamed = new StringBuilder();
                var last = 0;
                ForEachIdentifier(inner, (start, length) =>
                {
                    renamed.Append(inner, last, start - last);
                    var name = inner.Substring(start, length);
                    renamed.Append(renames.TryGetValue(name, out var to) ? to : name);
                    last = start + length;
                });
                renamed.Append(inner, last, inner.Length - last);
                builder.Append("@[").Append(renamed).Append(']');
                i = end + 1;
            }
            else if (c == '@')
            {
                var end = i + 1;
                while (end < text.Length && IsIdentifierChar(text[end]))
                {
                    end++;
                }

                var name = text[(i + 1)..end];
                builder.Append('@').Append(renames.TryGetValue(name, out var to) ? to : name);
                i = end;
            }
            else
            {
                builder.Append(c);
                i++;
            }
        }

        return builder.ToString();
    }

    /// <summary>Konstante im nächsten umschließenden Gültigkeitsbereich (wie der Resolver).</summary>
    private static GuiNode? Find(string name, GuiNode at)
    {
        foreach (var scope in at.Ancestors())
        {
            if (scope.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.ConstantDefinition && c.Name == name) is { } constant)
            {
                return constant;
            }
        }

        return null;
    }

    /// <summary>Bezeichner in einem Ausdruck, ohne Zahlen und ohne Glieder nach oder vor einem Punkt (Datenbindungen wie <c>A.B</c>).</summary>
    private static void ForEachIdentifier(string expression, Action<int, int> found)
    {
        var i = 0;
        while (i < expression.Length)
        {
            if (!IsIdentifierChar(expression[i]))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < expression.Length && IsIdentifierChar(expression[i]))
            {
                i++;
            }

            var before = start > 0 ? expression[start - 1] : ' ';
            var after = i < expression.Length ? expression[i] : ' ';
            if (!char.IsAsciiDigit(expression[start]) && before != '.' && after is not ('.' or '('))
            {
                found(start, i - start);
            }
        }
    }

    private static bool IsIdentifier(string text) => text.Length > 0 && !char.IsAsciiDigit(text[0]) && text.All(IsIdentifierChar);

    private static bool IsIdentifierChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
}
