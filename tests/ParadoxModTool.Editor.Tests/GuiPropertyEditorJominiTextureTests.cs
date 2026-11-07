using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Texturfeld: "Textur wählen…" neben "Durchsuchen…" setzt den Pfad ins Feld; "Übernehmen" schreibt; schreibgeschützt ist der Knopf aus.</summary>
[Collection("UiLanguage")]
public sealed class GuiPropertyEditorJominiTextureTests
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

    private static (GuiPropertyEditor Editor, List<GuiDesignerAction> Actions) Show(string property, string value, bool editable)
    {
        var text = $"widget = {{\n\tname = \"w\"\n\t{property} = {value}\n}}\n";
        var file = new GuiSourceFile(@"C:\mod\gui\a.gui", "gui/a.gui", GuiLayer.Mod, "Mod", 0, 0);
        var index = GuiIndex.Build(new[] { file }, _ => text);
        var node = index.DocumentOf(file).Root.Children[0];
        var element = new GuiResolver(index, GuiRules.Empty).Resolve(node);
        var editor = new GuiPropertyEditor();
        var actions = new List<GuiDesignerAction>();
        editor.ActionRequested += (_, action) => actions.Add(action);
        editor.SetElement(node, element, editable);
        editor.SetProperty(element.Property(property));
        return (editor, actions);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static Button ButtonOf(GuiPropertyEditor editor, string content) => Tree(editor).OfType<Button>().Single(b => b.Content as string == content);

    private static void WithPicker(Func<string?, string?>? picker, Action action)
    {
        var previous = GuiSettings.TexturePicker;
        GuiSettings.TexturePicker = picker;
        try
        {
            action();
        }
        finally
        {
            GuiSettings.TexturePicker = previous;
        }
    }

    [Fact]
    public void TexturePick_ReadOnly_ButtonDisabled()
    {
        Sta.Run(() => WithPicker(_ => "gfx/interface/x.dds", () =>
        {
            var (editor, _) = Show("texture", "\"gfx/interface/old.dds\"", editable: false);

            Assert.False(ButtonOf(editor, GuiTexts.PropertyBrowse).IsEnabled);
            Assert.False(ButtonOf(editor, GuiTexts.TexturePickerButton).IsEnabled);
        }));
    }

    [Fact]
    public void TexturePick_WithoutPicker_NoButton()
    {
        Sta.Run(() => WithPicker(null, () =>
        {
            var (editor, _) = Show("texture", "\"gfx/interface/old.dds\"", editable: true);

            Assert.DoesNotContain(Tree(editor).OfType<Button>(), b => b.Content as string == GuiTexts.TexturePickerButton);
        }));
    }

    [Fact]
    public void TexturePick_WritesIntoField()
    {
        Sta.Run(() =>
        {
            string? asked = null;
            WithPicker(current => { asked = current; return "gfx/interface/x.dds"; }, () =>
            {
                var (editor, actions) = Show("texture", "\"gfx/interface/old.dds\"", editable: true);
                Assert.True(ButtonOf(editor, GuiTexts.PropertyBrowse).IsEnabled);

                Click(ButtonOf(editor, GuiTexts.TexturePickerButton));
                var field = Tree(editor).OfType<TextBox>().Single(t => t.MinWidth == 240);
                Assert.Equal("gfx/interface/x.dds", field.Text);
                Assert.Empty(actions);

                Click(ButtonOf(editor, GuiTexts.PropertyApply));

                Assert.Contains("gfx/interface/old.dds", asked);
                Assert.Equal("\"gfx/interface/x.dds\"", actions.Single().Value);
            });
        });
    }
}
