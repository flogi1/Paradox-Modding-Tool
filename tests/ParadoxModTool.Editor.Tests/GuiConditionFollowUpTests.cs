using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.DataTypes;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Zurückgestellte Punkte aus dem Review des Bedingungs-Helfers.</summary>
public sealed class GuiConditionFollowUpTests
{
    private static readonly DataTypeDatabase Db = new(new[]
    {
        Entry("AdvanceItem", string.Empty, DataTypeKind.Type, string.Empty),
        Entry("IsResearched", "AdvanceItem", DataTypeKind.Function, "bool"),
        Entry("GetCost", "AdvanceItem", DataTypeKind.Function, "CFixedPoint"),
        Entry("GetName", "AdvanceItem", DataTypeKind.Function, "CString"),
        Entry("GreaterThan_CFixedPoint", string.Empty, DataTypeKind.GlobalFunction, "bool", "Arg0", "Arg1"),
        Entry("GreaterThan_int32", string.Empty, DataTypeKind.GlobalFunction, "bool", "Arg0", "Arg1"),
        Entry("EqualTo_string", string.Empty, DataTypeKind.GlobalFunction, "bool", "Arg0", "Arg1"),
        Entry("EqualTo_int32", string.Empty, DataTypeKind.GlobalFunction, "bool", "Arg0", "Arg1"),
    });

    private sealed class TestSource : PresentationSource
    {
        public override Visual? RootVisual { get; set; }

        public override bool IsDisposed => false;

        protected override CompositionTarget? GetCompositionTargetCore() => null;
    }

    private static DataTypeEntry Entry(string name, string owner, DataTypeKind kind, string returns, params string[] args) =>
        new(name, owner, kind, args, returns, string.Empty, "test");

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

    private static void PressDelete(UIElement view, UIElement source) =>
        view.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, new TestSource(), Environment.TickCount, Key.Delete)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
            Source = source,
        });

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static GuiDataHelperWindow Window(string? start, string condition) =>
        new(new DataTypeLoad(Db, DataTypeStatus.Current, null, Array.Empty<string>()), new GuiPropertyCatalog(), start, condition);

    [Fact]
    public void TakeChainButton_IsResetWhenTheUserReturnsToTheConditionTab()
    {
        Sta.Run(() =>
        {
            var window = Window("AdvanceItem", "[GreaterThan_CFixedPoint(AdvanceItem.GetCost, '(CFixedPoint)1')]");
            var build = Tree(window).OfType<Button>().First(b => b.Content as string == GuiDataTexts.CondBuildChain);
            Click(build);
            var take = Tree(window).OfType<Button>().Single(b => b.Content as string == GuiDataTexts.CondTakeChain);
            Assert.Equal(Visibility.Visible, take.Visibility);

            var tabs = Tree(window).OfType<TabControl>().First();
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(t => t.Header as string == GuiDataTexts.TabCondition);

            Assert.Equal(Visibility.Collapsed, take.Visibility);
        });
    }

    [Fact]
    public void BuildChain_FromAContextMember_StartsAtTheWindowStart()
    {
        Sta.Run(() =>
        {
            var window = Window("AdvanceItem", "[GreaterThan_CFixedPoint(GetCost, '(CFixedPoint)1')]");
            var build = Tree(window).OfType<Button>().First(b => b.Content as string == GuiDataTexts.CondBuildChain);

            Click(build);

            Assert.False(window.ConditionTabActive);
            Assert.Equal("[AdvanceItem.GetCost]", window.CurrentExpression);
        });
    }

    [Fact]
    public void Delete_OnlyInTheTree()
    {
        Sta.Run(() =>
        {
            var tab = new GuiConditionTab(Db, null, "[And(AdvanceItem.IsResearched, AdvanceItem.IsResearched)]");
            tab.Selected = CondPath.Root.Child(0);
            var list = Tree(tab.View).OfType<ListBox>().First();

            PressDelete(tab.View, list);
            Assert.Equal(2, ((LogicNode)tab.Editor.Root!).Items.Count);

            PressDelete(tab.View, Tree(tab.View).OfType<TreeView>().Single());
            Assert.Single(((LogicNode)tab.Editor.Root!).Items);
        });
    }

    [Fact]
    public void StartEmpty_ClearsNodesAddedMeanwhile()
    {
        Sta.Run(() =>
        {
            var tab = new GuiConditionTab(Db, null, "[And(A.B]");
            tab.Editor.Add(null, new RawCondNode("x", null));

            tab.StartEmpty();

            Assert.Null(tab.Editor.Root);
            Assert.True(tab.Editor.CanUndo);
        });
    }

    [Fact]
    public void UntypedNumber_FollowsTheVariant()
    {
        Sta.Run(() =>
        {
            var tab = new GuiConditionTab(Db, null, null);
            var path = tab.Editor.Add(null, new CompareNode(CompareKind.GreaterThan, null, new RawNode("X.Y", null), new RawNode(string.Empty, null), null, null));

            tab.SetConstant(path, 1, "5", null);
            tab.SetVariant(path, "int32");

            Assert.Equal("[GreaterThan_int32(X.Y, '(int32)5')]", tab.Expression);
        });
    }

    [Fact]
    public void TextLeftSide_ChoosesStringVariant()
    {
        Sta.Run(() =>
        {
            var tab = new GuiConditionTab(Db, null, null);
            var path = tab.Editor.Add(null, new CompareNode(CompareKind.EqualTo, null, new RawNode(string.Empty, null), new RawNode(string.Empty, null), null, null));

            tab.SetSide(path, 0, "AdvanceItem.GetName");

            Assert.Equal("string", Assert.IsType<CompareNode>(tab.Editor.NodeAt(path)).Variant);
        });
    }
}
