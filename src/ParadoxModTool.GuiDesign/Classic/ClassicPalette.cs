using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Palette des klassischen Formats (HOI IV, EU IV): nur die Elementtypen der Spec "Fakten → Format", in der Schreibweise, die der Katalog
/// am häufigsten sah. Typen und Templates gibt es klassisch nicht.
/// </summary>
public static class ClassicPalette
{
    /// <summary>Elementtypen, kleingeschrieben (so liegen sie im klassischen Katalog).</summary>
    public static IReadOnlyList<string> ElementKeys { get; } = new[]
    {
        "containerwindowtype", "windowtype", "buttontype", "guibuttontype", "icontype", "instanttextboxtype", "textboxtype",
        "checkboxtype", "editboxtype", "gridboxtype", "listboxtype", "smoothlistboxtype", "overlappingelementsboxtype",
        "scrollbartype", "extendedscrollbartype", "positiontype", "dropdownboxtype", "eu3dialogtype", "shieldtype",
    };

    /// <summary>Elementtypen, die der Katalog für dieses Spiel gelernt hat, die häufigsten zuerst.</summary>
    public static IReadOnlyList<GuiPaletteEntry> Entries(GuiPropertyCatalog catalog)
    {
        var entries = new List<(string Key, GuiWidgetStats Stats)>();
        foreach (var key in ElementKeys)
        {
            if (catalog.LookupWidget(key) is { } stats)
            {
                entries.Add((key, stats));
            }
        }

        return entries
            .OrderByDescending(e => e.Stats.Count)
            .ThenBy(e => e.Key, StringComparer.Ordinal)
            .Select(e =>
            {
                var spelling = e.Stats.PreferredSpelling;
                return new GuiPaletteEntry(
                    spelling,
                    GuiPaletteKind.EngineWidget,
                    string.Empty,
                    GuiLayer.Vanilla,
                    e.Stats.Count,
                    GuiWidgetKinds.CategoryOf(spelling),
                    null)
                {
                    ClassicSnippet = $"{spelling} = {{\n\tname = \"new_{e.Key}\"\n\tposition = {{ x = 0 y = 0 }}\n}}",
                };
            })
            .ToList();
    }
}
