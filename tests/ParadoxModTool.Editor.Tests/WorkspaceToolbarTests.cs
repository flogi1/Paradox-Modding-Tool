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
                MenuTexts.WorkspaceMove, MenuTexts.WorkspaceCategory, MenuTexts.WorkspaceFilter, MenuTexts.WorkspaceIcons, MenuTexts.WorkspaceSort, MenuTexts.WorkspaceNotes,
                MenuTexts.ToolbarLabel(ToolbarItemIds.Bookmark), MenuTexts.ToolbarLabel(ToolbarItemIds.Bug),
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

        pane.SetToolbarLayout(classic: true, category: true, filter: false, icons: true, sort: true, marks: true);

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

    [Fact]
    public void TilesLayoutHidesFileButtonsAndShowsMarksAndName() => Sta.Run(() =>
    {
        var pane = new EditorPane { PaneName = "B" };
        pane.SetToolbarLayout(classic: false, category: true, filter: false, icons: true, sort: false, marks: true);

        Assert.Equal(Visibility.Collapsed, ButtonNamed(pane, "NewButton").Visibility);
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)pane.FindName("FileSeparator")).Visibility);
        Assert.Equal(Visibility.Collapsed, ButtonNamed(pane, "FilterBarButton").Visibility);
        Assert.Equal(Visibility.Collapsed, ButtonNamed(pane, "SortButton").Visibility);
        Assert.Equal(Visibility.Visible, ButtonNamed(pane, "NoteButton").Visibility);
        Assert.Equal(MenuTexts.ToolbarLabel(ToolbarItemIds.Bug), ButtonNamed(pane, "BugButton").Content);
        var name = (TextBlock)pane.FindName("PaneNameText");
        Assert.Equal(Visibility.Visible, name.Visibility);
        Assert.Equal(EditorTexts.WorkspaceName("B"), name.Text);
    });

    [Fact]
    public void ClassicLayoutShowsFileButtonsAndNoMarks() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        pane.SetToolbarLayout(classic: true, category: true, filter: true, icons: true, sort: true, marks: true);
        Assert.Equal(Visibility.Visible, ButtonNamed(pane, "NewButton").Visibility);
        Assert.Equal(Visibility.Collapsed, ButtonNamed(pane, "NoteButton").Visibility);
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)pane.FindName("PaneNameText")).Visibility);
    });

    [Theory]
    // classic, Menü-Knöpfe an, Markierungen an → FileSeparator, MarkSeparator
    [InlineData(true, true, true, true, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(false, true, true, false, true)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, true, false, false, false)]
    public void SeparatorsOnlyStandBetweenVisibleButtons(bool classic, bool menus, bool marks, bool fileSeparator, bool markSeparator) => Sta.Run(() =>
    {
        var pane = new EditorPane();
        pane.SetToolbarLayout(classic, category: menus, filter: menus, icons: menus, sort: menus, marks: marks);

        Assert.Equal(fileSeparator ? Visibility.Visible : Visibility.Collapsed, ((FrameworkElement)pane.FindName("FileSeparator")).Visibility);
        Assert.Equal(markSeparator ? Visibility.Visible : Visibility.Collapsed, ((FrameworkElement)pane.FindName("MarkSeparator")).Visibility);
    });

    [Fact]
    public void OneMenuButtonIsEnoughForTheSeparators() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        pane.SetToolbarLayout(classic: true, category: false, filter: false, icons: false, sort: true, marks: true);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)pane.FindName("FileSeparator")).Visibility);

        pane.SetToolbarLayout(classic: false, category: false, filter: true, icons: false, sort: false, marks: true);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)pane.FindName("MarkSeparator")).Visibility);
    });

    [Fact]
    public void MarkButtonsRaiseTheirEvents() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        var log = new List<string>();
        pane.NoteRequested += (_, _) => log.Add("note");
        pane.BookmarkRequested += (_, _) => log.Add("bookmark");
        pane.BugRequested += (_, _) => log.Add("bug");
        Click(ButtonNamed(pane, "NoteButton"));
        Click(ButtonNamed(pane, "BookmarkButton"));
        Click(ButtonNamed(pane, "BugButton"));
        Assert.Equal(new[] { "note", "bookmark", "bug" }, log);
    });
}
