using System.Windows;
using System.Windows.Controls;

namespace ParadoxModTool.Editor.Controls;

/// <summary>
/// Freier Wert mit durchsuchbaren Vorschlägen: vorn ein Textfeld, dahinter eine <see cref="SearchableChoice"/>. Eine Wahl in den Vorschlägen setzt den
/// Wert; eintippen bleibt jederzeit möglich (Modifier-Parameter der GUI-Bausteine, Gruppe beim Speichern eines Bausteins).
/// </summary>
public sealed class FreeTextChoice : StackPanel
{
    private bool _updating;

    public FreeTextChoice(double valueWidth = 180, double searchWidth = 90, double choiceWidth = 180)
    {
        Orientation = Orientation.Horizontal;
        ValueBox = new TextBox { Width = valueWidth, Margin = new Thickness(0, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center };
        Suggestions = new SearchableChoice(searchWidth, choiceWidth);
        Children.Add(ValueBox);
        Children.Add(Suggestions);
        ValueBox.TextChanged += (_, _) =>
        {
            if (!_updating)
            {
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        Suggestions.SelectionChanged += (_, _) =>
        {
            if (Suggestions.SelectedTag is string chosen && chosen != ValueBox.Text)
            {
                _updating = true;
                ValueBox.Text = chosen;
                _updating = false;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    /// <summary>Getippt oder aus den Vorschlägen gewählt.</summary>
    public event EventHandler? ValueChanged;

    public TextBox ValueBox { get; }

    public SearchableChoice Suggestions { get; }

    /// <summary>Aktueller Wert; Setzen aus dem Code meldet keine Änderung.</summary>
    public string Value
    {
        get => ValueBox.Text;
        set
        {
            _updating = true;
            ValueBox.Text = value;
            _updating = false;
        }
    }

    public void SetSuggestions(IReadOnlyList<string> suggestions) =>
        Suggestions.SetItems(suggestions.Select(s => new ChoiceItem(s, s)).ToList(), null);
}
