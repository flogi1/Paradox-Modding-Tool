using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Localization;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>
/// Mehrsprachiger Text im Paket-Editor (Spec Teil 5a, Abschnitt 2): Feld für die Sprache, die die UI zeigt, und Knopf "Sprachen" mit allen 8
/// UI-Sprachen. Eine Steuerung für alle Masken.
/// </summary>
internal sealed class LocalizedTextBox : DockPanel
{
    public static readonly IReadOnlyList<string> UiLanguages = new[] { "de", "en", "fr", "es", "ru", "pl", "pt-br", "zh-hans" };

    private readonly TextBox _box = new() { VerticalContentAlignment = VerticalAlignment.Center };
    private readonly string _label;
    private LocalizedText _value = LocalizedText.Empty;
    private LocalizedTextEditor _editor = new(LocalizedText.Empty, UiText.Current.Language);
    private bool _setting;

    public LocalizedTextBox(string label, LocalizedText value, string? tooltip = null)
    {
        _label = label;
        var languages = new Button { Content = new TextBlock { Text = PackageEditorTexts.Languages }, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(4, 0, 0, 0), ToolTip = PackageEditorTexts.LanguagesTip };
        languages.Click += (_, _) => EditLanguages();
        SetDock(languages, Dock.Right);
        Children.Add(languages);
        Children.Add(_box);
        _box.ToolTip = tooltip;
        _box.TextChanged += (_, _) =>
        {
            if (_setting)
            {
                return;
            }

            _value = _editor.Type(_box.Text);
            ValueChanged?.Invoke(_value);
        };
        Value = value;
    }

    /// <summary>Nach jeder Änderung (Tippen oder Sprachen-Dialog).</summary>
    public event Action<LocalizedText>? ValueChanged;

    public LocalizedText Value
    {
        get => _value;
        set
        {
            // Die bearbeitete Sprache steht ab hier fest, bis ein neuer Wert kommt (Spec: kein Überschreiben von Englisch nach dem Leeren).
            _value = value;
            _editor = new LocalizedTextEditor(value, UiText.Current.Language);
            _setting = true;
            _box.Text = value.Resolve(UiText.Current.Language);
            _setting = false;
        }
    }

    public bool ReadOnly
    {
        set => IsEnabled = !value;
    }

    private void EditLanguages()
    {
        var current = _value.Values.ToDictionary(p => p.Key.Length == 0 ? "en" : p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        if (TranslationsDialog.Ask(Window.GetWindow(this)!, PackageEditorTexts.LanguagesTitle(_label), UiLanguages, current, multiline: false) is not { } result)
        {
            return;
        }

        Value = _value.WithLanguages(UiLanguages.Where(result.ContainsKey).ToDictionary(l => l, l => result[l]));
        ValueChanged?.Invoke(_value);
    }
}
