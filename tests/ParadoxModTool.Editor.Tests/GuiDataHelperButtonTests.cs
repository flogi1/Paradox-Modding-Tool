using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>
/// Der Designer-Eigenschaftseditor zeigt den Knopf "Daten-Helfer" bei Feldern mit Datenausdrücken (Nutzertest: visible = "[…]" ohne Knopf); bei
/// Bedingungs-Eigenschaften öffnet er den Bedingungs-Helfer mit dem Feldwert.
/// </summary>
public sealed class GuiDataHelperButtonTests
{
    private const string Text =
        "widget = {\n\tname = \"tabs\"\n\tvisible = \"[GetVariableSystem.HasValue( 'iv2_var_system_tabs', 'iv2_tabs_ideagroups' )]\"\n\tsize = { 10 10 }\n}\n";

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

    private static GuiPropertyEditor Editor(string text, string property)
    {
        var file = new GuiSourceFile(@"C:\mod\gui\a.gui", "gui/a.gui", GuiLayer.Mod, "Mod", 0, 0);
        var index = GuiIndex.Build(new[] { file }, _ => text);
        var node = index.DocumentOf(file).Root.Children[0];
        var element = new GuiResolver(index, GuiRules.Empty).Resolve(node);
        var editor = new GuiPropertyEditor();
        editor.SetElement(node, element, editable: true);
        editor.SetProperty(element.Property(property));
        return editor;
    }

    private static Button HelperButton(GuiPropertyEditor editor) =>
        Tree(editor).OfType<Button>().Single(b => b.Content as string == GuiDataTexts.OpenHelper);

    private static bool FieldShows(GuiPropertyEditor editor, string value) =>
        Tree(editor).OfType<TextBox>().Any(t => t.Text == value) || Tree(editor).OfType<ComboBox>().Any(c => c.Text == value);

    private static void WithHelpers(Func<GuiNode, string?>? data, Func<GuiNode, string, GuiHelperResult?>? condition, Action action)
    {
        var previousData = GuiSettings.DataHelper;
        var previousCondition = GuiSettings.ConditionHelper;
        GuiSettings.DataHelper = data;
        GuiSettings.ConditionHelper = condition;
        try
        {
            action();
        }
        finally
        {
            GuiSettings.DataHelper = previousData;
            GuiSettings.ConditionHelper = previousCondition;
        }
    }

    [Fact]
    public void VisibleWithDataExpression_ShowsTheHelperButton()
    {
        Sta.Run(() => WithHelpers(_ => "[X]", null, () =>
        {
            var buttons = Tree(Editor(Text, "visible")).OfType<Button>().Select(b => b.Content as string).ToList();
            Assert.Contains(GuiDataTexts.OpenHelper, buttons);
        }));
    }

    [Fact]
    public void ConditionButton_UsesConditionHelperWithFieldValue()
    {
        Sta.Run(() =>
        {
            string? seen = null;
            var dataCalled = false;
            WithHelpers(_ => { dataCalled = true; return "[Wrong]"; }, (_, value) => { seen = value; return new GuiHelperResult("[X.Y]", IsCondition: true); }, () =>
            {
                var editor = Editor(Text, "visible");
                HelperButton(editor).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.False(dataCalled);
                Assert.NotNull(seen);
                Assert.Contains("GetVariableSystem.HasValue", seen);
                Assert.True(FieldShows(editor, "[X.Y]"), "Feld enthält nicht genau das Ergebnis");
            });
        });
    }

    [Fact]
    public void ConditionButton_ReplacesYesWithExpression()
    {
        Sta.Run(() =>
        {
            string? seen = null;
            WithHelpers(null, (_, value) => { seen = value; return new GuiHelperResult("[X.Y]", IsCondition: true); }, () =>
            {
                var editor = Editor("widget = {\n\tvisible = yes\n}\n", "visible");
                HelperButton(editor).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.Equal("yes", seen);
                Assert.True(FieldShows(editor, "[X.Y]"), "yes wurde nicht ersetzt");
            });
        });
    }

    [Fact]
    public void ConditionButton_ResultFromChainTab_IsInsertedNotReplacing()
    {
        Sta.Run(() =>
        {
            WithHelpers(null, (_, _) => new GuiHelperResult("[A.C]", IsCondition: false), () =>
            {
                var editor = Editor("widget = {\n\tvisible = \"[Not(X.Y)]\"\n}\n", "visible");
                HelperButton(editor).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                var texts = Tree(editor).OfType<TextBox>().Select(t => t.Text).Concat(Tree(editor).OfType<ComboBox>().Select(c => c.Text)).ToList();
                Assert.Contains(texts, t => t.Contains("[A.C]") && t.Contains("Not(X.Y)"));
            });
        });
    }

    [Fact]
    public void TextButton_StillUsesDataHelper()
    {
        Sta.Run(() =>
        {
            var conditionCalled = false;
            var dataCalled = false;
            WithHelpers(_ => { dataCalled = true; return "[A.C]"; }, (_, _) => { conditionCalled = true; return new GuiHelperResult("[X]", IsCondition: true); }, () =>
            {
                var editor = Editor("widget = {\n\ttext = \"[A.B]\"\n}\n", "text");
                HelperButton(editor).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.True(dataCalled);
                Assert.False(conditionCalled);
            });
        });
    }
}
