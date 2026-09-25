using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.GuiDesign.Designer;

public enum GuiPaletteKind
{
    EngineWidget,
    Type,
    Template,
}

/// <summary>
/// Eintrag der Palette. <c>Group</c> ist "" für Engine-Widgets, sonst der relative Pfad der Datei mit der wirksamen Definition.
/// <c>Usages</c>: bei Engine-Widgets die Anzahl im gelernten Katalog, sonst die Verwendungen im Index (als Widget bzw. über <c>using</c>).
/// </summary>
public sealed record GuiPaletteEntry(
    string Name,
    GuiPaletteKind Kind,
    string Group,
    GuiLayer Layer,
    int Usages,
    GuiWidgetCategory Category,
    GuiDefinition? Definition)
{
    public bool IsMod => Layer == GuiLayer.Mod;

    /// <summary>Code zum Einfügen als Widget; null bei Templates (sie kommen als <c>using</c> in die Auswahl).</summary>
    public string? Snippet => Kind == GuiPaletteKind.Template ? null : $"{Name} = {{ }}";
}

/// <summary>
/// Palette des Designers: oben die Engine-Widgets aus dem Katalog (die EU-V-Hüllen wie <c>uberhbox</c> unter ihrem Typnamen <c>hbox</c>),
/// darunter alle Typen und Templates (je Name die wirksame Definition) nach Quelldatei gruppiert, Mod-Dateien zuerst.
/// </summary>
public static class GuiPalette
{
    public static IReadOnlyList<GuiPaletteEntry> Entries(GuiIndex index, GuiResolver resolver, GuiPropertyCatalog catalog, CancellationToken cancellation = default)
    {
        var engine = new List<GuiPaletteEntry>();
        var engineNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var widget in catalog.Widgets.Values.OrderByDescending(w => w.Count).ThenBy(w => w.Name, StringComparer.Ordinal))
        {
            var normalized = GuiWidgetKinds.Normalize(widget.Name);
            var name = normalized != widget.Name && resolver.IsType(normalized) ? normalized : widget.Name;
            if (engineNames.Add(name))
            {
                engine.Add(new GuiPaletteEntry(
                    name,
                    GuiPaletteKind.EngineWidget,
                    string.Empty,
                    GuiLayer.Vanilla,
                    widget.Count,
                    GuiWidgetKinds.CategoryOf(widget.Name),
                    index.Find(name, GuiDefinitionKind.Type)));
            }
        }

        var files = new List<GuiPaletteEntry>();
        foreach (var key in index.Definitions
            .Where(d => d.Kind is GuiDefinitionKind.Type or GuiDefinitionKind.Template)
            .Select(d => (d.Kind, d.Name))
            .Distinct())
        {
            cancellation.ThrowIfCancellationRequested();
            var isType = key.Kind == GuiDefinitionKind.Type;
            if (isType && engineNames.Contains(key.Name))
            {
                continue;
            }

            var definition = index.Find(key.Name, key.Kind)!;
            var usageKind = isType ? GuiUsageKind.Widget : GuiUsageKind.Using;
            files.Add(new GuiPaletteEntry(
                key.Name,
                isType ? GuiPaletteKind.Type : GuiPaletteKind.Template,
                definition.File.RelativePath,
                definition.File.Layer,
                index.UsagesOf(key.Name).Count(u => u.Kind == usageKind),
                isType ? GuiWidgetKinds.CategoryOf(resolver.BaseWidgetOf(key.Name)) : GuiWidgetCategory.Other,
                definition));
        }

        files.Sort((a, b) =>
        {
            var byLayer = b.Layer.CompareTo(a.Layer);
            if (byLayer != 0)
            {
                return byLayer;
            }

            var byGroup = StringComparer.OrdinalIgnoreCase.Compare(a.Group, b.Group);
            return byGroup != 0 ? byGroup : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
        });
        return engine.Concat(files).ToList();
    }

    public static bool Matches(GuiPaletteEntry entry, string text) =>
        text.Length == 0 || entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase);
}
