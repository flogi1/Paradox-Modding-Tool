using System.Text;

namespace ParadoxModTool.Templates;

/// <summary>Eine Stelle im erzeugten Text, an der ein Zählpunkt seinen Wert eingesetzt hat.</summary>
public readonly record struct RenderedSpan(int Start, int Length, VariationPoint Point);

/// <summary>Erzeugt aus einem <see cref="TemplateModel"/> die gewünschte Anzahl Durchläufe.</summary>
public static class TemplateEngine
{
    private readonly record struct Item(int Start, int End, VariationPoint? Point, RepeatGroup? Group);

    /// <summary>
    /// Erzeugt <paramref name="count"/> Durchläufe, getrennt durch eine Leerzeile; das Ergebnis endet mit einem Zeilenumbruch. Wird <paramref name="spans"/> übergeben,
    /// erhält es die Stellen im Ergebnis, an denen ein Zählpunkt eingesetzt wurde.
    /// </summary>
    public static string Render(TemplateModel model, int count, List<RenderedSpan>? spans = null)
    {
        if (count < 1)
        {
            return string.Empty;
        }

        var items = BuildItems(model);
        var sb = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(model.Newline).Append(model.Newline);
            }

            var cursor = 0;
            Emit(sb, model, items, ref cursor, 0, model.Text.Length, i, 0, spans);
        }

        sb.Append(model.Newline);
        return sb.ToString();
    }

    /// <summary>Erzeugt nur den Durchlauf <paramref name="instance"/> (0-basiert) ohne abschließenden Zeilenumbruch.</summary>
    public static string RenderInstance(TemplateModel model, int instance)
    {
        var items = BuildItems(model);
        var sb = new StringBuilder();
        var cursor = 0;
        Emit(sb, model, items, ref cursor, 0, model.Text.Length, instance, 0, null);
        return sb.ToString();
    }

    private static List<Item> BuildItems(TemplateModel model)
    {
        var items = new List<Item>();
        foreach (var point in model.Points.Where(p => p.Enabled))
        {
            items.AddRange(point.Occurrences.Select(o => new Item(o.Start, o.Start + o.Length, point, null)));
        }

        items.AddRange(model.Groups.Select(g => new Item(g.Start, g.End, null, g)));

        // Gruppen vor den Zählpunkten an derselben Stelle, größere Gruppen vor kleineren.
        return items.OrderBy(i => i.Start).ThenByDescending(i => i.End).ThenBy(i => i.Group is null ? 1 : 0).ToList();
    }

    private static void Emit(StringBuilder sb, TemplateModel model, List<Item> items, ref int cursor, int from, int to, int instance, int repeat, List<RenderedSpan>? spans)
    {
        var pos = from;
        while (cursor < items.Count && items[cursor].Start < to)
        {
            var item = items[cursor];
            if (item.Start < pos)
            {
                // Überlappende Fundstelle (nur bei widersprüchlichen Eingaben): überspringen.
                cursor++;
                continue;
            }

            sb.Append(model.Text, pos, item.Start - pos);
            cursor++;

            if (item.Point is { } point)
            {
                var value = point.Format(instance, repeat);
                spans?.Add(new RenderedSpan(sb.Length, value.Length, point));
                sb.Append(value);
                pos = item.End;
                continue;
            }

            var group = item.Group!;
            var groupCursor = cursor;
            var repeats = Math.Max(1, group.Count);
            for (var j = 0; j < repeats; j++)
            {
                if (j > 0)
                {
                    sb.Append(group.Separator);
                }

                cursor = groupCursor;
                Emit(sb, model, items, ref cursor, group.Start, group.End, instance, j, spans);
            }

            pos = group.End;
        }

        sb.Append(model.Text, pos, to - pos);
    }
}
