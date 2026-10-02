using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>Ein GUI-Schlüssel: <c>type name</c> oder <c>template name</c> mit seiner Einheit; <see cref="ContainerEnd"/> = schließende Klammer des <c>types</c>-Blocks.</summary>
public sealed record GuiUnit(string Key, EntryUnit Unit, int? ContainerEnd);

/// <summary>Ergebnis von <see cref="GuiUnits.Read"/>: die Schlüssel und die obersten Einheiten, die sie belegen (Rest der Datei ist alles andere).</summary>
public sealed record GuiRead(IReadOnlyList<GuiUnit> Keys, IReadOnlyList<EntryUnit> Consumed);

/// <summary>
/// Zerlegt eine .gui-Datei in GUI-Typen (<c>type name = basis { }</c> in <c>types X { }</c>) und Templates (<c>template name { }</c>). Gilt für
/// "zuletzt geladen gewinnt" (EU V) und "zuerst geladen gewinnt" (CK III, gemessen 2026-09-30); Kompatch-Merge und Mod-Kompatibilität nutzen
/// dieselbe Zerlegung. Oberste Fenster und Widgets sind keine Schlüssel.
/// </summary>
public static class GuiUnits
{
    public static bool Applies(RuleMatch rule, string relativePath) =>
        rule.Behavior is OverrideBehavior.LastWins or OverrideBehavior.FirstWins
        && string.Equals(Path.GetExtension(relativePath), ".gui", StringComparison.OrdinalIgnoreCase);

    public static GuiRead Read(ScriptBlock root)
    {
        var keys = new List<GuiUnit>();
        var consumed = new List<EntryUnit>();
        foreach (var unit in EntryUnits.Build(root.Entries))
        {
            var first = FirstWord(unit);
            if (first == "template")
            {
                consumed.Add(unit);
                keys.Add(new GuiUnit("template " + HeaderName(unit), unit, null));
            }
            else if (first == "types" && unit.Value is ScriptBlock typesBlock)
            {
                consumed.Add(unit);
                var typesEnd = unit.Entry.SourceEnd is { } end ? end - 1 : (int?)null;
                foreach (var inner in EntryUnits.Build(typesBlock.Entries))
                {
                    if (FirstWord(inner) == "type")
                    {
                        keys.Add(new GuiUnit("type " + HeaderName(inner), inner, typesEnd));
                    }
                }
            }
        }

        return new GuiRead(keys, consumed);
    }

    private static string? FirstWord(EntryUnit unit) =>
        unit.Kind == EntryUnit.UnitKind.Body && unit.Header.Count > 0 && unit.Header[0] is ScriptListItem { Value: ScriptScalar word } ? word.Text : null;

    /// <summary>"template name" → name, "type name = basis" → name.</summary>
    private static string HeaderName(EntryUnit unit) =>
        unit.Header.Skip(1).Select(h => h switch
        {
            ScriptStatement statement => statement.Key,
            ScriptListItem { Value: ScriptScalar value } => value.Text,
            _ => string.Empty,
        }).FirstOrDefault(name => name.Length > 0) ?? string.Empty;
}
