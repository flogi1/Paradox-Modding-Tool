using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ParadoxModTool.Settings;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class WorkspaceToolbarTests
{
    private static Button ButtonNamed(EditorPane pane, string name) => (Button)pane.FindName(name);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

    [Fact]
    public void ToolbarHasTheSpecOrderAndTexts() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        var bar = (Panel)ButtonNamed(pane, "NewButton").Parent;

        var labels = bar.Children.OfType<Button>().Select(button => button.Content as string).ToList();

        Assert.Equal(
            new[]
            {
                MenuTexts.WorkspaceNew, MenuTexts.WorkspaceOpen, MenuTexts.WorkspaceSave, MenuTexts.WorkspaceSaveAll, MenuTexts.WorkspaceClose,
                MenuTexts.WorkspaceMove, MenuTexts.WorkspaceCategory, MenuTexts.WorkspaceFilter, MenuTexts.WorkspaceIcons,
            },
            labels);
        Assert.Equal(MenuTexts.WorkspaceSaveAllTip, ButtonNamed(pane, "SaveAllButton").ToolTip);
    });

    [Fact]
    public void SaveAllButtonRaisesTheEvent() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        var raised = 0;
        pane.SaveAllRequested += (_, _) => raised++;

        Click(ButtonNamed(pane, "SaveAllButton"));

        Assert.Equal(1, raised);
    });

    [Fact]
    public void CloseButtonAsksToCloseTheActiveDocument() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        pane.NewDocument();
        var active = pane.NewDocument();
        OpenDocument? requested = null;
        pane.CloseDocumentRequested += (_, document) => requested = document;

        Click(ButtonNamed(pane, "CloseDocumentButton"));

        Assert.Same(active, requested);
    });

    [Fact]
    public void ToolbarButtonsCanBeHiddenSeparately() => Sta.Run(() =>
    {
        var pane = new EditorPane();

        pane.SetToolbarButtonsVisible(filterButton: false, iconButton: true);

        Assert.Equal(Visibility.Collapsed, ButtonNamed(pane, "FilterBarButton").Visibility);
        Assert.Equal(Visibility.Visible, ButtonNamed(pane, "TabIconsButton").Visibility);
    });

    [Fact]
    public void DropdownsStayVisibleWhenTheDocumentBarIsHidden() => Sta.Run(() =>
    {
        var pane = new EditorPane();

        pane.SetDocumentBarVisible(false);

        Assert.Equal(Visibility.Visible, ButtonNamed(pane, "FilterBarButton").Visibility);
        Assert.Equal(Visibility.Visible, ButtonNamed(pane, "TabIconsButton").Visibility);
        Assert.Same(ButtonNamed(pane, "NewButton").Parent, ButtonNamed(pane, "FilterBarButton").Parent);
    });

    [Fact]
    public void PathBarCanBeHiddenWithItsButtons() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        var bar = (FrameworkElement)((FrameworkElement)pane.FindName("PathText")).Parent;
        Assert.Same(bar, ButtonNamed(pane, "CopyPathButton").Parent);
        Assert.Same(bar, ButtonNamed(pane, "ShowNoteButton").Parent);

        pane.SetPathBarVisible(false);
        Assert.Equal(Visibility.Collapsed, bar.Visibility);

        pane.SetPathBarVisible(true);
        Assert.Equal(Visibility.Visible, bar.Visibility);
    });
}
