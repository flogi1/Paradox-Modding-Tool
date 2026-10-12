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

        // Auffanggrenze gegen grobe Rückschritte: das Release-Werkzeug und test.bat fahren alle Testprojekte gleichzeitig, dann dauert dasselbe mehrfach so lang.
        Assert.True(watch.ElapsedMilliseconds < 5000, $"{watch.ElapsedMilliseconds} ms");
        Assert.Equal(2500, choice.Choice.Items.Count);
    });

    [Fact]
    public void Search_sits_at_the_top_of_the_dropdown_with_placeholder() => Sta.Run(() =>
    {
        var choice = new SearchableChoice();
        choice.SetItems(Items, "CAS", pinned: 1);
        var window = new System.Windows.Window { Content = choice, Left = -3000, Top = -3000, ShowActivated = false, Width = 400, Height = 100 };
        window.Resources.MergedDictionaries.Add(WpfWindows.LoadTheme());
        window.Show();
        WpfWindows.Pump();

        var popup = (System.Windows.Controls.Primitives.Popup)choice.Choice.Template.FindName("PART_Popup", choice.Choice);
        var inPopup = WpfWindows.Descendants<TextBox>(popup.Child).ToList();
        var placeholder = WpfWindows.Descendants<TextBlock>(popup.Child).Select(t => t.Text).ToList();
        var itemsBelow = WpfWindows.Descendants<ScrollViewer>(popup.Child).Any();
        window.Close();

        Assert.Single(choice.Children);
        Assert.Contains(choice.Search, inPopup);
        Assert.Contains(ParadoxModTool.Settings.SettingsTexts.ListSearchPlaceholder, placeholder);
        Assert.True(itemsBelow);
    });

    [Fact]
    public void Opening_the_dropdown_starts_with_an_empty_search_and_keeps_the_choice() => Sta.Run(() =>
    {
        var choice = new SearchableChoice();
        choice.SetItems(Items, "CAS", pinned: 1);
        var window = new System.Windows.Window { Content = choice, Left = -3000, Top = -3000, ShowActivated = false, Width = 400, Height = 100 };
        window.Resources.MergedDictionaries.Add(WpfWindows.LoadTheme());
        window.Show();
        WpfWindows.Pump();

        choice.Search.Text = "sw";
        Assert.Equal(3, choice.Choice.Items.Count);

        choice.Choice.IsDropDownOpen = true;
        WpfWindows.Pump();
        var text = choice.Search.Text;
        choice.Choice.IsDropDownOpen = false;
        WpfWindows.Pump();
        window.Close();

        Assert.Equal(string.Empty, text);
        Assert.Equal(string.Empty, choice.Search.Text);
        Assert.Equal("CAS", choice.SelectedTag);
        Assert.Equal(Items.Length, choice.Choice.Items.Count);
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
