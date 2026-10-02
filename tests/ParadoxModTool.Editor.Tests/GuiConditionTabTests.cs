using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.DataTypes;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Tab "Bedingung" im GUI-Daten-Helfer: Laden, Bearbeiten, Variante nach Typ, Ergebnis des Fensters.</summary>
public sealed class GuiConditionTabTests
{
    private static readonly DataTypeDatabase Db = new(new[]
    {
        Entry("AdvanceItem", string.Empty, DataTypeKind.Type, string.Empty),
        Entry("IsResearched", "AdvanceItem", DataTypeKind.Function, "bool"),
        Entry("GetCost", "AdvanceItem", DataTypeKind.Function, "CFixedPoint"),
        Entry("GreaterThan_CFixedPoint", string.Empty, DataTypeKind.GlobalFunction, "bool", "Arg0", "Arg1"),
        Entry("GreaterThan_int32", string.Empty, DataTypeKind.GlobalFunction, "bool", "Arg0", "Arg1"),
    });

    private static DataTypeEntry Entry(string name, string owner, DataTypeKind kind, string returns, params string[] args) =>
        new(name, owner, kind, args, returns, string.Empty, "test");

    [Fact]
    public void Load_ShowsTreeAndPlainText()
    {
        Sta.Run(() =>
        {
            var tab = new GuiConditionTab(Db, null, "\"[Not(AdvanceItem.IsResearched)]\"");

            Assert.IsType<NotNode>(tab.Editor.Root);
            Assert.False(tab.LoadFailed);
            Assert.Equal("\"[Not(AdvanceItem.IsResearched)]\"", tab.Expression);
            Assert.Contains("nicht AdvanceItem › Is Researched", tab.PlainText);
            Assert.Equal(2, tab.TreeItemCount);
        });
    }

    [Fact]
    public void Load_Broken_ShowsBanner_AndChoices()
    {
        Sta.Run(() =>
        {
            var empty = new GuiConditionTab(Db, null, "[And(A.B]");
            Assert.True(empty.LoadFailed);
            empty.StartEmpty();
            Assert.False(empty.LoadFailed);
            Assert.Null(empty.Editor.Root);
            Assert.Equal(string.Empty, empty.Expression);

            var raw = new GuiConditionTab(Db, null, "[And(A.B]");
            raw.TakeAsRaw();
            Assert.Equal("And(A.B", Assert.IsType<RawCondNode>(raw.Editor.Root).Text);
            Assert.Equal("[And(A.B]", raw.Expression);
        });
    }

    [Fact]
    public void AddAndUndo_ChangeExpression()
    {
        Sta.Run(() =>
        {
            var tab = new GuiConditionTab(Db, null, "[Not(AdvanceItem.IsResearched)]");
            var changed = 0;
            tab.ExpressionChanged += (_, _) => changed++;

            tab.Editor.Add(CondPath.Root, new ValueNode(DataExpressionParser.ParseDetached("A.B")!, null));
            Assert.Equal("[And(Not(AdvanceItem.IsResearched), A.B)]", tab.Expression);
            Assert.Equal(4, tab.TreeItemCount);
            tab.Editor.Undo();
            Assert.Equal("[Not(AdvanceItem.IsResearched)]", tab.Expression);
            Assert.Equal(2, changed);
        });
    }

    [Fact]
    public void CompareVariant_FollowsLeftSide()
    {
        Sta.Run(() =>
        {
            var tab = new GuiConditionTab(Db, null, null);
            var path = tab.Editor.Add(null, new CompareNode(CompareKind.GreaterThan, null, new RawNode(string.Empty, null), new RawNode(string.Empty, null), null, null));

            tab.SetSide(path, 0, "AdvanceItem.GetCost");
            Assert.Equal("CFixedPoint", Assert.IsType<CompareNode>(tab.Editor.NodeAt(path)).Variant);
            tab.SetConstant(path, 1, "1", null);
            Assert.Equal("[GreaterThan_CFixedPoint(AdvanceItem.GetCost, '(CFixedPoint)1')]", tab.Expression);

            tab.SetVariant(path, "int32");
            Assert.Equal("[GreaterThan_int32(AdvanceItem.GetCost, '(int32)1')]", tab.Expression);
            Assert.Contains(tab.Hints, h => h.Kind == ConditionHintKind.TypeMismatch);
        });
    }

    [Fact]
    public void RawText_BecomesNodesWhenValid()
    {
        Sta.Run(() =>
        {
            var tab = new GuiConditionTab(Db, null, "[And(AdvanceItem.IsResearched, $p$)]");

            Assert.Contains(tab.Hints, h => h.Kind == ConditionHintKind.RawText);
            tab.SetRaw(CondPath.Root.Child(1), "Not(AdvanceItem.IsResearched)");
            Assert.Equal("[And(AdvanceItem.IsResearched, Not(AdvanceItem.IsResearched))]", tab.Expression);
            Assert.IsType<NotNode>(tab.Editor.NodeAt(CondPath.Root.Child(1)));
        });
    }

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

    private static void LoseFocus(UIElement element) =>
        element.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, element, null) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });

    private static (GuiConditionTab Tab, CondPath Path) Comparison(ExprNode right)
    {
        var tab = new GuiConditionTab(Db, null, null);
        var path = tab.Editor.Add(null, new CompareNode(CompareKind.GreaterThan, "CFixedPoint", DataExpressionParser.ParseDetached("AdvanceItem.GetCost")!, right, null, null));
        tab.Selected = path;
        return (tab, path);
    }

    [Fact]
    public void SwitchingToConstant_AppliesTheSide()
    {
        Sta.Run(() =>
        {
            var (tab, _) = Comparison(new RawNode("5", null));

            var constants = Tree(tab.View).OfType<RadioButton>().Where(r => r.Content as string == GuiDataTexts.CondConstantMode).ToList();
            constants[1].IsChecked = true;

            Assert.Equal("[GreaterThan_CFixedPoint(AdvanceItem.GetCost, '(CFixedPoint)5')]", tab.Expression);
        });
    }

    [Fact]
    public void RawText_AppliesWhenFocusLeaves()
    {
        Sta.Run(() =>
        {
            var tab = new GuiConditionTab(Db, null, "[$p$]");
            tab.Selected = CondPath.Root;
            var box = Tree(tab.View).OfType<TextBox>().Single(t => t.Text == "$p$" && !t.IsReadOnly);

            box.Text = "Not(AdvanceItem.IsResearched)";
            LoseFocus(box);

            Assert.IsType<NotNode>(tab.Editor.Root);
        });
    }

    [Fact]
    public void SideEditor_AppliesOnce_EvenWhenTheOldFieldLosesFocusAgain()
    {
        Sta.Run(() =>
        {
            var (tab, path) = Comparison(new RawNode(string.Empty, null));
            var right = Tree(tab.View).OfType<TextBox>().Where(t => t.Text.Length == 0).First();

            right.Text = "AdvanceItem.GetCost";
            LoseFocus(right);
            LoseFocus(right);
            tab.Editor.Undo();

            Assert.IsType<RawNode>(Assert.IsType<CompareNode>(tab.Editor.NodeAt(path)).Right);
        });
    }

    [Fact]
    public void Window_ResultComesFromActiveTab()
    {
        Sta.Run(() =>
        {
            var load = new DataTypeLoad(Db, DataTypeStatus.Current, null, Array.Empty<string>());
            var window = new GuiDataHelperWindow(load, new GuiPropertyCatalog(), null, "[Not(AdvanceItem.IsResearched)]");

            Assert.True(window.ConditionTabActive);
            Assert.Equal("[Not(AdvanceItem.IsResearched)]", window.CurrentExpression);

            var plain = new GuiDataHelperWindow(load, new GuiPropertyCatalog(), "AdvanceItem");
            Assert.False(plain.ConditionTabActive);
            Assert.Equal("[AdvanceItem]", plain.CurrentExpression);
        });
    }
}
