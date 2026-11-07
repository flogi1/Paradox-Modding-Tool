using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Jomini-Farbfeld: Farbfeld und Wählen-Knopf folgen der Regel der Vorschau und schreiben in der Form des Werts.</summary>
[Collection("UiLanguage")]
public sealed class GuiPropertyEditorJominiColorTests
{
    private static IEnumerable<DependencyObject> Tree(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            foreach (var nested in Tree(child))
            {
                yield return nested;
            }
        }
    }

    private static (GuiPropertyEditor Editor, List<GuiDesignerAction> Actions) Show(string property, string value)
    {
        var text = $"widget = {{\n\tname = \"w\"\n\t{property} = {value}\n}}\n";
        var file = new GuiSourceFile(@"C:\mod\gui\a.gui", "gui/a.gui", GuiLayer.Mod, "Mod", 0, 0);
        var index = GuiIndex.Build(new[] { file }, _ => text);
        var node = index.DocumentOf(file).Root.Children[0];
        var element = new GuiResolver(index, GuiRules.Empty).Resolve(node);
        var editor = new GuiPropertyEditor();
        var actions = new List<GuiDesignerAction>();
        editor.ActionRequested += (_, action) => actions.Add(action);
        editor.SetElement(node, element, editable: true);
        editor.SetProperty(element.Property(property));
        return (editor, actions);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void WithPicker(Func<Color, Color?> picker, Action action)
    {
        var previous = GuiSettings.ColorPicker;
        GuiSettings.ColorPicker = picker;
        try
        {
            action();
        }
        finally
        {
            GuiSettings.ColorPicker = previous;
        }
    }

    [Fact]
    public void Picker_KeepsByteFormOfFontcolor_AndStartsFromTheShownColour()
    {
        Sta.Run(() =>
        {
            Color? start = null;
            WithPicker(c => { start = c; return Color.FromRgb(200, 20, 0); }, () =>
            {
                var (editor, actions) = Show("fontcolor", "{ 46 49 49 0.7 }");
                Click(Tree(editor).OfType<Button>().Single(b => b.Content as string == GuiTexts.PropertyPickColor));
                Click(Tree(editor).OfType<Button>().Single(b => b.Content as string == GuiTexts.PropertyApply));

                Assert.Equal(46, start!.Value.R);
                Assert.Equal("{ 200 20 0 0.7 }", actions.Single().Value);
            });
        });
    }

    [Fact]
    public void Picker_Texture_WritesZeroToOne_AndSwatchHasTooltip()
    {
        Sta.Run(() => WithPicker(_ => Color.FromRgb(200, 20, 0), () =>
        {
            var (editor, actions) = Show("color", "{ 46 49 49 0.7 }");
            var swatch = Tree(editor).OfType<Border>().First(b => b.Width == 18);
            Assert.Equal(GuiTexts.ColorAboveOne, swatch.ToolTip);
            Click(Tree(editor).OfType<Button>().Single(b => b.Content as string == GuiTexts.PropertyPickColor));
            Click(Tree(editor).OfType<Button>().Single(b => b.Content as string == GuiTexts.PropertyApply));

            Assert.Equal("{ 0.784 0.078 0 0.7 }", actions.Single().Value);
        }));
    }
}
