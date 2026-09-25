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

    public static GuiWidgetCategory CategoryOf(string baseWidget)
    {
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
