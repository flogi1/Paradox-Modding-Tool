using System.Globalization;

namespace ParadoxModTool.Generators;

/// <summary>
/// Findet für den Einleser (Spec Teil 4) das Formular-Element zu <c>number</c> und <c>suffix</c>: die feste Gruppe, deren Basis passt
/// (<c>base &lt; number &lt;= base + max</c>), darin das Element <c>number - base</c>, darunter die Stufe aus der Endung (<c>""</c> = erste,
/// <c>_n</c> = (n+1)-te). Fehlende Elemente davor werden leer angelegt. Mit <c>bySuffix</c> (Muster ohne Gruppe <c>number</c>, aber mit <c>suffix</c>) und zählbarer
/// oberster Ebene wählt allein die Endung das Element dieser Ebene (<c>""</c> = erstes, <c>_n</c> = (n+1)-tes).
/// </summary>
internal static class ElementAddress
{
    public static FormNode? Element(GeneratorPackage package, GeneratorState state, int number, string? suffix, string level, bool create, ICollection<string> notes, bool bySuffix = false)
    {
        // Zählbare oberste Ebene (ohne Konstanten-Tabelle): Nummer = Index ab 1, suffix wählt das Element der Kind-Ebene.
        if (package.Root is { ConstantsTable: null } countable)
        {
            if (bySuffix)
            {
                // ohne Nummer wählt allein die Endung das Element der obersten Ebene: "" = erstes, _n = (n+1)-tes
                var position = string.IsNullOrEmpty(suffix) ? 0 : int.TryParse(suffix.TrimStart('_'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
                var elements = Children(state.Head, countable.Id);
                return position >= 0 && Ensure(package, elements, countable, position, create, notes) ? Deeper(package, elements[position], countable, null, level, create, notes) : null;
            }

            if (number < 1)
            {
                Note(notes, GeneratorsTexts.ReadNumberOutside(number));
                return null;
            }

            var roots = Children(state.Head, countable.Id);
            return Ensure(package, roots, countable, number - 1, create, notes) ? Deeper(package, roots[number - 1], countable, suffix, level, create, notes) : null;
        }

        if (package.Root is not { ConstantsTable: { } table, Child: { NumberBaseField: { } baseField } numbered } root
            || !package.Constants.TryGetValue(table, out var rows))
        {
            return null;
        }

        int group = -1, index = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (int.TryParse(rows[i].GetValueOrDefault(baseField), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
                && number > start && number <= start + numbered.Max)
            {
                group = i;
                index = number - start - 1;
                break;
            }
        }

        if (group < 0)
        {
            Note(notes, GeneratorsTexts.ReadNumberOutside(number));
            return null;
        }

        var groups = state.Head.Children[root.Id];
        var items = Children(groups[group], numbered.Id);
        if (!Ensure(package, items, numbered, index, create, notes))
        {
            return null;
        }

        return Deeper(package, items[index], numbered, suffix, level, create, notes);
    }

    /// <summary>Das Element selbst, wenn seine Ebene gemeint ist; sonst das Element der Kind-Ebene, das <paramref name="suffix"/> wählt (ohne Endung das erste).</summary>
    private static FormNode? Deeper(GeneratorPackage package, FormNode item, LevelDef itemLevel, string? suffix, string level, bool create, ICollection<string> notes)
    {
        if (level == itemLevel.Id || itemLevel.Child is not { } deeper)
        {
            return item;
        }

        var position = string.IsNullOrEmpty(suffix) ? 0 : int.TryParse(suffix.TrimStart('_'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
        if (position < 0)
        {
            return null;
        }

        var levels = Children(item, deeper.Id);
        return Ensure(package, levels, deeper, position, create, notes) ? levels[position] : null;
    }

    /// <summary>Füllt jede zählbare Ebene bis zu ihrem Minimum auf (nach dem Lesen).</summary>
    public static void FillToMinimum(GeneratorPackage package, GeneratorState state)
    {
        if (package.Root is { ConstantsTable: null } countable)
        {
            var roots = Children(state.Head, countable.Id);
            Pad(package, roots, countable, countable.Min);
            if (countable.Child is { } child)
            {
                foreach (var item in roots)
                {
                    Pad(package, Children(item, child.Id), child, child.Min);
                }
            }

            return;
        }

        if (package.Root is not { Child: { } numbered } root)
        {
            return;
        }

        foreach (var group in state.Head.Children[root.Id])
        {
            var items = Children(group, numbered.Id);
            Pad(package, items, numbered, numbered.Min);
            if (numbered.Child is { } deeper)
            {
                foreach (var item in items)
                {
                    Pad(package, Children(item, deeper.Id), deeper, deeper.Min);
                }
            }
        }
    }

    /// <summary>Ein leeres Element einer Ebene mit Vorgabewerten, ohne Kind-Elemente (die legt der Einleser selbst an).</summary>
    public static FormNode Empty(GeneratorPackage package, LevelDef level)
    {
        var node = FormNode.Create(level.Fields, null, package.Constants);
        if (level.Child is { } child)
        {
            node.Children[child.Id] = new List<FormNode>();
        }

        return node;
    }

    private static List<FormNode> Children(FormNode node, string id)
    {
        if (!node.Children.TryGetValue(id, out var list))
        {
            list = new List<FormNode>();
            node.Children[id] = list;
        }

        return list;
    }

    private static bool Ensure(GeneratorPackage package, List<FormNode> items, LevelDef level, int index, bool create, ICollection<string> notes)
    {
        if (index >= level.Max)
        {
            Note(notes, GeneratorsTexts.ReadTooMany(level.Label, level.Max));
            return false;
        }

        if (index < items.Count)
        {
            return true;
        }

        if (!create)
        {
            return false;
        }

        Pad(package, items, level, index + 1);
        return true;
    }

    private static void Pad(GeneratorPackage package, List<FormNode> items, LevelDef level, int count)
    {
        while (items.Count < count)
        {
            items.Add(Empty(package, level));
        }
    }

    private static void Note(ICollection<string> notes, string note)
    {
        if (!notes.Contains(note))
        {
            notes.Add(note);
        }
    }
}
