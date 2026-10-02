using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.Editor.Controls;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public class SearchableChoiceTests
{
    private static readonly (string Text, object? Tag)[] Items =
    {
        ("Alle Länder", null), ("ARA", "ARA"), ("CAS", "CAS"), ("NAV", "NAV"), ("POR", "POR"), ("SWE", "SWE"),
    };

    private static string[] Visible(SearchableChoice choice) =>
        choice.Choice.Items.Cast<ChoiceItem>().Select(i => i.Text).ToArray();

    [Fact]
    public void SearchKeepsPinnedAndMatchingItemsIgnoringCase() => Sta.Run(() =>
    {
        var choice = new SearchableChoice();
        choice.SetItems(Items, null, pinned: 1);

        choice.Search.Text = "a";

        Assert.Equal(new[] { "Alle Länder", "ARA", "CAS", "NAV" }, Visible(choice));
        choice.Search.Text = string.Empty;
        Assert.Equal(6, Visible(choice).Length);
    });

    [Fact]
    public void SelectedItemStaysVisibleAndSelectedWhileFiltered() => Sta.Run(() =>
    {
        var choice = new SearchableChoice();
        choice.SetItems(Items, "SWE", pinned: 1);

        choice.Search.Text = "ca";

        Assert.Equal(new[] { "Alle Länder", "CAS", "SWE" }, Visible(choice));
        Assert.Equal("SWE", choice.SelectedTag);
    });

    [Fact]
    public void EnterSelectsTheFirstMatchAndReportsTheChange() => Sta.Run(() =>
    {
        var choice = new SearchableChoice();
        choice.SetItems(Items, null, pinned: 1);
        var changes = 0;
        choice.SelectionChanged += (_, _) => changes++;

        choice.Search.Text = "wE";
        choice.PressInSearch(Key.Enter);

        Assert.Equal("SWE", choice.SelectedTag);
        Assert.Equal(1, changes);
    });

    [Fact]
    public void EscapeClearsTheSearch() => Sta.Run(() =>
    {
        var choice = new SearchableChoice();
        choice.SetItems(Items, null, pinned: 1);
        choice.Search.Text = "sw";

        choice.PressInSearch(Key.Escape);

        Assert.Equal(string.Empty, choice.Search.Text);
        Assert.Equal(6, Visible(choice).Length);
    });

    [Fact]
    public void SettingItemsOrFilteringDoesNotReportAChange() => Sta.Run(() =>
    {
        var choice = new SearchableChoice();
        var changes = 0;
        choice.SelectionChanged += (_, _) => changes++;

        choice.SetItems(Items, "CAS", pinned: 1);
        choice.Search.Text = "s";
        choice.Search.Text = string.Empty;

        Assert.Equal(0, changes);
        Assert.Equal("CAS", choice.SelectedTag);
    });

    [Fact]
    public void UnknownSelectionPicksThePinnedItemOrNothing() => Sta.Run(() =>
    {
        var pinned = new SearchableChoice();
        pinned.SetItems(Items, "XYZ", pinned: 1);
        Assert.Equal("Alle Länder", ((ChoiceItem)pinned.Choice.SelectedItem).Text);

        var plain = new SearchableChoice();
        plain.SetItems(new[] { new ChoiceItem("a.eu5", "a", Enabled: false, ToolTip: "nicht lesbar"), new ChoiceItem("b.eu5", "b") }, null);
        Assert.Null(plain.Choice.SelectedItem);
        Assert.Null(plain.SelectedTag);
        Assert.False(((ChoiceItem)plain.Choice.Items[0]).Enabled);

        plain.Search.Text = ".eu5";
        plain.PressInSearch(Key.Enter);
        Assert.Equal("b", plain.SelectedTag);
    });

    [Fact]
    public void PickingAnItemInTheListReportsTheChange() => Sta.Run(() =>
    {
        var choice = new SearchableChoice();
        choice.SetItems(Items, null, pinned: 1);
        var changes = 0;
        choice.SelectionChanged += (_, _) => changes++;

        choice.Choice.SelectedIndex = 2;

        Assert.Equal("CAS", choice.SelectedTag);
        Assert.Equal(1, changes);
    });

    [Fact]
    public void UnderscoresStayVisible() => Sta.Run(() =>
    {
        var choice = new SearchableChoice();
        choice.SetItems(new[] { ("bank_loan_cost", (object?)"x") }, "x");
        var window = new System.Windows.Window { Content = choice, Left = -3000, Top = -3000, ShowActivated = false, Width = 400, Height = 100 };
        window.Resources.MergedDictionaries.Add(WpfWindows.LoadTheme());
        window.Show();
        WpfWindows.Pump();

        var shown = WpfWindows.Descendants<TextBlock>(choice.Choice).Select(t => t.Text).ToList();
        window.Close();

        Assert.Contains("bank_loan_cost", shown);
    });

    [Fact]
    public void SearchableChoice_ManyItems_FiltersQuickly() => Sta.Run(() =>
    {
        var items = Enumerable.Range(0, 2500).Select(i => ($"modifier_number_{i}", (object?)i)).ToList();
        var choice = new SearchableChoice();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        choice.SetItems(items, null);
        foreach (var text in new[] { "m", "mo", "number_1", "number_12", string.Empty })
        {
            choice.Search.Text = text;
        }

        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
        Assert.Equal(2500, choice.Choice.Items.Count);
    });

    [Fact]
    public void Window_WithSearchableChoice_KeepsItsSize() => Sta.Run(() =>
    {
        var choice = new SearchableChoice();
        choice.SetItems(Items, "CAS", pinned: 1);
        var window = new System.Windows.Window { Content = choice, SizeToContent = System.Windows.SizeToContent.WidthAndHeight };

        WpfWindows.AssertKeepsItsSizeWhenChoicesAreUsed(window);
    });
}
