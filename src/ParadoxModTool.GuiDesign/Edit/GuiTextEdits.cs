using System.Text;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Edit;

/// <summary>
/// Designer-Aktionen als minimale Textersetzungen gegen den Originaltext. Einrückung (Tab oder Leerzeichen) und Zeilenende werden aus
/// der Datei übernommen. Außerhalb der Ersetzung bleibt der Text byte-identisch. Ergebnis leer = nichts zu tun.
/// </summary>
public sealed class GuiTextEdits
{
    private readonly string _text;

    public GuiTextEdits(GuiDocument document)
    {
        _text = document.Text;
        Newline = _text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        IndentUnit = DetectIndentUnit(_text);
    }

    public string Newline { get; }

    public string IndentUnit { get; }

    public IReadOnlyList<TextEdit> SetProperty(GuiNode element, string name, string valueText)
    {
        RequireBody(element);
        var existing = element.Children.LastOrDefault(c => c.Name == name && c.Kind is GuiNodeKind.Property or GuiNodeKind.KeyedBlock);
        if (existing is null)
        {
            var anchor = element.Children.LastOrDefault(IsPropertyLike);
            return anchor is null
                ? InsertChild(element, $"{name} = {valueText}", InsertPosition.FirstChild)
                : InsertChild(element, $"{name} = {valueText}", InsertPosition.After, anchor);
        }

        return SetValue(existing, valueText);
    }

    /// <summary>Ersetzt den Wert genau dieses Knotens (Property oder Block-Eigenschaft); derselbe Wert ergibt keine Ersetzung.</summary>
    public IReadOnlyList<TextEdit> SetValue(GuiNode property, string valueText)
    {
        int start;
        int end;
        if (property.Kind == GuiNodeKind.Property)
        {
            if (property.ValueSpan is not { } value)
            {
                return new[] { new TextEdit(property.Span.End, 0, " " + valueText) };
            }

            start = value.Start;
            end = value.End;
        }
        else if (property.Kind == GuiNodeKind.KeyedBlock && property.OpenBrace >= 0)
        {
            start = property.OpenBrace;
            end = property.Span.End;
        }
        else
        {
            throw new ArgumentException($"\"{property.Name}\" ist keine Eigenschaft.", nameof(property));
        }

        return _text[start..end] == valueText
            ? Array.Empty<TextEdit>()
            : new[] { new TextEdit(start, end - start, valueText) };
    }

    /// <summary>
    /// Verschiebt einen Knoten, der allein auf seinen Zeilen steht: seine Zeilen entfernen und ihn neu eingerückt am Ziel einfügen, als zwei
    /// Ersetzungen in dieser Reihenfolge (bei gleichem Offset muss das Entfernen zuerst angewendet werden, siehe
    /// <see cref="TextEdit.InApplyOrder"/>). Leer, wenn das Ziel im Knoten selbst liegt, der Knoten schon dort steht oder nicht allein auf
    /// seinen Zeilen steht.
    /// </summary>
    public IReadOnlyList<TextEdit> MoveNode(GuiNode node, GuiNode newParent, InsertPosition position, GuiNode? anchor = null)
    {
        if (node.Parent is null
            || !IsOwnLines(node)
            || ReferenceEquals(newParent, node)
            || ReferenceEquals(anchor, node)
            || newParent.Ancestors().Any(a => ReferenceEquals(a, node))
            || IsSamePlace(node, newParent, position, anchor))
        {
            return Array.Empty<TextEdit>();
        }

        var removal = NodeLines(node);
        var insertion = InsertChild(newParent, Dedent(node), position, anchor);
        return new[] { new TextEdit(removal.Start, removal.Length, string.Empty) }.Concat(insertion).ToList();
    }

    private static bool IsSamePlace(GuiNode node, GuiNode parent, InsertPosition position, GuiNode? anchor)
    {
        if (!ReferenceEquals(node.Parent, parent))
        {
            return false;
        }

        var siblings = parent.Children.Where(c => c.Kind != GuiNodeKind.Comment).ToList();
        var index = siblings.IndexOf(node);
        return position switch
        {
            InsertPosition.FirstChild => index == 0,
            InsertPosition.LastChild => index == siblings.Count - 1,
            InsertPosition.Before => index + 1 < siblings.Count && ReferenceEquals(siblings[index + 1], anchor),
            _ => index > 0 && ReferenceEquals(siblings[index - 1], anchor),
        };
    }

    /// <summary>Text des Knotens als Schnipsel für <see cref="Reindent"/>: Einrückung seiner ersten Zeile abgezogen, jede weitere Einheit als Tab.</summary>
    public string Dedent(GuiNode node)
    {
        var baseIndent = IndentAt(node.Span.Start);
        var lines = _text[node.Span.Start..node.Span.End].Replace("\r\n", "\n").Split('\n');
        var builder = new StringBuilder(lines[0]);
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].StartsWith(baseIndent, StringComparison.Ordinal) ? lines[i][baseIndent.Length..] : lines[i].TrimStart(' ', '\t');
            var tabs = 0;
            while (line.StartsWith(IndentUnit, StringComparison.Ordinal))
            {
                line = line[IndentUnit.Length..];
                tabs++;
            }

            builder.Append('\n').Append('\t', tabs).Append(line);
        }

        return builder.ToString();
    }

    public IReadOnlyList<TextEdit> RemoveNode(GuiNode node)
    {
        if (IsOwnLines(node))
        {
            var lines = NodeLines(node);
            return new[] { new TextEdit(lines.Start, lines.Length, string.Empty) };
        }

        var end = node.Span.End;
        if (end < _text.Length && _text[end] == ' ')
        {
            end++;
        }

        return new[] { new TextEdit(node.Span.Start, end - node.Span.Start, string.Empty) };
    }

    public IReadOnlyList<TextEdit> InsertChild(GuiNode parent, string snippet, InsertPosition position, GuiNode? anchor = null)
    {
        RequireBody(parent);
        if (position is InsertPosition.Before or InsertPosition.After && (anchor is null || anchor.Parent != parent))
        {
            throw new ArgumentException("Der Anker muss ein Kind des Elternknotens sein.", nameof(anchor));
        }

        var indent = ChildIndent(parent);
        var body = Reindent(snippet, indent);
        return position switch
        {
            InsertPosition.Before => InsertBefore(anchor!, body, indent),
            InsertPosition.After => InsertAfter(anchor!, body, indent),
            InsertPosition.FirstChild => parent.Children.Count == 0 ? InsertIntoEmpty(parent, body) : InsertBefore(parent.Children[0], body, indent),
            _ => parent.Children.Count == 0 ? InsertIntoEmpty(parent, body) : InsertAfter(parent.Children[^1], body, indent),
        };
    }

    public IReadOnlyList<TextEdit> DuplicateNode(GuiNode node)
    {
        if (!IsOwnLines(node))
        {
            return new[] { new TextEdit(node.Span.End, 0, " " + _text[node.Span.Start..node.Span.End]) };
        }

        var lines = NodeLines(node);
        var copy = _text[lines.Start..lines.End];
        return new[] { new TextEdit(lines.End, 0, copy.EndsWith('\n') ? copy : Newline + copy) };
    }

    public IReadOnlyList<TextEdit> MoveUp(GuiNode node) => Swap(Sibling(node, -1), node);

    public IReadOnlyList<TextEdit> MoveDown(GuiNode node) => Swap(node, Sibling(node, +1));

    public IReadOnlyList<TextEdit> WrapIn(GuiNode node, string boxKey)
    {
        if (!IsOwnLines(node))
        {
            return new[] { new TextEdit(node.Span.Start, node.Span.Length, $"{boxKey} = {{ {_text[node.Span.Start..node.Span.End]} }}") };
        }

        var lines = NodeLines(node);
        var indent = IndentAt(node.Span.Start);
        var inner = _text[lines.Start..lines.End];
        var newlineLength = inner.EndsWith("\r\n", StringComparison.Ordinal) ? 2 : inner.EndsWith('\n') ? 1 : 0;
        var core = inner[..^newlineLength];
        var indented = string.Join("\n", core.Split('\n').Select(line => line.TrimEnd('\r').Length == 0 ? line : IndentUnit + line));
        var replacement = indent + boxKey + " = {" + Newline + indented + Newline + indent + "}" + (newlineLength > 0 ? Newline : string.Empty);
        return new[] { new TextEdit(lines.Start, lines.Length, replacement) };
    }

    public IReadOnlyList<TextEdit> AddBlockOverride(GuiNode instance, string blockName, string contentSnippet)
    {
        var inner = string.Join("\n", contentSnippet.Replace("\r\n", "\n").Split('\n').Select(line => line.Length == 0 ? line : "\t" + line));
        return InsertChild(instance, $"blockoverride \"{blockName}\" {{\n{inner}\n}}", InsertPosition.LastChild);
    }

    public IReadOnlyList<TextEdit> RenameKey(GuiNode node, string newKey) =>
        new[] { new TextEdit(node.NameSpan.Start, node.NameSpan.Length, newKey) };

    /// <summary>Einrückung für Kinder: die des ersten Kinds auf eigener Zeile, sonst Einrückung des Elternknotens plus eine Einheit.</summary>
    public string ChildIndent(GuiNode parent)
    {
        foreach (var child in parent.Children)
        {
            if (OnlyWhitespaceBefore(child.Span.Start))
            {
                return IndentAt(child.Span.Start);
            }
        }

        return parent.Kind == GuiNodeKind.Document ? string.Empty : IndentAt(parent.Span.Start) + IndentUnit;
    }

    /// <summary>Einrückung der Zeile, in der der Knoten beginnt.</summary>
    public string IndentOf(GuiNode node) => IndentAt(node.Span.Start);

    /// <summary>
    /// Ersatz für die Spanne des Knotens: <paramref name="snippet"/> (Einrückung als Tabs, siehe <see cref="Dedent"/>) mit der Einrückung und
    /// den Zeilenenden dieser Datei; die erste Zeile ohne Einrückung, weil die Spanne erst nach ihr beginnt.
    /// </summary>
    public string ReplacementFor(GuiNode node, string snippet)
    {
        var indent = IndentOf(node);
        var body = Reindent(snippet, indent);
        return body.StartsWith(indent, StringComparison.Ordinal) ? body[indent.Length..] : body;
    }

    /// <summary>Die ganzen Zeilen eines Knotens (mit Zeilenende), wenn er allein auf ihnen steht; sonst nur seine Spanne.</summary>
    public TextSpan NodeLines(GuiNode node)
    {
        var start = OnlyWhitespaceBefore(node.Span.Start) ? LineStart(node.Span.Start) : node.Span.Start;
        var end = OnlyWhitespaceAfter(node.Span.End) ? AfterLineEnd(node.Span.End) : node.Span.End;
        return new TextSpan(start, end);
    }

    /// <summary>Setzt jede Zeile auf <paramref name="baseIndent"/> plus je führendem Tab eine Einrückungseinheit; Zeilentrenner werden zum Zeilenende der Datei.</summary>
    public string Reindent(string snippet, string baseIndent)
    {
        var lines = snippet.Replace("\r\n", "\n").Split('\n');
        var builder = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(Newline);
            }

            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                continue;
            }

            var tabs = 0;
            while (tabs < line.Length && line[tabs] == '\t')
            {
                tabs++;
            }

            builder.Append(baseIndent);
            for (var t = 0; t < tabs; t++)
            {
                builder.Append(IndentUnit);
            }

            builder.Append(line, tabs, line.Length - tabs);
        }

        return builder.ToString();
    }

    private IReadOnlyList<TextEdit> InsertBefore(GuiNode anchor, string body, string indent)
    {
        return OnlyWhitespaceBefore(anchor.Span.Start)
            ? new[] { new TextEdit(LineStart(anchor.Span.Start), 0, body + Newline) }
            : new[] { new TextEdit(anchor.Span.Start, 0, Newline + body + Newline + indent) };
    }

    private IReadOnlyList<TextEdit> InsertAfter(GuiNode anchor, string body, string indent)
    {
        var end = anchor.Span.End;
        if (OnlyWhitespaceAfter(end))
        {
            var lineEnd = LineEnd(end);
            return lineEnd < _text.Length
                ? new[] { new TextEdit(lineEnd + 1, 0, body + Newline) }
                : new[] { new TextEdit(_text.Length, 0, Newline + body) };
        }

        var next = end;
        while (next < _text.Length && _text[next] is ' ' or '\t')
        {
            next++;
        }

        var parent = anchor.Parent!;
        var closesHere = parent.CloseBrace == next;
        var restIndent = closesHere ? IndentAt(parent.Span.Start) : indent;
        return new[] { new TextEdit(end, next - end, Newline + body + Newline + restIndent) };
    }

    private IReadOnlyList<TextEdit> InsertIntoEmpty(GuiNode parent, string body)
    {
        if (parent.Kind == GuiNodeKind.Document)
        {
            var prefix = _text.Length > 0 && !_text.EndsWith('\n') ? Newline : string.Empty;
            return new[] { new TextEdit(_text.Length, 0, prefix + body + Newline) };
        }

        if (parent.CloseBrace < 0)
        {
            return new[] { new TextEdit(parent.Span.End, 0, Newline + body) };
        }

        var inside = parent.BodySpan;
        return new[] { new TextEdit(inside.Start, inside.Length, Newline + body + Newline + IndentAt(parent.Span.Start)) };
    }

    private IReadOnlyList<TextEdit> Swap(GuiNode? first, GuiNode? second)
    {
        if (first is null || second is null || !IsOwnLines(first) || !IsOwnLines(second))
        {
            return Array.Empty<TextEdit>();
        }

        var a = NodeLines(first);
        var b = NodeLines(second);
        var firstText = _text[a.Start..a.End];
        var between = _text[a.End..b.Start];
        var secondText = _text[b.Start..b.End];
        if (!secondText.EndsWith('\n'))
        {
            secondText += Newline;
            firstText = firstText.EndsWith(Newline, StringComparison.Ordinal) ? firstText[..^Newline.Length] : firstText.TrimEnd('\r', '\n');
        }

        return new[] { new TextEdit(a.Start, b.End - a.Start, secondText + between + firstText) };
    }

    private static GuiNode? Sibling(GuiNode node, int direction)
    {
        var siblings = node.Parent?.Children;
        if (siblings is null)
        {
            return null;
        }

        for (var i = siblings.IndexOf(node) + direction; i >= 0 && i < siblings.Count; i += direction)
        {
            if (siblings[i].Kind != GuiNodeKind.Comment)
            {
                return siblings[i];
            }
        }

        return null;
    }

    private static bool IsPropertyLike(GuiNode node) =>
        node.Kind is GuiNodeKind.Property or GuiNodeKind.Using
        || (node.Kind == GuiNodeKind.KeyedBlock && node.Children.Count > 0 && node.Children.All(c => c.Kind is GuiNodeKind.ListItem or GuiNodeKind.Comment));

    private static void RequireBody(GuiNode node)
    {
        if (!node.HasBody)
        {
            throw new ArgumentException($"\"{node.Name}\" hat keinen Block.", nameof(node));
        }
    }

    private bool IsOwnLines(GuiNode node) => OnlyWhitespaceBefore(node.Span.Start) && OnlyWhitespaceAfter(node.Span.End);

    private int LineStart(int offset)
    {
        var i = offset;
        while (i > 0 && _text[i - 1] != '\n')
        {
            i--;
        }

        return i;
    }

    /// <summary>Position des "\n" am Ende der Zeile oder Textende.</summary>
    private int LineEnd(int offset)
    {
        var i = offset;
        while (i < _text.Length && _text[i] != '\n')
        {
            i++;
        }

        return i;
    }

    private int AfterLineEnd(int offset)
    {
        var end = LineEnd(offset);
        return end < _text.Length ? end + 1 : end;
    }

    private bool OnlyWhitespaceBefore(int offset)
    {
        for (var i = LineStart(offset); i < offset; i++)
        {
            if (!char.IsWhiteSpace(_text[i]) && _text[i] != '﻿')
            {
                return false;
            }
        }

        return true;
    }

    private bool OnlyWhitespaceAfter(int offset)
    {
        var end = LineEnd(offset);
        for (var i = offset; i < end; i++)
        {
            if (!char.IsWhiteSpace(_text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private string IndentAt(int offset)
    {
        var start = LineStart(offset);
        var i = start;
        while (i < _text.Length && _text[i] is ' ' or '\t')
        {
            i++;
        }

        return _text[start..i];
    }

    private static string DetectIndentUnit(string text)
    {
        var tabs = 0;
        var spaces = 0;
        var smallest = int.MaxValue;
        foreach (var line in text.Split('\n'))
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            if (line[0] == '\t')
            {
                tabs++;
            }
            else if (line[0] == ' ')
            {
                spaces++;
                smallest = Math.Min(smallest, line.Length - line.TrimStart(' ').Length);
            }
        }

        return spaces > tabs ? new string(' ', Math.Clamp(smallest, 1, 8)) : "\t";
    }
}
