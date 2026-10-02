using System.Windows.Input;
using ParadoxModTool.Editor.Controls;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public class FreeTextChoiceTests
{
    [Fact]
    public void ChoosingASuggestionSetsTheValue() => Sta.Run(() =>
    {
        var field = new FreeTextChoice();
        field.SetSuggestions(new[] { "a_b", "c" });
        var changes = 0;
        field.ValueChanged += (_, _) => changes++;

        field.Suggestions.Search.Text = "c";
        field.Suggestions.PressInSearch(Key.Enter);

        Assert.Equal("c", field.Value);
        Assert.Equal("c", field.ValueBox.Text);
        Assert.Equal(1, changes);
    });

    [Fact]
    public void TypingKeepsFreeValues() => Sta.Run(() =>
    {
        var field = new FreeTextChoice();
        field.SetSuggestions(new[] { "a_b" });
        var changes = 0;
        field.ValueChanged += (_, _) => changes++;

        field.ValueBox.Text = "frei";

        Assert.Equal("frei", field.Value);
        Assert.Equal(1, changes);
    });

    [Fact]
    public void SettingTheValueInCodeDoesNotReportAChange() => Sta.Run(() =>
    {
        var field = new FreeTextChoice();
        var changes = 0;
        field.ValueChanged += (_, _) => changes++;

        field.Value = "x";

        Assert.Equal("x", field.ValueBox.Text);
        Assert.Equal(0, changes);
    });
}
