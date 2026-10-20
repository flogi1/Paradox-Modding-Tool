using System.Reflection;
using ParadoxModTool.Editor.Gui;

namespace ParadoxModTool.Editor.Tests;

/// <summary>
/// Setzt <see cref="GuiSettings"/> ohne das statische Ereignis <c>Changed</c> auszulösen. Fenster aus anderen Tests (EditorPane) bleiben daran hängen und
/// gehören einem fremden Thread; ein Aufruf von einem anderen Thread aus wirft dort. Der Handler-Bestand wird danach wiederhergestellt.
/// </summary>
internal static class GuiSettingsChanged
{
    public static void Quietly(Action action)
    {
        var field = typeof(GuiSettings).GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GuiSettings.Changed hat kein Feld mehr.");
        var handlers = field.GetValue(null);
        field.SetValue(null, null);
        try
        {
            action();
        }
        finally
        {
            field.SetValue(null, handlers);
        }
    }
}
