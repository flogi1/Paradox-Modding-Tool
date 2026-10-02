using System.Text;

namespace ParadoxModTool.GuiDesign.Blocks;

/// <summary>
/// Eingaben zu einem Baustein, eine Ebene: die Wurzel (Parameter oberster Ebene) oder ein Eintrag einer Wiederholung. Jede Ebene kennt ihre
/// äußere (<see cref="Parent"/>), damit Platzhalter und Vorgaben die Werte äußerer Ebenen sehen.
/// </summary>
public sealed class GuiBlockValues
{
    /// <summary>Höchste Anzahl Einträge je Wiederholung (Plan-Ruling 6); größere Eingaben werden begrenzt.</summary>
    public const int MaxCount = 50;

    private GuiBlockValues(GuiBlockValues? parent, int index)
    {
        Parent = parent;
        Index = index;
    }

    public GuiBlockValues? Parent { get; }

    /// <summary>0 an der Wurzel, 1 … n für die Einträge einer Wiederholung.</summary>
    public int Index { get; }

    /// <summary>Werte der Parameter dieser Ebene nach vollem Schlüssel.</summary>
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    /// <summary>Einträge der direkten Wiederholungen dieser Ebene nach vollem Schlüssel.</summary>
    public Dictionary<string, List<GuiBlockValues>> Repeats { get; } = new(StringComparer.Ordinal);

    public static GuiBlockValues Defaults(GuiBlock block)
    {
        var root = new GuiBlockValues(null, 0);
        root.Fill(block.Parameters, block.Repeats);
        return root;
    }

    /// <summary>
    /// Setzt die Anzahl der Einträge einer direkten Wiederholung dieser Ebene (begrenzt auf 0 … <see cref="MaxCount"/>). Vorhandene Einträge
    /// bleiben; neue bekommen ihre Vorgaben, aufgelöst mit den aktuellen Werten der äußeren Ebenen.
    /// </summary>
    public void Resize(GuiBlock block, string repeatKey, int count)
    {
        var repeat = block.FindRepeat(repeatKey) ?? throw new ArgumentException($"Unknown repeat '{repeatKey}'.", nameof(repeatKey));
        count = Math.Clamp(count, 0, MaxCount);
        if (!Repeats.TryGetValue(repeatKey, out var entries))
        {
            entries = new List<GuiBlockValues>();
            Repeats[repeatKey] = entries;
        }

        if (entries.Count > count)
        {
            entries.RemoveRange(count, entries.Count - count);
        }
        else
        {
            AddEntries(repeat, entries, count);
        }
    }

    /// <summary>Wert eines Parameters dieser oder einer äußeren Ebene.</summary>
    public string? Lookup(string key)
    {
        for (var level = this; level is not null; level = level.Parent)
        {
            if (level.Values.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Einträge einer Wiederholung dieser oder der nächsten äußeren Ebene, die sie hat.</summary>
    internal IReadOnlyList<GuiBlockValues> EntriesOf(string repeatKey)
    {
        for (var level = this; level is not null; level = level.Parent)
        {
            if (level.Repeats.TryGetValue(repeatKey, out var entries))
            {
                return entries;
            }
        }

        return Array.Empty<GuiBlockValues>();
    }

    /// <summary>Setzt Text und Platzhalter zusammen; <c>$index$</c> ist die Nummer des nächsten Eintrags.</summary>
    internal string Resolve(IReadOnlyList<GuiBlockPart> parts)
    {
        var text = new StringBuilder();
        foreach (var part in parts)
        {
            switch (part)
            {
                case GuiBlockText plain:
                    text.Append(plain.Text);
                    break;
                case GuiBlockPlaceholder placeholder:
                    text.Append(ValueOf(placeholder.Key));
                    break;
            }
        }

        return text.ToString();
    }

    internal string ValueOf(string key)
    {
        if (key != GuiBlockParser.IndexKey)
        {
            return Lookup(key) ?? string.Empty;
        }

        for (var level = this; level is not null; level = level.Parent)
        {
            if (level.Index > 0)
            {
                return level.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return string.Empty;
    }

    private void Fill(IReadOnlyList<GuiBlockParameter> parameters, IReadOnlyList<GuiBlockRepeat> repeats)
    {
        foreach (var parameter in parameters)
        {
            Values[parameter.Key] = Resolve(parameter.DefaultParts);
        }

        foreach (var repeat in repeats)
        {
            var entries = new List<GuiBlockValues>();
            Repeats[repeat.Key] = entries;
            AddEntries(repeat, entries, Math.Clamp(repeat.DefaultCount, 0, MaxCount));
        }
    }

    private void AddEntries(GuiBlockRepeat repeat, List<GuiBlockValues> entries, int count)
    {
        while (entries.Count < count)
        {
            var entry = new GuiBlockValues(this, entries.Count + 1);
            entries.Add(entry);
            entry.Fill(repeat.Parameters, repeat.Repeats);
        }
    }
}
