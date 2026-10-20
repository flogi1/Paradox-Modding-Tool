namespace ParadoxModTool.GuiDesign.Resolve;

public enum GuiWidgetCategory
{
    Container,
    Box,
    List,
    Text,
    Icon,
    Button,
    Window,
    Other,
}

/// <summary>Einordnung von Engine-Widgets für Anzeige und Layout. EU V kapselt Engine-Widgets (Vanilla: type hbox = uberhbox usw.).</summary>
public static class GuiWidgetKinds
{
    /// <summary>Engine-Widget ohne das Präfix "uber".</summary>
    public static string Normalize(string baseWidget) =>
        baseWidget.Length > 4 && baseWidget.StartsWith("uber", StringComparison.Ordinal) ? baseWidget[4..] : baseWidget;

    /// <summary>Klassische Schlüssel (HOI IV, EU IV), ohne Rücksicht auf Groß-/Kleinschreibung. Ein Fenster auf oberster Ebene ist hier ein Container; ob es als Fenster gilt, entscheidet der Aufrufer über die Vorschau-Wurzel.</summary>
    private static readonly Dictionary<string, GuiWidgetCategory> ClassicCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["containerWindowType"] = GuiWidgetCategory.Container,
        ["windowType"] = GuiWidgetCategory.Container,
        ["iconType"] = GuiWidgetCategory.Icon,
        ["buttonType"] = GuiWidgetCategory.Button,
        ["guiButtonType"] = GuiWidgetCategory.Button,
        ["checkboxType"] = GuiWidgetCategory.Button,
        ["instantTextBoxType"] = GuiWidgetCategory.Text,
        ["textboxType"] = GuiWidgetCategory.Text,
        ["editBoxType"] = GuiWidgetCategory.Text,
        ["gridBoxType"] = GuiWidgetCategory.List,
        ["listboxType"] = GuiWidgetCategory.List,
        ["smoothListboxType"] = GuiWidgetCategory.List,
        ["OverlappingElementsBoxType"] = GuiWidgetCategory.List,
    };

    public static GuiWidgetCategory CategoryOf(string baseWidget)
    {
        if (ClassicCategories.TryGetValue(baseWidget, out var classic))
        {
            return classic;
        }

        var kind = Normalize(baseWidget);
        return kind switch
        {
            "window" => GuiWidgetCategory.Window,
            "hbox" or "vbox" or "flowcontainer" => GuiWidgetCategory.Box,
            "fixedgridbox" or "dynamicgridbox" or "overlappingitembox" or "scrollarea" => GuiWidgetCategory.List,
            "widget" or "container" => GuiWidgetCategory.Container,
            "icon" => GuiWidgetCategory.Icon,
            _ when kind.StartsWith("button", StringComparison.Ordinal) || kind == "checkbutton" => GuiWidgetCategory.Button,
            _ when kind.StartsWith("text", StringComparison.Ordinal) => GuiWidgetCategory.Text,
            _ => GuiWidgetCategory.Other,
        };
    }
}
