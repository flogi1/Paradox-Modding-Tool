using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Leiste unter der Eigenschaftenliste (Plan-Ruling 3, 4, 5): Geltungsbereich "Diese Stelle", Eingabefeld passend zur Eigenschaft,
/// Übernehmen, Zurücksetzen und "Eigenschaft hinzufügen". Schreibt nie selbst, sondern meldet <see cref="GuiDesignerAction"/>s.
/// </summary>
public sealed class GuiPropertyEditor : UserControl
{
    private static readonly GuiPropertyCatalog EmptyCatalog = new();
    private readonly TextBlock _scope = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 4, 4, 2) };
    private readonly TextBlock _name = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) };
    private readonly ContentControl _input = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _apply = new() { Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(4, 0, 0, 0) };
    private readonly Button _reset = new() { Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(4, 0, 4, 0) };
    private readonly ComboBox _addName = new() { IsEditable = true, Width = 180, Margin = new Thickness(4, 0, 4, 0) };
    private readonly Button _add = new() { Padding = new Thickness(6, 1, 6, 1) };
    private readonly Button _adapt = new() { Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(4, 0, 4, 4), HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
    private readonly Button _newType = new() { Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(4, 0, 0, 0) };
    private readonly Button _typeEverywhere = new() { Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(4, 0, 4, 0) };
    private readonly StackPanel _scopeButtons = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0), Visibility = Visibility.Collapsed };
    private bool _isDraft;
    private bool _canDerive;
    private bool _isTypeInstance;
    private Action? _adaptAction;
    private GuiNode? _selected;
    private ResolvedElement? _element;
    private ResolvedProperty? _property;
    private string? _propertyName;
    private Func<string?>? _read;
    private bool _editable;

    public GuiPropertyEditor()
    {
        _scope.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _scope.ToolTip = GuiTexts.ScopeTip;
        _apply.Content = GuiTexts.PropertyApply;
        _apply.ToolTip = GuiTexts.PropertyApplyTip;
        _apply.Click += (_, _) => Apply();
        _reset.Content = GuiTexts.PropertyReset;
        _reset.ToolTip = GuiTexts.PropertyResetTip;
        _reset.Click += (_, _) => Reset();
        _add.Content = GuiTexts.PropertyAdd;
        _add.ToolTip = GuiTexts.PropertyAddTip;
        _addName.ToolTip = GuiTexts.PropertyAddTip;
        _add.Click += (_, _) => AddProperty();
        _adapt.Content = GuiTexts.InnerAdapt;
        _adapt.ToolTip = GuiTexts.InnerAdaptTip;
        _adapt.Click += (_, _) => _adaptAction?.Invoke();
        _newType.Content = GuiTexts.ScopeNewType;
        _newType.ToolTip = GuiTexts.ScopeNewTypeTip;
        _newType.Click += (_, _) =>
        {
            if (_selected is { } node)
            {
                NewTypeRequested?.Invoke(this, node);
            }
        };
        _typeEverywhere.Content = GuiTexts.ScopeTypeEverywhere;
        _typeEverywhere.ToolTip = GuiTexts.ScopeTypeEverywhereTip;
        _typeEverywhere.Click += (_, _) =>
        {
            if (_selected is { } node)
            {
                TypeEverywhereRequested?.Invoke(this, node);
            }
        };
        _scopeButtons.Children.Add(_newType);
        _scopeButtons.Children.Add(_typeEverywhere);
        _input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Apply();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ShowFor(_propertyName, _property);
                e.Handled = true;
            }
        };

        var editRow = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_input, 1);
        Grid.SetColumn(_apply, 2);
        Grid.SetColumn(_reset, 3);
        editRow.Children.Add(_name);
        editRow.Children.Add(_input);
        editRow.Children.Add(_apply);
        editRow.Children.Add(_reset);

        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2, 4, 4) };
        addRow.Children.Add(new TextBlock { Text = GuiTexts.PropertyAddLabel, VerticalAlignment = VerticalAlignment.Center });
        addRow.Children.Add(_addName);
        addRow.Children.Add(_add);

        var panel = new StackPanel();
        var scopeRow = new DockPanel();
        DockPanel.SetDock(_scopeButtons, Dock.Right);
        scopeRow.Children.Add(_scopeButtons);
        scopeRow.Children.Add(_scope);
        panel.Children.Add(scopeRow);
        panel.Children.Add(_adapt);
        panel.Children.Add(editRow);
        panel.Children.Add(addRow);
        var border = new Border { Child = panel, BorderThickness = new Thickness(0, 1, 0, 0) };
        border.SetResourceReference(Border.BorderBrushProperty, "Theme.Separator");
        Content = border;
        ShowFor(null, null);
    }

    public event EventHandler<GuiDesignerAction>? ActionRequested;

    public event EventHandler<GuiNode>? ShowInCodeRequested;

    /// <summary>Hinweis für die Statuszeile (z.B. ungültiger Wert).</summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>"Neuer Typ" für das gewählte Widget (Plan 3c Ruling 1).</summary>
    public event EventHandler<GuiNode>? NewTypeRequested;

    /// <summary>"Typ überall": Definition des Typs der gewählten Instanz öffnen.</summary>
    public event EventHandler<GuiNode>? TypeEverywhereRequested;

    private static GuiPropertyCatalog Catalog => GuiSettings.Workspace?.Catalog ?? EmptyCatalog;

    /// <summary>
    /// Vanilla-Entwurf: Ein Widget auf oberster Ebene lässt sich nur mit der ganzen Datei übernehmen (Plan-Ruling 6). Ändert sich das, wird der
    /// Hinweis zum Geltungsbereich neu geschrieben (3b-Nacharbeit: vorher blieb er bis zur nächsten Auswahl veraltet).
    /// </summary>
    public bool IsDraft
    {
        get => _isDraft;
        set
        {
            if (_isDraft != value)
            {
                _isDraft = value;
                UpdateScope();
            }
        }
    }

    /// <summary>Datei des angezeigten Texts: Der Hinweis zählt Verwendungen darin live (Task 2, <see cref="GuiEditScope.Of"/>).</summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Neues gewähltes Element: Geltungsbereich und "Eigenschaft hinzufügen" neu, Eingabe leer. <paramref name="canDerive"/>: ein Widget, von dem
    /// "Neuer Typ" ableiten kann; <paramref name="isTypeInstance"/>: "Typ überall" ist möglich.
    /// </summary>
    public void SetElement(GuiNode? selected, ResolvedElement? element, bool editable, bool canDerive = false, bool isTypeInstance = false)
    {
        _selected = selected;
        _element = element;
        _editable = editable;
        _canDerive = canDerive;
        _isTypeInstance = isTypeInstance;
        _adaptAction = null;
        _adapt.Visibility = Visibility.Collapsed;
        if (selected is null || element is null)
        {
            _scope.Text = string.Empty;
            _addName.ItemsSource = null;
        }
        else
        {
            _addName.ItemsSource = GuiPropertyInput.AddableProperties(element, Catalog);
        }

        UpdateScope();
        ShowFor(null, null);
    }

    /// <summary>Hinweis und Knöpfe des Geltungsbereichs für das gewählte Element; ohne Element (oder bei einem geerbten) bleiben die Knöpfe weg.</summary>
    private void UpdateScope()
    {
        var shown = _selected is not null && _element is not null;
        _scopeButtons.Visibility = shown && (_canDerive || _isTypeInstance) ? Visibility.Visible : Visibility.Collapsed;
        if (!shown)
        {
            return;
        }

        var scope = GuiEditScope.Of(_selected!, GuiSettings.Workspace?.Index ?? GuiIndex.Empty, FilePath);
        _scope.Text = scope.Label is { } label ? GuiTexts.ScopeHere(label, scope.Usages)
            : _isDraft ? GuiTexts.ScopeCopiesFile
            : GuiTexts.ScopeHereOnly;
        _newType.IsEnabled = _editable && _canDerive;
        _typeEverywhere.IsEnabled = _isTypeInstance;
    }

    /// <summary>
    /// Element aus dem Inneren eines Typs oder Templates (Klick in der Vorschau; Plan-Ruling 14): nur lesen, Herkunft zeigen und, wenn es in
    /// einem block liegt, "Über blockoverride anpassen" anbieten.
    /// </summary>
    public void SetInherited(GuiNode instance, ResolvedElement inner, bool editable)
    {
        _selected = null;
        _element = null;
        _editable = editable;
        _addName.ItemsSource = null;
        UpdateScope();
        var source = GuiBlockOverrides.ContainingBlock(inner);
        _scope.Text = source is null
            ? GuiTexts.InnerNoBlock(GuiBlockOverrides.DefinitionLabelOf(inner.Origin.Node))
            : GuiTexts.InnerFrom(source.DefinitionLabel, source.BlockName);
        _adaptAction = source is null
            ? null
            : () => ActionRequested?.Invoke(this, new GuiDesignerAction(GuiDesignerActionKind.OverrideBlock, instance)
            {
                Name = source.BlockName,
                ContentFrom = source.Owner,
                FocusKey = inner.Key,
            });
        _adapt.Visibility = source is null ? Visibility.Collapsed : Visibility.Visible;
        ShowFor(null, null);
    }

    /// <summary>Zeile in der Liste gewählt: Eingabe für diese Eigenschaft mit ihrem Wert.</summary>
    public void SetProperty(ResolvedProperty? property) => ShowFor(property?.Name, property);

    public void SetEditable(bool editable)
    {
        _editable = editable;
        UpdateScope();
        ShowFor(_propertyName, _property);
    }

    private void ShowFor(string? name, ResolvedProperty? property)
    {
        _propertyName = name;
        _property = property;
        _addName.IsEnabled = _add.IsEnabled = _editable && _element is not null;
        _adapt.IsEnabled = _editable && _adaptAction is not null;
        if (name is null || _element is null || _selected is null)
        {
            _name.Text = GuiTexts.PropertyNone;
            _input.Content = null;
            _read = null;
            _apply.IsEnabled = _reset.IsEnabled = false;
            return;
        }

        _name.Text = name;
        var editor = GuiPropertyInput.EditorFor(name, _element.BaseWidget, Catalog, property);
        _input.Content = CreateInput(editor, name, GuiPropertyInput.InputOf(property), property?.IsQuoted == true);
        _apply.IsEnabled = _editable && _read is not null;
        _reset.IsEnabled = _editable && property is not null && GuiPropertyInput.TargetFor(property, _selected).InPlace;
    }

    /// <summary>Eingabefeld je Art; setzt <see cref="_read"/> (Eingabe als Code, null bei ungültiger Eingabe).</summary>
    private FrameworkElement CreateInput(GuiValueEditor editor, string name, string input, bool wasQuoted)
    {
        switch (editor)
        {
            case GuiValueEditor.Block:
                _read = null;
                var show = new Button { Content = GuiTexts.TreeShowInCode, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0) };
                show.Click += (_, _) =>
                {
                    if (_property is { } property && _selected is { } selected)
                    {
                        ShowInCodeRequested?.Invoke(this, GuiPropertyInput.TargetFor(property, selected).Node);
                    }
                };
                return Row(Muted(GuiTexts.PropertyBlockReadOnly), show);

            case GuiValueEditor.Enum or GuiValueEditor.Bool:
                var choice = new ComboBox
                {
                    IsEditable = true,
                    MinWidth = 140,
                    ItemsSource = editor == GuiValueEditor.Bool ? new[] { "yes", "no" } : GuiPropertyInput.EnumOptions(name, _element!.BaseWidget, Catalog),
                    Text = input,
                    IsEnabled = _editable,
                };
                _read = () => GuiPropertyInput.ValueText(editor, choice.Text, wasQuoted);
                return WithDataHelper(choice, () => EditableField(choice), name, input, always: false);

            case GuiValueEditor.Binding:
                var binding = new ComboBox
                {
                    IsEditable = true,
                    MinWidth = 220,
                    ItemsSource = Catalog.SuggestBindings(GuiPropertyInput.DataContextOf(_selected!)),
                    Text = input,
                    IsEnabled = _editable,
                };
                _read = () => GuiPropertyInput.ValueText(editor, binding.Text, wasQuoted);
                return WithDataHelper(binding, () => EditableField(binding), name, input, always: true);

            case GuiValueEditor.Pair:
                var (first, second) = GuiPropertyInput.SplitPair(input);
                var a = new TextBox { Text = first, Width = 64, IsEnabled = _editable };
                var b = new TextBox { Text = second, Width = 64, Margin = new Thickness(6, 0, 0, 0), IsEnabled = _editable };
                _read = () => GuiPropertyInput.ValueText(editor, a.Text + " " + b.Text, wasQuoted);
                return Row(a, PercentToggle(a), b, PercentToggle(b));

            case GuiValueEditor.Color:
                var text = new TextBox { Text = input, MinWidth = 150, IsEnabled = _editable };
                var swatch = new Border { Width = 18, Height = 18, Margin = new Thickness(0, 0, 6, 0), BorderThickness = new Thickness(1) };
                swatch.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
                void UpdateSwatch() => swatch.Background = GuiPropertyInput.TryParseColor(text.Text, out var shown)
                    ? new SolidColorBrush(ToMedia(shown))
                    : Brushes.Transparent;
                text.TextChanged += (_, _) => UpdateSwatch();
                UpdateSwatch();
                var colorRow = Row(swatch, text);
                if (GuiSettings.ColorPicker is { } picker)
                {
                    var pick = new Button { Content = GuiTexts.PropertyPickColor, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0), IsEnabled = _editable };
                    pick.Click += (_, _) =>
                    {
                        var current = GuiPropertyInput.TryParseColor(text.Text, out var parsed) ? parsed : new GuiColor(1, 1, 1, 1);
                        if (picker(ToMedia(current)) is { } chosen)
                        {
                            text.Text = GuiPropertyInput.FormatColor(GuiPropertyInput.FromBytes(chosen.R, chosen.G, chosen.B, current.A));
                        }
                    };
                    colorRow.Children.Add(pick);
                }

                _read = () => GuiPropertyInput.ValueText(editor, text.Text, wasQuoted);
                return colorRow;

            case GuiValueEditor.Texture:
                var path = new TextBox { Text = input, MinWidth = 240, IsEnabled = _editable };
                var size = Muted(string.Empty);
                size.Margin = new Thickness(6, 0, 0, 0);
                void UpdateSize() => size.Text = GuiSettings.Workspace?.Textures.SizeOf(path.Text.Trim().Trim('"')) is { } found
                    ? GuiTexts.PropertyTextureSize(found.Width, found.Height)
                    : string.Empty;
                path.LostFocus += (_, _) => UpdateSize();
                UpdateSize();
                var browse = new Button { Content = GuiTexts.PropertyBrowse, ToolTip = GuiTexts.PropertyBrowseTip, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(6, 0, 0, 0), IsEnabled = _editable };
                browse.Click += (_, _) => BrowseTexture(path, UpdateSize);
                _read = () => GuiPropertyInput.ValueText(editor, path.Text, wasQuoted);
                return Row(path, browse, size);

            default:
                var box = new TextBox { Text = input, MinWidth = 220, IsEnabled = _editable };
                _read = () => GuiPropertyInput.ValueText(editor, box.Text, wasQuoted);
                return editor == GuiValueEditor.Text ? WithDataHelper(box, () => box, name, input, always: false) : box;
        }
    }

    private static TextBox? EditableField(ComboBox box) => box.Template?.FindName("PART_EditableTextBox", box) as TextBox;

    /// <summary>
    /// Knopf "Daten-Helfer" neben dem Feld, wenn das Hauptfenster den Helfer anbietet und das Feld einen Datenausdruck aufnehmen kann (immer bei
    /// Ausdrucksfeldern, sonst nach <see cref="GuiPropertyInput.AcceptsDataExpression"/>, z. B. visible = yes). Der Ausdruck ersetzt eine Markierung,
    /// sonst kommt er an die Cursorstelle; ein Ja/Nein-Wert wie yes wird dabei ganz ersetzt.
    /// </summary>
    private FrameworkElement WithDataHelper(FrameworkElement input, Func<TextBox?> field, string name, string value, bool always)
    {
        if (ConditionTarget.IsConditionProperty(name) && GuiSettings.ConditionHelper is { } conditionHelper && _editable && _selected is not null)
        {
            return WithConditionHelper(input, field, value, conditionHelper);
        }

        if (GuiSettings.DataHelper is not { } helper || !_editable || _selected is null || _element is null
            || (!always && !GuiPropertyInput.AcceptsDataExpression(name, _element.BaseWidget, Catalog, value)))
        {
            return input;
        }

        // Unter dem Feld statt daneben: die Zeile bricht nicht um, in einer schmalen Eigenschaftsspalte wäre der Knopf sonst abgeschnitten.
        var open = new Button
        {
            Content = GuiDataTexts.OpenHelper,
            ToolTip = GuiDataTexts.OpenHelperTip,
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        open.Click += (_, _) =>
        {
            if (_selected is not { } element || helper(element) is not { } expression || field() is not { } text)
            {
                return;
            }

            InsertAtCaret(text, expression);
            input.Focus();
        };
        return Stacked(input, open);
    }

    /// <summary>Ersetzt eine Markierung, sonst Einfügen an der Cursorstelle; ein Wert ohne Datenausdruck (yes) wird ganz ersetzt.</summary>
    private static void InsertAtCaret(TextBox text, string expression)
    {
        if (text.SelectionLength == 0 && !text.Text.Contains('['))
        {
            text.SelectAll();
        }

        text.SelectedText = expression;
        text.CaretIndex = text.SelectionStart + expression.Length;
        text.SelectionLength = 0;
    }

    /// <summary>
    /// Bedingungs-Eigenschaften (visible, enabled, down, checked): Knopf immer, auch bei einem Ja/Nein-Wert; der Bedingungs-Helfer bekommt den Feldwert. Ein
    /// Ergebnis aus dem Tab "Bedingung" ersetzt den ganzen Wert, eines aus "Kette"/"Nachschlagen" kommt wie beim Daten-Helfer an die Cursorstelle.
    /// </summary>
    private FrameworkElement WithConditionHelper(FrameworkElement input, Func<TextBox?> field, string value, Func<GuiNode, string, GuiHelperResult?> helper)
    {
        var open = new Button
        {
            Content = GuiDataTexts.OpenHelper,
            ToolTip = GuiDataTexts.OpenHelperTip,
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        open.Click += (_, _) =>
        {
            var text = field();
            var current = text?.Text ?? (input as ComboBox)?.Text ?? (input as TextBox)?.Text ?? value;
            if (_selected is not { } element || helper(element, current) is not { } result)
            {
                return;
            }

            var expression = result.Expression;
            text ??= input as TextBox;
            if (result.IsCondition)
            {
                if (text is not null)
                {
                    text.Text = expression;
                    text.CaretIndex = expression.Length;
                }
                else if (input is ComboBox combo)
                {
                    combo.Text = expression;
                }
            }
            else if (text is not null)
            {
                InsertAtCaret(text, expression);
            }
            else if (input is ComboBox combo)
            {
                combo.Text = combo.Text.Contains('[') ? combo.Text + expression : expression;
            }

            input.Focus();
        };
        return Stacked(input, open);
    }

    private static StackPanel Stacked(FrameworkElement input, Button open)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(input);
        stack.Children.Add(open);
        return stack;
    }

    /// <summary>Plan-Ruling 4: Wert aus einer Zeile dieser Datei wird dort geändert, ein geerbter bekommt eine neue Zeile am Element.</summary>
    private void Apply()
    {
        if (!_editable || _read is null || _propertyName is null || _selected is null)
        {
            return;
        }

        if (_read() is not { } value)
        {
            StatusChanged?.Invoke(this, GuiTexts.PropertyInvalid);
            return;
        }

        var target = _property is null ? new GuiPropertyTarget(_selected, false) : GuiPropertyInput.TargetFor(_property, _selected);
        ActionRequested?.Invoke(this, target.InPlace
            ? new GuiDesignerAction(GuiDesignerActionKind.SetValue, target.Node) { Value = value }
            : new GuiDesignerAction(GuiDesignerActionKind.SetProperty, _selected) { Name = _propertyName, Value = value });
    }

    private void Reset()
    {
        if (_editable && _property is not null && _selected is not null && GuiPropertyInput.TargetFor(_property, _selected) is { InPlace: true } target)
        {
            ActionRequested?.Invoke(this, new GuiDesignerAction(GuiDesignerActionKind.RemoveProperty, target.Node));
        }
    }

    private void AddProperty()
    {
        var name = _addName.Text.Trim();
        if (name.Length == 0 || !name.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            StatusChanged?.Invoke(this, GuiTexts.PropertyInvalidName);
            return;
        }

        ShowFor(name, _element?.Properties.LastOrDefault(p => p.Name == name && p.Role != GuiRole.StructuredBlock));
        (_input.Content as FrameworkElement)?.Focus();
    }

    private void BrowseTexture(TextBox path, Action updateSize)
    {
        var roots = GuiSettings.Workspace?.Textures.Roots ?? Array.Empty<string>();
        var dialog = new OpenFileDialog { Filter = GuiTexts.PropertyTextureFilter, CheckFileExists = true };
        if (roots.Select(r => Path.Combine(r, "gfx")).FirstOrDefault(Directory.Exists) is { } start)
        {
            dialog.InitialDirectory = start;
        }

        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("gui.texture");
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        if (GuiPropertyInput.TexturePathFor(dialog.FileName, roots) is { } relative)
        {
            path.Text = relative;
            updateSize();
        }
        else
        {
            StatusChanged?.Invoke(this, GuiTexts.PropertyTextureOutside);
        }
    }

    private ToggleButton PercentToggle(TextBox box)
    {
        var toggle = new ToggleButton
        {
            Content = "%",
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(2, 0, 0, 0),
            ToolTip = GuiTexts.PropertyPercentTip,
            IsChecked = box.Text.EndsWith('%'),
            IsEnabled = _editable,
        };
        toggle.Click += (_, _) => box.Text = GuiPropertyInput.TogglePercent(box.Text.Trim());
        return toggle;
    }

    private static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children)
        {
            row.Children.Add(child);
        }

        return row;
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }

    private static Color ToMedia(GuiColor color)
    {
        var (r, g, b) = GuiPropertyInput.ToBytes(color);
        return Color.FromArgb((byte)Math.Clamp(Math.Round(color.A * 255), 0, 255), r, g, b);
    }
}
