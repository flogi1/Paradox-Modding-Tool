using System.Globalization;

namespace ParadoxModTool.Modifiers;

/// <summary>Ein Chip unter einem Modifikatorfeld: <see cref="Key"/> (leer, wenn die Zeile kein <c>schlüssel = wert</c> ist), Anzeigetext und ob PMT den Modifier kennt.</summary>
public sealed record ModifierChip(string Key, string Text, bool Known);

/// <summary>
/// Text eines Chips wie im Spiel (Spec 2026-10-05-generator-packages-lookups, Abschnitt 2): übersetzter Name und formatierter Wert, derselbe Text wie beim
/// Hover im Editor. Unbekannte Schlüssel und andere Zeilen erscheinen roh.
/// </summary>
public static class ModifierChipText
{
    /// <returns><see langword="null"/> für leere Zeilen und reine Kommentare.</returns>
    public static ModifierChip? For(string line, ModifierFormatIndex index, ModifierNameIndex names)
    {
        var comment = line.IndexOf('#');
        var text = (comment >= 0 ? line[..comment] : line).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var equals = text.IndexOf('=');
        if (equals <= 0)
        {
            return new ModifierChip(string.Empty, text, false);
        }

        var key = text[..equals].Trim();
        var value = text[(equals + 1)..].Trim();
        if (key.Length == 0 || key.Any(char.IsWhiteSpace) || value.Length == 0)
        {
            return new ModifierChip(string.Empty, text, false);
        }

        var name = names.Find(key);
        var format = index.Find(key);
        if (format is not null && decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return new ModifierChip(key, $"{name ?? key} {ModifierValueFormatter.Format(number, format)}", true);
        }

        return name is not null || format is not null || index.Traits.ContainsKey(key)
            ? new ModifierChip(key, $"{name ?? key} {value}", true)
            : new ModifierChip(key, text, false);
    }
}
