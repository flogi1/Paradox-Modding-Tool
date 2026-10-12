using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ParadoxModTool.Editor.Controls;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Editing;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Orientation = System.Windows.Controls.Orientation;
using RadioButton = System.Windows.Controls.RadioButton;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>Reiter "Formular": baut aus dem Paket die Felder und Ebenen und schreibt jede Eingabe sofort in den Arbeitsstand.</summary>
public partial class GeneratorWindow
{
    private const double LabelWidth = 150;

    /// <summary>Eingeklappte Gruppen und Elemente (Pfad wie <c>categories[2].ideas[1]</c>), damit ein Neuaufbau sie so lässt.</summary>
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);

    /// <summary>Geschwister und Stelle jedes Elements einer zählbaren Ebene (für "auf höhere Stufen kopieren" und "Löschen übertragen").</summary>
    private readonly Dictionary<FormNode, (List<FormNode> Siblings, int Index)> _levelOf = new(ReferenceEqualityComparer.Instance);

    /// <summary>Modifikatorfelder des aufgebauten Formulars, damit Änderungen an anderen Stufen sofort im Feld und in den Chips stehen.</summary>
    private readonly List<(FormNode Node, string FieldId, TextBox Box, Action RefreshChips)> _modifierBoxes = new();

    private void BuildForm()
    {
        FormPanel.Children.Clear();
        _levelOf.Clear();
        _modifierBoxes.Clear();
        if (_package is null || _state is null)
        {
            ShowMessage(_package is null ? GeneratorTexts.NoPackages(PackagesRoot) : GeneratorTexts.NoState);
            return;
        }

        FormPanel.Children.Add(Heading(GeneratorTexts.Head));
        foreach (var field in _package.Head)
        {
            FormPanel.Children.Add(FieldRow(field, _state.Head));
        }

        FormPanel.Children.Add(ModRow());
        FormPanel.Children.Add(TargetRow());
        foreach (var (id, external) in _package.Externals)
        {
            FormPanel.Children.Add(ExternalRow(id, external));
        }

        FormPanel.Children.Add(LanguageRows());
        if (ModifierSwitchRow() is { } switches)
        {
            FormPanel.Children.Add(switches);
        }

        if (_package.Root is { } root)
        {
            FormPanel.Children.Add(LevelPanel(root, _state.Head, root.Id));
        }
    }

    private static string FolderName(string path) => System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));

    /// <summary>Ziel "In Mod" mit dem Ordnernamen der Mod des Arbeitsstands, damit sichtbar ist, wohin geschrieben wird.</summary>
    private string ModTargetLabel() =>
        _state?.ModRoot is { } modRoot ? GeneratorTexts.TargetModNamed(FolderName(modRoot)) : GeneratorTexts.TargetNoMod;

    /// <summary>
    /// Die Mod des Arbeitsstands: Auswahl aus den Mods des Spiels (mit Suche) oder ein beliebiger Ordner. Sie gilt für diesen Stand, unabhängig davon,
    /// welche Datei gerade im Editor offen ist.
    /// </summary>
    private UIElement ModRow()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        row.Children.Add(new TextBlock { Text = GeneratorTexts.ModLabel, Width = LabelWidth, VerticalAlignment = VerticalAlignment.Center, ToolTip = GeneratorTexts.ModPickerTip });
        var mods = _host.KnownMods().ToList();
        if (_state!.ModRoot is { } current && !mods.Any(m => string.Equals(m.Path, current, StringComparison.OrdinalIgnoreCase)))
        {
            mods.Insert(0, (FolderName(current), current));
        }

        var choice = new SearchableChoice(searchWidth: 120, choiceWidth: 320) { ToolTip = GeneratorTexts.ModPickerTip };
        var items = new List<(string Text, object? Tag)> { (GeneratorTexts.NoModChosen, null) };
        items.AddRange(mods.Select(m => ($"{m.Name}  ({FolderName(m.Path)})", (object?)m.Path)));
        choice.SetItems(items, _state.ModRoot, pinned: 1);
        choice.SelectionChanged += (_, _) =>
        {
            var chosen = choice.SelectedTag as string;
            if (!string.Equals(chosen, _state.ModRoot, StringComparison.OrdinalIgnoreCase))
            {
                _state.ModRoot = chosen;
                MarkDirty();
                Dispatcher.BeginInvoke(BuildForm);
            }
        };
        row.Children.Add(choice);
        var browse = new Button { Content = new TextBlock { Text = GeneratorTexts.ChooseFolder }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFolderDialog { InitialDirectory = _state.ModRoot };
            if (dialog.ShowDialog(this) == true)
            {
                _state.ModRoot = dialog.FolderName;
                MarkDirty();
                BuildForm();
            }
        };
        row.Children.Add(browse);
        row.Children.Add(new TextBlock { Text = GeneratorTexts.ModHint, Foreground = Muted(), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private static TextBlock Heading(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };

    private UIElement TargetRow()
    {
        var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        row.Children.Add(new TextBlock { Text = GeneratorTexts.Target, Width = LabelWidth, VerticalAlignment = VerticalAlignment.Center });
        var folder = new TextBox { Width = 320, Text = _state!.OutputFolder ?? string.Empty, IsEnabled = _state.Target == "output", Margin = new Thickness(6, 0, 4, 0) };
        var choose = new Button { Content = new TextBlock { Text = GeneratorTexts.ChooseFolder }, Padding = new Thickness(8, 1, 8, 1), IsEnabled = folder.IsEnabled };
        foreach (var target in _package!.Targets)
        {
            var radio = new RadioButton
            {
                Content = new TextBlock { Text = target == "output" ? GeneratorTexts.TargetOutput : ModTargetLabel() },
                IsChecked = _state.Target == target,
                GroupName = "target",
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            radio.Checked += (_, _) =>
            {
                _state.Target = target;
                folder.IsEnabled = choose.IsEnabled = target == "output";
                MarkDirty();
            };
            row.Children.Add(radio);
        }

        if (_package.Targets.Contains("output"))
        {
            folder.TextChanged += (_, _) =>
            {
                _state.OutputFolder = folder.Text.Trim().Length == 0 ? null : folder.Text.Trim();
                MarkDirty();
            };
            choose.Click += (_, _) =>
            {
                var dialog = new OpenFolderDialog { InitialDirectory = _state.OutputFolder };
                if (dialog.ShowDialog(this) == true)
                {
                    folder.Text = dialog.FolderName;
                }
            };
            row.Children.Add(folder);
            row.Children.Add(choose);
        }

        return row;
    }

    /// <summary>
    /// Eine externe Datei des Pakets (Abschnitt <c>externals</c>): Beschriftung, gewählter Pfad und "Datei wählen". Der Pfad gehört zum Paket, nicht zum
    /// Arbeitsstand (<see cref="ExternalPathStore"/>); der Hinweis des Pakets steht als Tooltip.
    /// </summary>
    private UIElement ExternalRow(string id, ExternalDef external)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        var tip = external.Hint;
        row.Children.Add(new TextBlock { Text = external.Label, Width = LabelWidth, VerticalAlignment = VerticalAlignment.Center, ToolTip = tip });
        var path = new TextBox { Width = 320, IsReadOnly = true, Margin = new Thickness(6, 0, 4, 0), ToolTip = tip };
        void ShowPath()
        {
            var chosen = ExternalPathStore.Load(_package!.DataFolder).GetValueOrDefault(id);
            path.Text = chosen ?? GeneratorTexts.ExternalNone;
            path.ToolTip = string.IsNullOrEmpty(tip) ? chosen : tip + (chosen is null ? string.Empty : Environment.NewLine + chosen);
        }

        ShowPath();
        var choose = new Button { Content = new TextBlock { Text = GeneratorTexts.ExternalChoose }, Padding = new Thickness(8, 1, 8, 1), ToolTip = tip };
        choose.Click += (_, _) =>
        {
            var current = ExternalPathStore.Load(_package!.DataFolder).GetValueOrDefault(id);
            var dialog = new OpenFileDialog
            {
                Title = GeneratorTexts.ExternalDialogTitle(external.Label),
                Filter = "*.json|*.json|*.*|*.*",
                InitialDirectory = current is null ? null : System.IO.Path.GetDirectoryName(current),
                FileName = current is null ? string.Empty : System.IO.Path.GetFileName(current),
            };
            if (dialog.ShowDialog(this) == true)
            {
                try
                {
                    ExternalPathStore.Set(_package.DataFolder, id, dialog.FileName);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    MessageBox.Show(this, GeneratorTexts.ExternalSaveFailed(ex.Message), GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ShowPath();
                // Ein Plan ohne diese Datei ist veraltet
                ClearPlan();
            }
        };
        row.Children.Add(path);
        row.Children.Add(choose);
        return row;
    }

    private UIElement LanguageRows()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        var languages = new WrapPanel();
        languages.Children.Add(new TextBlock { Text = GeneratorTexts.Languages, Width = LabelWidth, VerticalAlignment = VerticalAlignment.Center });
        var all = _host.ProfileLanguages().Count > 0 ? _host.ProfileLanguages() : new[] { LanguageFill.English };
        foreach (var language in all)
        {
            var box = new CheckBox
            {
                Content = new TextBlock { Text = language },
                IsChecked = _state!.Languages.Contains(language),
                IsEnabled = language != LanguageFill.English,
                Margin = new Thickness(0, 0, 10, 0),
            };
            box.Click += (_, _) =>
            {
                _state.Languages = all.Where(l => l == LanguageFill.English || (l == language ? box.IsChecked == true : _state.Languages.Contains(l))).ToList();
                MarkDirty();
                BuildForm();
            };
            languages.Children.Add(box);
        }

        panel.Children.Add(languages);
        var rules = new WrapPanel { Margin = new Thickness(LabelWidth, 4, 0, 0) };
        // Eine Regel, die eine weitere schon abdeckt, ist ausgegraut: "alle Sprachen" schließt "leere Sprachen" ein, beide machen "fehlende" wirkungslos.
        var current = _state!.LanguageRules;
        rules.Children.Add(RuleBox(GeneratorTexts.RuleExtend, GeneratorTexts.RuleExtendTip, true, r => r.ExtendEnglish, (r, v) => r with { ExtendEnglish = v }));
        rules.Children.Add(RuleBox(GeneratorTexts.RuleCopy, GeneratorTexts.RuleCopyTip, !current.ExtendEnglish, r => r.CopyEnglish, (r, v) => r with { CopyEnglish = v }));
        rules.Children.Add(RuleBox(GeneratorTexts.RuleMissing, GeneratorTexts.RuleMissingTip, !current.ExtendEnglish && !current.CopyEnglish, r => r.EnglishForMissing, (r, v) => r with { EnglishForMissing = v }));
        panel.Children.Add(rules);
        return panel;
    }

    private CheckBox RuleBox(string text, string tip, bool enabled, Func<LanguageDefaults, bool> get, Func<LanguageDefaults, bool, LanguageDefaults> set)
    {
        var box = new CheckBox { Content = new TextBlock { Text = text }, IsChecked = get(_state!.LanguageRules), Margin = new Thickness(0, 0, 14, 0), ToolTip = tip, IsEnabled = enabled };
        ToolTipService.SetShowOnDisabled(box, true);
        box.Click += (_, _) =>
        {
            _state.LanguageRules = set(_state.LanguageRules, box.IsChecked == true);
            MarkDirty();
            BuildForm();
        };
        return box;
    }

    /// <summary>Eine Ebene unter <paramref name="parent"/>: feste Gruppen und Elemente mit Kind-Ebenen als Expander, die letzte Ebene als kompakte Zeilen.</summary>
    private UIElement LevelPanel(LevelDef level, FormNode parent, string path)
    {
        if (!parent.Children.TryGetValue(level.Id, out var nodes))
        {
            nodes = new List<FormNode>();
            parent.Children[level.Id] = nodes;
        }

        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        if (level.ConstantsTable is null)
        {
            panel.Children.Add(CountRow(level, nodes));
        }

        var rows = level.ConstantsTable is { } table && _package!.Constants.TryGetValue(table, out var r) ? r : null;
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            _levelOf[node] = (nodes, i);
            var itemPath = $"{path}[{i + 1}]";
            if (level.Child is null)
            {
                panel.Children.Add(LeafRow(level, node, i));
                continue;
            }

            var content = new StackPanel { Margin = new Thickness(18, 2, 0, 6) };
            foreach (var field in level.Fields)
            {
                content.Children.Add(FieldRow(field, node));
            }

            content.Children.Add(LevelPanel(level.Child, node, $"{itemPath}.{level.Child.Id}"));
            var header = rows is not null && i < rows.Count
                ? rows[i].GetValueOrDefault("label") ?? rows[i].GetValueOrDefault("key") ?? (i + 1).ToString()
                : $"{level.Label} {i + 1}: {node.Values.GetValueOrDefault(level.Fields.FirstOrDefault()?.Id ?? string.Empty)}";
            var expander = new Expander { Header = new TextBlock { Text = header, FontWeight = FontWeights.SemiBold }, Content = content, IsExpanded = !_collapsed.Contains(itemPath), Margin = new Thickness(0, 2, 0, 2) };
            expander.Expanded += (_, _) => _collapsed.Remove(itemPath);
            expander.Collapsed += (_, _) => _collapsed.Add(itemPath);
            panel.Children.Add(expander);
        }

        return panel;
    }

    private UIElement CountRow(LevelDef level, List<FormNode> nodes)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new TextBlock { Text = $"{level.Label} – {GeneratorTexts.Count}", Width = LabelWidth, VerticalAlignment = VerticalAlignment.Center });
        var box = new TextBox { Width = 44, Text = nodes.Count.ToString() };
        void Apply()
        {
            if (!int.TryParse(box.Text.Trim(), out var wanted))
            {
                box.Text = nodes.Count.ToString();
                return;
            }

            wanted = Math.Clamp(wanted, level.Min, level.Max);
            if (wanted == nodes.Count)
            {
                box.Text = wanted.ToString();
                return;
            }

            while (nodes.Count < wanted)
            {
                nodes.Add(FormNode.Create(level.Fields, level.Child, _package!.Constants));
            }

            nodes.RemoveRange(wanted, nodes.Count - wanted);
            MarkDirty();
            Dispatcher.BeginInvoke(BuildForm);
        }

        box.LostFocus += (_, _) => Apply();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Apply();
            }
        };
        row.Children.Add(box);
        row.Children.Add(new TextBlock { Text = $"({level.Min}–{level.Max})", Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Muted() });
        return row;
    }

    /// <summary>Ein Element der letzten Ebene (bei IV2 eine Stufe) als eine Zeile mit allen Feldern.</summary>
    private UIElement LeafRow(LevelDef level, FormNode node, int index)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(LabelWidth, 2, 0, 2) };
        row.Children.Add(new TextBlock { Text = $"{level.Label} {index + 1}", Width = 70, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0) });
        foreach (var field in level.Fields)
        {
            row.Children.Add(FieldControl(field, node, inline: true));
        }

        return row;
    }

    private UIElement FieldRow(FieldDef field, FormNode node)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        if (field.Type != FieldType.Bool)
        {
            row.Children.Add(new TextBlock { Text = field.Label, Width = LabelWidth, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0) });
        }

        row.Children.Add(FieldControl(field, node, inline: false));
        return row;
    }

    private FrameworkElement FieldControl(FieldDef field, FormNode node, bool inline)
    {
        var value = node.Values.GetValueOrDefault(field.Id) ?? field.Default ?? string.Empty;
        void Set(string text)
        {
            node.Values[field.Id] = text;
            MarkDirty();
        }

        FrameworkElement control;
        switch (field.Type)
        {
            case FieldType.Bool:
            {
                var box = new CheckBox { Content = new TextBlock { Text = field.Label }, IsChecked = value is "yes" or "true", Margin = new Thickness(inline ? 8 : 0, 3, 0, 0), VerticalAlignment = VerticalAlignment.Top };
                box.Click += (_, _) => Set(box.IsChecked == true ? "yes" : "no");
                return box;
            }

            case FieldType.Choice:
            {
                var combo = new ComboBox { Width = 200, ItemsSource = field.Choices ?? Array.Empty<string>(), SelectedItem = value };
                combo.SelectionChanged += (_, _) => Set(combo.SelectedItem as string ?? string.Empty);
                return combo;
            }

            case FieldType.Image:
                return ImageChoice(field, value, Set);

            default:
            {
                var multiline = field.Type is FieldType.MultilineText or FieldType.Modifiers;
                var box = new TextBox
                {
                    Text = value,
                    Width = field.Type == FieldType.Number ? 80 : multiline ? (inline ? 420 : 460) : 300,
                    AcceptsReturn = multiline,
                    TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    MinHeight = multiline ? 38 : 0,
                    VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                    MaxHeight = 160,
                };
                box.TextChanged += (_, _) => Set(box.Text);
                if (field.Type == FieldType.Modifiers)
                {
                    box.FontFamily = new FontFamily("Consolas");
                    return ModifierField(field, node, box);
                }

                control = box;
                break;
            }
        }

        if (!field.Multilingual)
        {
            return field.Lookup is null ? control : WithLookupButton(control, field, node);
        }

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(control);
        if (field.Lookup is not null)
        {
            panel.Children.Add(LookupButton(field, node));
        }

        var button = new Button { Content = new TextBlock { Text = GeneratorTexts.Translations }, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        var warning = new TextBlock { Text = "⚠", Margin = new Thickness(6, 2, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        warning.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
        void UpdateWarning()
        {
            var missing = MissingTranslations(field, node);
            warning.Visibility = missing.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            warning.ToolTip = GeneratorTexts.MissingTranslation(string.Join(", ", missing));
        }

        button.Click += (_, _) =>
        {
            var current = node.Translations.GetValueOrDefault(field.Id) ?? new Dictionary<string, string>();
            var languages = _state!.Languages.Where(l => l != LanguageFill.English).ToList();
            if (TranslationsDialog.Ask(this, GeneratorTexts.TranslationsTitle(field.Label), languages, current, field.Type is FieldType.MultilineText) is { } edited)
            {
                node.Translations[field.Id] = edited;
                MarkDirty();
                UpdateWarning();
            }
        };
        UpdateWarning();
        panel.Children.Add(button);
        panel.Children.Add(warning);
        return panel;
    }

    /// <summary>Gewählte Sprachen, für die das Feld weder eine Übersetzung hat noch eine Regel Englisch einsetzt.</summary>
    private IReadOnlyList<string> MissingTranslations(FieldDef field, FormNode node)
    {
        var english = node.Values.GetValueOrDefault(field.Id) ?? string.Empty;
        var translations = node.Translations.GetValueOrDefault(field.Id);
        return english.Trim().Length == 0
            ? Array.Empty<string>()
            : _state!.Languages.Where(l => l != LanguageFill.English && LanguageFill.Resolve(english, translations, l, _state.LanguageRules).Length == 0).ToList();
    }

    private FrameworkElement ImageChoice(FieldDef field, string value, Action<string> set)
    {
        if (field.Asset is { } assetId && _package!.Assets.ContainsKey(assetId))
        {
            var library = _package.AssetPath(assetId);
            if (!File.Exists(library) && !Directory.Exists(library))
            {
                return AssetMissing(assetId);
            }
        }

        var choice = new SearchableChoice(searchWidth: 120, choiceWidth: 300);
        try
        {
            var library = field.Asset is { } asset && _package!.Assets.ContainsKey(asset) ? AssetLibrary.Open(_package.AssetPath(asset)) : null;
            var items = new List<(string Text, object? Tag)> { (GeneratorTexts.ImageNone, string.Empty) };
            items.AddRange((library?.Entries ?? Array.Empty<string>()).Select(e => (e, (object?)e)));
            choice.SetItems(items, value, pinned: 1);
            var preview = new ContentControl { Width = 72, Height = 48, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            void ShowPreview() => preview.Content = Preview(library, choice.SelectedTag as string);
            choice.SelectionChanged += (_, _) =>
            {
                set(choice.SelectedTag as string ?? string.Empty);
                ShowPreview();
            };
            ShowPreview();
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(choice);
            row.Children.Add(preview);
            if (library is not null)
            {
                var grid = new Button { Content = new TextBlock { Text = GeneratorTexts.ImageGridButton }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                grid.Click += (_, _) =>
                {
                    var key = PackageKey.Compose(_package!, _state!.Head.Values);
                    var window = new ImageGridWindow(this, library, choice.SelectedTag as string, ImageSuggest.ForKey(library.Entries, key), _host.Session());
                    if (window.ShowDialog() == true && window.Picked is { } picked)
                    {
                        choice.SetItems(items, picked, pinned: 1);
                        set(picked);
                        ShowPreview();
                    }
                };
                row.Children.Add(grid);
            }

            return row;
        }
        catch (GeneratorOutputException ex)
        {
            return new TextBlock { Text = GeneratorTexts.ImageLibraryError(ex.Message), Foreground = Muted() };
        }
    }

    /// <summary>Vorschau eines Bild-Eintrags (DDS: DXT1/DXT5, sonst was WPF selbst lesen kann); ohne Auswahl leer, sonst Hinweis "keine Vorschau".</summary>
    private object? Preview(AssetLibrary? library, string? entry)
    {
        if (library is null || string.IsNullOrEmpty(entry))
        {
            return null;
        }

        try
        {
            if (ImageDecode.FromBytes(library.Read(entry), entry) is { } source)
            {
                return new System.Windows.Controls.Image { Source = source, Stretch = Stretch.Uniform, ToolTip = entry };
            }
        }
        catch (Exception ex) when (ex is GeneratorOutputException or IOException or NotSupportedException or System.IO.InvalidDataException)
        {
            // Unlesbarer Eintrag: Hinweis statt Bild.
        }

        return new TextBlock { Text = GeneratorTexts.NoPreview, Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    }

    /// <summary>Hinweis und Knopf "Asset wählen" für eine noch nicht bereitgestellte Asset-Bibliothek (z. B. die Wappen-Zip).</summary>
    private FrameworkElement AssetMissing(string asset)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock { Text = GeneratorTexts.AssetMissing(Path.GetFileName(_package!.Assets[asset])), Foreground = Muted(), VerticalAlignment = VerticalAlignment.Center });
        var pick = new Button { Content = new TextBlock { Text = GeneratorTexts.AssetPick }, ToolTip = GeneratorTexts.AssetPickTip, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) };
        pick.Click += (_, _) => PickAsset(asset);
        panel.Children.Add(pick);
        return panel;
    }

    private void PickAsset(string asset)
    {
        var target = _package!.AssetPath(asset);
        // Filter nach der Endung des Assets: Wappen-Zip oder JSON-Datenbank.
        var filter = target.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? GeneratorTexts.AssetFilterJson : GeneratorTexts.AssetFilter;
        var dialog = new OpenFileDialog { Filter = filter, Title = GeneratorTexts.AssetPick };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(dialog.FileName, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BuildForm();
    }

    private Brush Muted() => (Brush)FindResource("Theme.TextMuted");
}
