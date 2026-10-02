using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Ordner-Filter in der Filterleiste eines Arbeitsbereichs: Häkchen an = nur Tabs aus diesem Ordnerbaum.</summary>
public sealed class FolderFilterBarTests
{
    private static CheckBox FolderBox(EditorPane pane, string name) =>
        ((Panel)pane.FindName("CategoryFilterPanel")).Children.OfType<CheckBox>()
            .Single(c => c.Content is TextBlock text && text.Text == name);

    private static void Toggle(CheckBox box)
    {
        box.IsChecked = box.IsChecked != true;
        box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, box));
    }

    private static List<string?> Visible(EditorPane pane) =>
        CollectionViewSource.GetDefaultView(pane.Documents).Cast<OpenDocument>().Select(d => d.FilePath).ToList();

    [Fact]
    public void A_checked_folder_filter_shows_only_tabs_from_that_folder() => Sta.Run(() =>
    {
        FolderFilterSettings.Load(null);
        try
        {
            FolderFilterSettings.Add("Idea Variation Hauptmod", @"C:\mod\Idea Variation");
            var pane = new EditorPane();
            pane.AddDocument(new OpenDocument(@"C:\mod\Idea Variation\common\a.txt", string.Empty, null), select: false);
            pane.AddDocument(new OpenDocument(@"C:\mod\Idea Variation BT Addon\common\a.txt", string.Empty, null), select: false);
            pane.AddDocument(new OpenDocument(@"C:\mod\Other\b.txt", string.Empty, null), select: true);

            Assert.Equal(3, Visible(pane).Count);

            var box = FolderBox(pane, "Idea Variation Hauptmod");
            Toggle(box);

            Assert.Equal(new[] { @"C:\mod\Idea Variation\common\a.txt", @"C:\mod\Other\b.txt" }, Visible(pane));

            Toggle(box);

            Assert.Equal(3, Visible(pane).Count);
        }
        finally
        {
            FolderFilterSettings.Load(null);
        }
    });

    [Fact]
    public void A_filter_hidden_from_the_bar_has_no_checkbox_and_does_not_filter() => Sta.Run(() =>
    {
        FolderFilterSettings.Load(null);
        try
        {
            var filter = FolderFilterSettings.Add("IV", @"C:\mod\Idea Variation");
            var pane = new EditorPane();
            pane.AddDocument(new OpenDocument(@"C:\mod\Other\b.txt", string.Empty, null), select: false);
            pane.AddDocument(new OpenDocument(@"C:\mod\Idea Variation\a.txt", string.Empty, null), select: true);
            Toggle(FolderBox(pane, "IV"));
            Assert.Single(Visible(pane));

            FolderFilterSettings.SetShown(filter.Id, false);

            Assert.DoesNotContain(((Panel)pane.FindName("CategoryFilterPanel")).Children.OfType<CheckBox>(), c => c.Content is TextBlock { Text: "IV" });
            Assert.Equal(2, Visible(pane).Count);
        }
        finally
        {
            FolderFilterSettings.Load(null);
        }
    });
}
