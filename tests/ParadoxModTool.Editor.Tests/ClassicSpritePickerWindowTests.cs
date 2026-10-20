using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.Editor.Gui;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Sprite-Auswahl (klassische Spiele): Suche über alle Sprites, vorgewählter aktueller Name und Wahl per Doppelklick.</summary>
public sealed class ClassicSpritePickerWindowTests
{
    [Fact]
    public void Lists_each_name_once_sorted()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = new ClassicSpritePickerWindow(env.Assets, null);

            Assert.Equal(new[] { "GFX_a", "GFX_alpha", "GFX_beta", "GFX_gamma_alpha" }, window.ShownNames);
        });
    }

    [Fact]
    public void Filters_by_search_text()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = new ClassicSpritePickerWindow(env.Assets, null);

            window.SearchText = "ALPHA";
            Assert.Equal(new[] { "GFX_alpha", "GFX_gamma_alpha" }, window.ShownNames);

            window.SearchText = "nothing";
            Assert.Empty(window.ShownNames);

            window.SearchText = string.Empty;
            Assert.Equal(4, window.ShownNames.Count);
        });
    }

    [Fact]
    public void Preselects_the_current_sprite()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = new ClassicSpritePickerWindow(env.Assets, "\"GFX_beta\"");

            Assert.Equal("GFX_beta", window.SelectedName);
        });
    }

    [Fact]
    public void Double_click_sets_chosen()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = new ClassicSpritePickerWindow(env.Assets, null);
            window.SearchText = "beta";

            // Ohne Anzeige des Fensters: nur die Liste messen, damit ihre Zeilen entstehen (ein angezeigtes Fenster würde DialogResult verlangen).
            window.List.Measure(new Size(700, 300));
            window.List.Arrange(new Rect(0, 0, 700, 300));
            window.List.UpdateLayout();

            var item = WpfWindows.Descendants<ListViewItem>(window.List).Single();
            // WPF löst MouseDoubleClick (Direct) auf der Liste selbst aus; OriginalSource ist das angeklickte Element.
            window.List.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent, Source = item });

            Assert.Equal("GFX_beta", window.Chosen);
        });
    }

    [Fact]
    public void Choose_without_selection_keeps_chosen_empty()
    {
        Sta.Run(() =>
        {
            using var env = new SpriteTestEnv();
            var window = new ClassicSpritePickerWindow(env.Assets, null);

            Assert.False(window.Choose());
            Assert.Equal(string.Empty, window.Chosen);
            Assert.False(window.OkButton.IsEnabled);

            window.SelectedName = "GFX_alpha";

            Assert.True(window.OkButton.IsEnabled);
            Assert.True(window.Choose());
            Assert.Equal("GFX_alpha", window.Chosen);
        });
    }
}
