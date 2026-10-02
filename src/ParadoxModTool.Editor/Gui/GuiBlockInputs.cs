using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Blocks;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Eingaben einer Ebene eines Bausteins: ein Textfeld je Parameter, je Wiederholung eine Anzahl und eine Tabelle (eine Zeile je Eintrag, eine Spalte je
/// Parameter). Unterwiederholungen erscheinen unter der Tabelle für den dort gewählten Eintrag der äußeren. Jede Änderung meldet <see cref="Changed"/>.
/// </summary>
public sealed class GuiBlockInputs : StackPanel
{
    private readonly GuiBlock _block;
    private readonly GuiBlockValues _values;

    public GuiBlockInputs(GuiBlock block, GuiBlockValues values, IReadOnlyList<GuiBlockParameter> parameters, IReadOnlyList<GuiBlockRepeat> repeats)
    {
        _block = block;
        _values = values;
        if (parameters.Count > 0)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            foreach (var parameter in parameters)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var row = grid.RowDefinitions.Count - 1;
                var label = new TextBlock { Text = parameter.Label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 8, 2) };
                Grid.SetRow(label, row);
                grid.Children.Add(label);
                var box = ValueBox(values, parameter);
                Grid.SetRow(box, row);
                Grid.SetColumn(box, 1);
                grid.Children.Add(box);
            }

            Children.Add(grid);
        }

        foreach (var repeat in repeats)
        {
            Children.Add(new RepeatEditor(this, repeat));
        }
    }

    public event EventHandler? Changed;

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static (Modifiers.ModifierFormatIndex Index, IReadOnlyList<string> Names)? _modifierNames;

    /// <summary>Modifier-Namen der Datenbank des Spiels, sortiert; einmal je Index berechnet.</summary>
    private static IReadOnlyList<string> ModifierNames()
    {
        var index = ModifierValueSettings.Index;
        if (_modifierNames is not { } cached || !ReferenceEquals(cached.Index, index))
        {
            cached = (index, index.Names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList());
            _modifierNames = cached;
        }

        return cached.Names;
    }

    private FrameworkElement ValueBox(GuiBlockValues values, GuiBlockParameter parameter)
    {
        var text = values.Values.TryGetValue(parameter.Key, out var current) ? current : string.Empty;
        if (parameter.Kind == GuiBlockParameterKind.Modifier)
        {
            // Parameterart modifier: freie Eingabe plus durchsuchbare Auswahl aus der Modifier-Datenbank (Teil-2-Ruling 8).
            var field = new Controls.FreeTextChoice(140, 70, 170) { Margin = new Thickness(0, 2, 4, 2), ToolTip = GuiTexts.BlocksModifierTip };
            field.SetSuggestions(ModifierNames());
            field.Value = text;
            field.ValueChanged += (_, _) =>
            {
                values.Values[parameter.Key] = field.Value;
                RaiseChanged();
            };
            return field;
        }

        var box = new TextBox
        {
            Text = text,
            MinWidth = 140,
            Margin = new Thickness(0, 2, 4, 2),
            ToolTip = parameter.Key,
        };
        box.TextChanged += (_, _) =>
        {
            values.Values[parameter.Key] = box.Text;
            RaiseChanged();
        };
        return box;
    }

    /// <summary>Anzahl, Tabelle und die Unterwiederholungen einer Wiederholung; baut bei geänderter Anzahl nur sich selbst neu auf.</summary>
    private sealed class RepeatEditor : StackPanel
    {
        private readonly GuiBlockInputs _owner;
        private readonly GuiBlockRepeat _repeat;
        private readonly StackPanel _table = new();

        public RepeatEditor(GuiBlockInputs owner, GuiBlockRepeat repeat)
        {
            _owner = owner;
            _repeat = repeat;
            Margin = new Thickness(0, 4, 0, 6);
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            header.Children.Add(new TextBlock { Text = repeat.Label, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            header.Children.Add(new TextBlock { Text = GuiTexts.BlocksCount, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            var count = new TextBox { Text = Entries.Count.ToString(CultureInfo.InvariantCulture), Width = 44, ToolTip = repeat.Key };
            count.TextChanged += (_, _) =>
            {
                if (int.TryParse(count.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wanted))
                {
                    owner._values.Resize(owner._block, repeat.Key, wanted);
                    Rebuild();
                    owner.RaiseChanged();
                }
            };
            count.LostFocus += (_, _) => count.Text = Entries.Count.ToString(CultureInfo.InvariantCulture);
            header.Children.Add(count);
            Children.Add(header);
            Children.Add(_table);
            Rebuild();
        }

        private IReadOnlyList<GuiBlockValues> Entries =>
            _owner._values.Repeats.TryGetValue(_repeat.Key, out var entries) ? entries : Array.Empty<GuiBlockValues>();

        private void Rebuild()
        {
            _table.Children.Clear();
            var entries = Entries;
            if (entries.Count == 0)
            {
                return;
            }

            if (_repeat.Parameters.Count > 0)
            {
                _table.Children.Add(Table(entries));
            }

            foreach (var sub in _repeat.Repeats)
            {
                _table.Children.Add(SubRepeat(entries, sub));
            }
        }

        private Grid Table(IReadOnlyList<GuiBlockValues> entries)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foreach (var parameter in _repeat.Parameters)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var column = 0; column < _repeat.Parameters.Count; column++)
            {
                var title = new TextBlock { Text = _repeat.Parameters[column].Label, Margin = new Thickness(0, 0, 4, 2) };
                title.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
                Grid.SetColumn(title, column + 1);
                grid.Children.Add(title);
            }

            foreach (var entry in entries)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var row = grid.RowDefinitions.Count - 1;
                var number = new TextBlock { Text = entry.Index.ToString(CultureInfo.InvariantCulture), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                number.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
                Grid.SetRow(number, row);
                grid.Children.Add(number);
                for (var column = 0; column < _repeat.Parameters.Count; column++)
                {
                    var box = _owner.ValueBox(entry, _repeat.Parameters[column]);
                    box.MinWidth = 90;
                    Grid.SetRow(box, row);
                    Grid.SetColumn(box, column + 1);
                    grid.Children.Add(box);
                }
            }

            return grid;
        }

        /// <summary>Unterwiederholung: Auswahl des äußeren Eintrags, darunter dessen Eingaben (eigene Anzahl und Tabelle).</summary>
        private FrameworkElement SubRepeat(IReadOnlyList<GuiBlockValues> entries, GuiBlockRepeat sub)
        {
            var panel = new StackPanel { Margin = new Thickness(16, 6, 0, 0) };
            // Texteinträge statt ComboBoxItems mit TextBlock: Deren Auswahlanzeige (VisualBrush) ließ das Layout endlos breiter werden.
            var choice = new ComboBox { MinWidth = 140, Margin = new Thickness(0, 0, 0, 2), HorizontalAlignment = HorizontalAlignment.Left };
            choice.ItemsSource = entries.Select(EntryLabel).ToList();

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = GuiTexts.BlocksEntryFor(sub.Label), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            header.Children.Add(choice);
            panel.Children.Add(header);
            var host = new ContentControl();
            panel.Children.Add(host);
            choice.SelectionChanged += (_, _) =>
            {
                if (choice.SelectedIndex >= 0 && choice.SelectedIndex < entries.Count)
                {
                    var entry = entries[choice.SelectedIndex];
                    var inputs = new GuiBlockInputs(_owner._block, entry, Array.Empty<GuiBlockParameter>(), new[] { sub });
                    inputs.Changed += (_, _) => _owner.RaiseChanged();
                    host.Content = inputs;
                }
            };
            choice.SelectedIndex = 0;
            return panel;
        }

        private string EntryLabel(GuiBlockValues entry)
        {
            var first = _repeat.Parameters.FirstOrDefault();
            return first is not null && entry.Values.TryGetValue(first.Key, out var value) && value.Length > 0
                ? value
                : "#" + entry.Index.ToString(CultureInfo.InvariantCulture);
        }
    }
}
