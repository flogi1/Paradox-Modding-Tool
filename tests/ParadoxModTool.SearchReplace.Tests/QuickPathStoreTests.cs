using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

/// <summary>Eigene Schnellzugriff-Knöpfe: beliebig viele, per "+" angelegt, per Rechtsklick verschoben oder entfernt.</summary>
public sealed class QuickPathStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "pmt-quickpaths-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }
    }

    private static QuickPathButtonState Button(string label, string? path) => new() { Label = label, Path = path };

    [Fact]
    public void Without_a_file_there_are_no_own_buttons()
    {
        var settings = QuickPathStore.TryLoad(_file);

        Assert.Empty(settings.CustomButtons);
        Assert.True(settings.Visible);
    }

    [Fact]
    public void More_than_four_buttons_survive_saving_and_loading_in_order()
    {
        var settings = new QuickPathSettings { CustomButtons = Enumerable.Range(1, 6).Select(i => Button($"B{i}", $@"C:\f{i}")).ToList() };

        QuickPathStore.Save(settings, _file);
        var loaded = QuickPathStore.TryLoad(_file);

        Assert.Equal(new[] { "B1", "B2", "B3", "B4", "B5", "B6" }, loaded.CustomButtons.Select(b => b.Label));
    }

    [Fact]
    public void Old_empty_places_without_a_folder_are_dropped_on_load()
    {
        var settings = new QuickPathSettings { CustomButtons = new List<QuickPathButtonState> { Button("Frei 1", null), Button("Mod", @"C:\mod"), Button("Frei 3", null) } };

        QuickPathStore.Save(settings, _file);

        Assert.Equal(new[] { "Mod" }, QuickPathStore.TryLoad(_file).CustomButtons.Select(b => b.Label));
    }

    [Fact]
    public void A_button_without_games_shows_in_every_game_and_a_listed_one_only_there()
    {
        var everywhere = Button("A", "a");
        var onlyCk3 = new QuickPathButtonState { Label = "B", Path = "b", Games = { "ck3" } };

        Assert.True(everywhere.AppliesTo("eu5"));
        Assert.True(onlyCk3.AppliesTo("ck3"));
        Assert.True(onlyCk3.AppliesTo("CK3"));
        Assert.False(onlyCk3.AppliesTo("eu5"));
        Assert.True(onlyCk3.AppliesTo(null));
    }

    [Fact]
    public void Games_survive_saving_and_loading()
    {
        var settings = new QuickPathSettings { CustomButtons = new List<QuickPathButtonState> { new() { Label = "A", Path = "a", Games = { "ck3", "eu5" } }, Button("B", "b") } };

        QuickPathStore.Save(settings, _file);
        var loaded = QuickPathStore.TryLoad(_file).CustomButtons;

        Assert.Equal(new[] { "ck3", "eu5" }, loaded[0].Games);
        Assert.Empty(loaded[1].Games);
    }

    [Fact]
    public void A_file_from_before_the_games_setting_loads_as_every_game()
    {
        File.WriteAllText(_file, "{\"Visible\":true,\"CustomButtons\":[{\"Label\":\"Alt\",\"Path\":\"C:\\\\alt\"}]}");

        var loaded = QuickPathStore.TryLoad(_file).CustomButtons;

        Assert.Single(loaded);
        Assert.Empty(loaded[0].Games);
        Assert.True(loaded[0].AppliesTo("eu4"));
    }

    [Fact]
    public void Null_or_blank_game_entries_in_the_file_are_dropped()
    {
        File.WriteAllText(_file, "{\"CustomButtons\":[{\"Label\":\"X\",\"Path\":\"x\",\"Games\":[\"ck3\",\"\",null,\"CK3\"]},{\"Label\":\"Y\",\"Path\":\"y\",\"Games\":null}]}");

        var loaded = QuickPathStore.TryLoad(_file).CustomButtons;

        Assert.Equal(new[] { "ck3" }, loaded[0].Games);
        Assert.Empty(loaded[1].Games);
    }

    [Fact]
    public void Move_shifts_a_button_and_stops_at_the_ends()
    {
        var settings = new QuickPathSettings { CustomButtons = new List<QuickPathButtonState> { Button("A", "a"), Button("B", "b"), Button("C", "c") } };

        settings.Move(0, +1);
        Assert.Equal(new[] { "B", "A", "C" }, settings.CustomButtons.Select(b => b.Label));

        settings.Move(0, -1);
        settings.Move(2, +1);
        Assert.Equal(new[] { "B", "A", "C" }, settings.CustomButtons.Select(b => b.Label));
    }
}
