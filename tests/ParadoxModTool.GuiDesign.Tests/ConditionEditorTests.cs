using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.DataTypes;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class ConditionEditorTests
{
    private static (ParsedExpression Parsed, ConditionEditor Editor) Load(string text)
    {
        var (parsed, root) = ConditionTreeTests.Read(text);
        return (parsed, new ConditionEditor(root));
    }

    private static string Text(ParsedExpression? parsed, ConditionEditor editor) => ConditionWriter.WriteFull(parsed, editor.Root);

    private static ValueNode Value(string chain) => ConditionTreeTests.Value(chain);

    private static CondPath P(params int[] indices) => new(indices);

    [Fact]
    public void Add_ToEmpty_IsRoot()
    {
        var editor = new ConditionEditor(null);

        Assert.Equal(CondPath.Root, editor.Add(null, Value("A.B")));
        Assert.Equal("[A.B]", Text(null, editor));
    }

    [Fact]
    public void Add_IntoGroup_Appends()
    {
        var (parsed, editor) = Load("[And(A.B, C.D)]");

        Assert.Equal(P(2), editor.Add(CondPath.Root, Value("E.F")));
        Assert.Equal("[And3(A.B, C.D, E.F)]", Text(parsed, editor));
    }

    [Fact]
    public void Add_NextToLeafInGroup_InsertsAfterIt()
    {
        var (parsed, editor) = Load("[And(A.B, C.D)]");

        Assert.Equal(P(1), editor.Add(P(0), Value("E.F")));
        Assert.Equal("[And3(A.B, E.F, C.D)]", Text(parsed, editor));
    }

    [Fact]
    public void Add_NextToLeafWithoutGroup_CreatesAnd()
    {
        var (parsed, editor) = Load("[Not(A.B)]");

        Assert.Equal(P(1), editor.Add(CondPath.Root, Value("C.D")));
        Assert.Equal("[And(Not(A.B), C.D)]", Text(parsed, editor));
    }

    [Fact]
    public void Wrap_InOr()
    {
        var (parsed, editor) = Load("[And(A.B, C.D)]");

        Assert.Equal(P(1), editor.Wrap(P(1), LogicOp.Or));
        var or = Assert.IsType<LogicNode>(editor.NodeAt(P(1)));
        Assert.Equal(LogicOp.Or, or.Op);
        Assert.Single(or.Items);
        editor.Add(P(1), Value("E.F"));
        Assert.Equal("[And(A.B, Or(C.D, E.F))]", Text(parsed, editor));
    }

    [Fact]
    public void ToggleNot_WrapsAndUnwraps()
    {
        var (parsed, editor) = Load("[And(A.B, C.D)]");

        Assert.Equal(P(0), editor.ToggleNot(P(0)));
        Assert.Equal("[And(Not(A.B), C.D)]", Text(parsed, editor));
        editor.ToggleNot(P(0));
        Assert.Equal("[And(A.B, C.D)]", Text(parsed, editor));
    }

    [Fact]
    public void Move_WithinGroup()
    {
        var (parsed, editor) = Load("[And3(A.B, C.D, E.F)]");

        Assert.Equal(P(1), editor.Move(P(0), 1));
        Assert.Equal("[And3(C.D, A.B, E.F)]", Text(parsed, editor));
    }

    [Fact]
    public void Move_OutsideGroup_IsNull()
    {
        var (_, editor) = Load("[And3(A.B, C.D, E.F)]");

        Assert.Null(editor.Move(P(0), -1));
        Assert.Null(editor.Move(P(2), 1));
        Assert.Null(editor.Move(CondPath.Root, 1));
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void Remove_InnerOfNot_RemovesTheNot()
    {
        var (parsed, editor) = Load("[And(Not(A.B), C.D)]");

        Assert.Equal(P(0), editor.Remove(P(0, 0)));
        Assert.Equal("[C.D]", Text(parsed, editor));
    }

    [Fact]
    public void Remove_Root_LeavesEmpty()
    {
        var (parsed, editor) = Load("[A.B]");

        Assert.Null(editor.Remove(CondPath.Root));
        Assert.Null(editor.Root);
        Assert.Equal(string.Empty, Text(parsed, editor));
        Assert.Equal(string.Empty, ConditionWriter.WriteFull(null, null));
    }

    [Fact]
    public void Remove_LastOfGroup_SelectsPreviousSibling()
    {
        var (_, editor) = Load("[And3(A.B, C.D, E.F)]");

        Assert.Equal(P(1), editor.Remove(P(2)));
    }

    [Fact]
    public void Undo_Redo_RestoresRoots()
    {
        var (parsed, editor) = Load("[And(A.B, C.D)]");
        var changes = 0;
        editor.Changed += (_, _) => changes++;

        editor.Remove(P(0));
        editor.ToggleNot(CondPath.Root);
        Assert.Equal("[Not(C.D)]", Text(parsed, editor));
        editor.Undo();
        Assert.Equal("[C.D]", Text(parsed, editor));
        editor.Undo();
        Assert.Equal("[And(A.B, C.D)]", Text(parsed, editor));
        Assert.False(editor.CanUndo);
        editor.Redo();
        Assert.Equal("[C.D]", Text(parsed, editor));
        Assert.True(editor.CanRedo);
        editor.Add(CondPath.Root, Value("E.F"));
        Assert.False(editor.CanRedo);
        Assert.Equal(6, changes);
    }

    [Fact]
    public void Change_KeepsUnchangedSiblingText()
    {
        var (parsed, editor) = Load("[And( A.B , Not( C.D ))]");

        editor.Replace(P(0), Value("E.F"));

        Assert.Equal("[And(E.F, Not( C.D ))]", Text(parsed, editor));
    }

    [Fact]
    public void Merge_AtPath()
    {
        var (parsed, editor) = Load("[And(And(A.B, C.D), E.F)]");

        Assert.Equal(CondPath.Root, editor.Merge(CondPath.Root));
        Assert.Equal("[And3(A.B, C.D, E.F)]", Text(parsed, editor));
    }
}
