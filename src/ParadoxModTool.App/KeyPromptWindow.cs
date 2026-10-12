using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators;
using ParadoxModTool.Localization;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>
/// "Neuer Stand" bei zusammengesetzten Schlüsseln (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 3): je Schlüsselfeld eine Eingabe, eine Auswahl
/// als Liste, sonst Text mit Musterprüfung. Liefert den zusammengesetzten Schlüssel. Ein Feld mit <c>autoNumber</c> bekommt die nächste freie Nummer
/// zu den übrigen Teilen vorgegeben (mit jeder Änderung dieser Teile neu); der Nutzer darf sie überschreiben.
/// </summary>
internal sealed class KeyPromptWindow : Window
{
    private readonly GeneratorPackage _package;
    private readonly IReadOnlyList<string> _takenKeys;
    private readonly List<(FieldDef Field, Func<string> Value)> _inputs = new();
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 8) };
    private string? _key;

    private KeyPromptWindow(GeneratorPackage package, IReadOnlyList<string> takenKeys)
    {
        _package = package;
        _takenKeys = takenKeys;
        Title = GeneratorTexts.NewKeyTitle;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = GeneratorTexts.NewKeyMessage, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        TextBox? numberBox = null;
        foreach (var id in package.KeyFields)
        {
            var field = package.Head.First(f => f.Id == id);
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var label = new TextBlock { Text = field.Label, Width = 140, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            if (field.Type == FieldType.Choice && field.Choices is { Count: > 0 } choices)
            {
                var combo = new ComboBox { ItemsSource = choices, SelectedIndex = 0 };
                combo.SelectionChanged += (_, _) => FillAutoNumber(numberBox);
                row.Children.Add(combo);
                _inputs.Add((field, () => combo.SelectedItem as string ?? string.Empty));
            }
            else
            {
                var box = new TextBox { VerticalContentAlignment = VerticalAlignment.Center };
                if (field.AutoNumber)
                {
                    numberBox = box;
                }
                else
                {
                    box.TextChanged += (_, _) => FillAutoNumber(numberBox);
                }

                row.Children.Add(box);
                _inputs.Add((field, () => box.Text.Trim()));
            }

            panel.Children.Add(row);
        }

        _error.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
        panel.Children.Add(_error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var ok = new Button { Content = new TextBlock { Text = CommonTexts.Ok }, Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = new TextBlock { Text = CommonTexts.Cancel }, Width = 80, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
        FillAutoNumber(numberBox);
    }

    /// <summary>Setzt die nächste freie Nummer zu den übrigen Schlüsselteilen in das Feld mit <c>autoNumber</c>; ohne ein solches Feld passiert nichts.</summary>
    private void FillAutoNumber(TextBox? numberBox)
    {
        if (numberBox is null)
        {
            return;
        }

        var others = _inputs.Where(i => !i.Field.AutoNumber).ToDictionary(i => i.Field.Id, i => i.Value(), StringComparer.Ordinal);
        numberBox.Text = AutoNumber.Next(_package, _takenKeys, others).ToString(CultureInfo.InvariantCulture);
    }

    private void Accept()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (field, value) in _inputs)
        {
            var text = value();
            var valid = text.Length > 0
                && (field.Type != FieldType.Number || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                && (string.IsNullOrEmpty(field.Pattern) || Regex.IsMatch(text, field.Pattern, RegexOptions.CultureInvariant));
            if (!valid)
            {
                _error.Text = GeneratorTexts.NewKeyInvalid(field.Label);
                _error.Visibility = Visibility.Visible;
                return;
            }

            values[field.Id] = text;
        }

        _key = PackageKey.Compose(_package, values);
        DialogResult = true;
    }

    /// <summary>Fragt die Schlüsselfelder ab; <see langword="null"/> bei Abbruch. <paramref name="takenKeys"/>: vergebene Schlüssel für die Nummer-Vorgabe.</summary>
    public static string? Ask(Window owner, GeneratorPackage package, IReadOnlyList<string> takenKeys)
    {
        var window = new KeyPromptWindow(package, takenKeys) { Owner = owner };
        return window.ShowDialog() == true ? window._key : null;
    }
}
