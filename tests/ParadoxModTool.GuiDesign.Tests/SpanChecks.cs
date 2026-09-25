using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Invarianten des Syntaxbaums, geprüft an beliebigen Dokumenten.</summary>
internal static class SpanChecks
{
    /// <summary>Kinder liegen im Inhalt des Elternknotens, überlappen sich nicht und stehen in Textreihenfolge; Klammerpositionen zeigen auf "{" bzw. "}".</summary>
    public static void AssertNested(GuiDocument doc, string label)
    {
        foreach (var node in doc.Root.Descendants().Prepend(doc.Root))
        {
            if (node.OpenBrace >= 0)
            {
                Assert.True(doc.Text[node.OpenBrace] == '{', $"{label}: OpenBrace von '{node.Name}' (Zeile {node.Line}) zeigt nicht auf '{{'");
            }

            if (node.CloseBrace >= 0)
            {
                Assert.True(doc.Text[node.CloseBrace] == '}' && node.Span.End == node.CloseBrace + 1, $"{label}: CloseBrace von '{node.Name}' (Zeile {node.Line}) stimmt nicht");
            }

            var previousEnd = node.HasBody ? node.BodySpan.Start : node.Span.Start;
            foreach (var child in node.Children)
            {
                Assert.True(node.Span.Contains(child.Span), $"{label}: {child.Kind} '{child.Name}' (Zeile {child.Line}) liegt nicht im Elternknoten");
                Assert.True(child.Span.Start >= previousEnd, $"{label}: {child.Kind} '{child.Name}' (Zeile {child.Line}) überlappt den Vorgänger");
                Assert.True(child.Parent == node, $"{label}: Parent von '{child.Name}' falsch");
                previousEnd = child.Span.End;
            }
        }
    }

    /// <summary>Jedes Zeichen im Inhalt eines Knotens gehört zu einem Kind oder ist Leerraum (oder eine gemeldete überzählige "}").</summary>
    public static void AssertGapsAreWhitespace(GuiDocument doc, string label)
    {
        var stray = doc.Diagnostics.Where(d => d.Kind == GuiDiagnosticKind.UnexpectedCloseBrace).Select(d => d.Start).ToHashSet();
        foreach (var node in doc.Root.Descendants().Prepend(doc.Root).Where(n => n.HasBody))
        {
            var body = node.BodySpan;
            var position = body.Start;
            foreach (var child in node.Children)
            {
                AssertWhitespace(doc, position, child.Span.Start, stray, label);
                position = child.Span.End;
            }

            AssertWhitespace(doc, position, body.End, stray, label);
        }
    }

    private static void AssertWhitespace(GuiDocument doc, int from, int to, HashSet<int> stray, string label)
    {
        for (var i = from; i < to; i++)
        {
            var c = doc.Text[i];
            Assert.True(char.IsWhiteSpace(c) || c == '\uFEFF' || stray.Contains(i), $"{label}: Zeichen '{c}' an Offset {i} gehört zu keinem Knoten");
        }
    }
}
