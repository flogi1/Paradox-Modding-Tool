using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Localization;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using FontFamily = System.Windows.Media.FontFamily;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Orientation = System.Windows.Controls.Orientation;
using RadioButton = System.Windows.Controls.RadioButton;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>Masken des Paket-Editors (Spec Teil 5a, Abschnitt 3): jede Eingabe schreibt sofort ins <see cref="PackageDocument"/>.</summary>
public sealed partial class PackageEditorWindow
{
    private const double LabelWidth = 160;

    /// <summary>Gewähltes Feld je Feldliste (Kopf oder Ebene), damit es nach dem Neuaufbau der Maske gewählt bleibt.</summary>
    private readonly Dictionary<object, string> _selectedField = new();

    private string? _selectedTable;

    private FrameworkElement BuildMask(SectionNode node) => node.Kind switch
    {
        SectionKind.Basics => BasicsMask(),
        SectionKind.Head => FieldListMask(_document!.Head, "head", null),
        SectionKind.Levels => LevelsMask(),
        SectionKind.Level => LevelMask((LevelNode)node.Payload!),
        SectionKind.Constants => ConstantsMask(),
        SectionKind.Languages => LanguagesMask(),
        SectionKind.Assets => AssetsMask(),
        SectionKind.Externals => ExternalsMask(),
        SectionKind.Outputs => OutputsMask(),
        SectionKind.Templates => TemplatesMask(),
        SectionKind.Messages => MessagesMask(),
        SectionKind.AfterGenerate => AfterGenerateMask(),
        SectionKind.Read => ReadMask(),
        SectionKind.Candidates => CandidatesMask(),
        _ => LookupsMask(),
    };

    private FrameworkElement BasicsMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionBasics);
        panel.Children.Add(Row(PackageEditorTexts.IdLabel, PackageEditorTexts.IdReadOnlyTip, new TextBox { Text = document.Id, IsReadOnly = true }));

        // Leer = Paket-Kennung; eine Kopie bringt die Kennung des Originals mit, damit sie dessen Abschnitte in der Mod wiederfindet.
        var markerProblem = Problem();
        var marker = new TextBox { Text = document.MarkerId ?? string.Empty, ToolTip = PackageEditorTexts.MarkerIdTip };
        marker.TextChanged += (_, _) =>
        {
            var text = marker.Text.Trim();
            if (text.Length > 0 && !PackageStore.IsValidPackageId(text))
            {
                markerProblem.Text = PackageEditorTexts.MarkerIdInvalid;
                return;
            }

            markerProblem.Text = string.Empty;
            document.MarkerId = text.Length == 0 ? null : text;
        };
        var markerPanel = new StackPanel();
        markerPanel.Children.Add(marker);
        markerPanel.Children.Add(markerProblem);
        panel.Children.Add(Row(PackageEditorTexts.MarkerIdLabel, PackageEditorTexts.MarkerIdTip, markerPanel));
        var name =new LocalizedTextBox(PackageEditorTexts.NameLabel, document.Name, PackageEditorTexts.NameTip);
        name.ValueChanged += value => document.Name = value;
        panel.Children.Add(Row(PackageEditorTexts.NameLabel, PackageEditorTexts.NameTip, name));
        var games = _host.Games().ToList();
        if (games.All(g => g.Id != document.GameId))
        {
            games.Add((document.GameId, document.GameId));
        }

        panel.Children.Add(Row(PackageEditorTexts.GameLabel, PackageEditorTexts.GameTip, Combo(games, document.GameId, id => document.GameId = id ?? document.GameId)));
        // Schlüsselfelder als Häkchen in der Reihenfolge des Kopfs; mindestens eins bleibt gewählt
        var keyPanel = new StackPanel();
        var keyProblem = Problem();
        foreach (var field in document.Head.Items)
        {
            var id = field.Id;
            var box = new CheckBox { Content = new TextBlock { Text = id }, ToolTip = PackageEditorTexts.KeyFieldsTip, IsChecked = document.KeyFields.Contains(id), Margin = new Thickness(0, 2, 0, 2) };
            box.Click += (_, _) =>
            {
                var chosen = document.KeyFields.Where(k => k != id).ToHashSet();
                if (box.IsChecked == true)
                {
                    chosen.Add(id);
                }

                if (chosen.Count == 0)
                {
                    box.IsChecked = true;
                    keyProblem.Text = PackageEditorTexts.KeyFieldsNeedOne;
                    return;
                }

                keyProblem.Text = string.Empty;
                document.KeyFields = document.Head.Items.Select(f => f.Id).Where(chosen.Contains).ToList();
            };
            keyPanel.Children.Add(box);
        }

        keyPanel.Children.Add(keyProblem);
        panel.Children.Add(Row(PackageEditorTexts.KeyFields, PackageEditorTexts.KeyFieldsTip, keyPanel));
        panel.Children.Add(Row(PackageEditorTexts.KeySeparator, PackageEditorTexts.KeySeparatorTip,
            Text(document.KeySeparator, PackageEditorTexts.KeySeparatorTip, v => document.KeySeparator = v, v => v.Length == 0 ? GeneratorsTexts.PatternEmpty : null)));

        var targets = new StackPanel();
        var problem = Problem();
        CheckBox Target(string id, string text, string tip)
        {
            var box = new CheckBox { Content = new TextBlock { Text = text }, ToolTip = tip, IsChecked = document.Targets.Contains(id), Margin = new Thickness(0, 2, 0, 2) };
            box.Click += (_, _) =>
            {
                var next = document.Targets.Where(t => t != id).ToList();
                if (box.IsChecked == true)
                {
                    next.Add(id);
                }

                if (next.Count == 0)
                {
                    box.IsChecked = true;
                    problem.Text = PackageEditorTexts.TargetsNeedOne;
                    return;
                }

                problem.Text = string.Empty;
                document.Targets = new[] { "mod", "output" }.Where(next.Contains).ToList();
            };
            return box;
        }

        targets.Children.Add(Target("mod", PackageEditorTexts.TargetMod, PackageEditorTexts.TargetModTip));
        targets.Children.Add(Target("output", PackageEditorTexts.TargetOutput, PackageEditorTexts.TargetOutputTip));
        targets.Children.Add(problem);
        panel.Children.Add(Row(PackageEditorTexts.Targets, PackageEditorTexts.TargetOutputTip, targets));
        return panel;
    }

    /// <summary>Feldliste (Kopf oder Felder einer Ebene) mit Tabelle, Knöpfen und der Maske des gewählten Felds.</summary>
    private FrameworkElement FieldListMask(FieldList list, object key, string? title)
    {
        var panel = new StackPanel();
        if (title is not null)
        {
            panel.Children.Add(Heading(title));
        }
        else
        {
            panel = MaskPanel(PackageEditorTexts.SectionHead);
        }

        var items = list.Items;
        var listBox = new ListBox { MaxHeight = 220, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var field in items)
        {
            listBox.Items.Add(new ListBoxItem
            {
                Content = new TextBlock { Text = $"{field.Id}  ·  {field.Label.Resolve(UiText.Current.Language)}  ·  {PackageEditorTexts.TypeName(field.Type)}" },
                Tag = field.Id,
            });
        }

        var selected = _selectedField.GetValueOrDefault(key);
        listBox.SelectedItem = listBox.Items.Cast<ListBoxItem>().FirstOrDefault(i => (string)i.Tag == selected) ?? listBox.Items.Cast<ListBoxItem>().FirstOrDefault();
        var fieldHost = new ContentControl();
        void ShowField()
        {
            var id = (listBox.SelectedItem as ListBoxItem)?.Tag as string;
            if (id is not null)
            {
                _selectedField[key] = id;
            }

            fieldHost.Content = list.Items.FirstOrDefault(f => f.Id == id) is { } field ? FieldMask(list, field, key, () => Relabel(field)) : null;
        }

        // Nach dem Umbenennen nur den Listeneintrag ändern: ein Neuaufbau der Maske verschluckte den Klick, mit dem das Feld verlassen wurde.
        void Relabel(FieldNode field)
        {
            if (listBox.SelectedItem is ListBoxItem item)
            {
                item.Tag = field.Id;
                item.Content = new TextBlock { Text = $"{field.Id}  ·  {field.Label.Resolve(UiText.Current.Language)}  ·  {PackageEditorTexts.TypeName(field.Type)}" };
            }
        }

        listBox.SelectionChanged += (_, _) => ShowField();
        ShowField();

        var buttons = ButtonRow(
            (PackageEditorTexts.New, PackageEditorTexts.NewFieldTip, () =>
            {
                var id = FreeId(items.Select(f => f.Id), "field");
                list.Add(id, FieldType.Text);
                _selectedField[key] = id;
                ShowMask();
            }),
            (PackageEditorTexts.Remove, PackageEditorTexts.RemoveFieldTip, () =>
            {
                if ((listBox.SelectedItem as ListBoxItem)?.Tag is string id)
                {
                    list.Remove(id);
                    _selectedField.Remove(key);
                    ShowMask();
                }
            }),
            (PackageEditorTexts.Up, PackageEditorTexts.MoveTip, () => MoveField(list, listBox, -1)),
            (PackageEditorTexts.Down, PackageEditorTexts.MoveTip, () => MoveField(list, listBox, 1)));
        panel.Children.Add(listBox);
        panel.Children.Add(buttons);
        panel.Children.Add(fieldHost);
        return panel;
    }

    private void MoveField(FieldList list, ListBox listBox, int delta)
    {
        if ((listBox.SelectedItem as ListBoxItem)?.Tag is string id)
        {
            list.Move(id, delta);
            ShowMask();
        }
    }

    private FrameworkElement FieldMask(FieldList list, FieldNode field, object key, Action renamed)
    {
        var document = _document!;
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var oldId = field.Id;
        var id = Text(field.Id, PackageEditorTexts.FieldIdTip, null, value =>
            !PackageDocument.IsValidId(value) ? PackageEditorTexts.IdInvalidName
            : value != oldId && list.Items.Any(f => f.Id == value) ? PackageEditorTexts.IdDuplicate
            : null);
        id.LostFocus += (_, _) =>
        {
            var value = id.Text.Trim();
            if (value == oldId || !PackageDocument.IsValidId(value) || list.Items.Any(f => f.Id == value))
            {
                return;
            }

            // Zieht Schlüsselfeld, read-Verweise und fills mit (Teil 5b).
            document.RenameField(list, field, value);
            oldId = value;
            _selectedField[key] = value;
            renamed();
        };
        panel.Children.Add(Row(PackageEditorTexts.FieldId, PackageEditorTexts.FieldIdTip, id));
        var label = new LocalizedTextBox(PackageEditorTexts.FieldLabel, field.Label, PackageEditorTexts.FieldLabelTip);
        label.ValueChanged += value => field.Label = value;
        panel.Children.Add(Row(PackageEditorTexts.FieldLabel, PackageEditorTexts.FieldLabelTip, label));
        var types = Enum.GetValues<FieldType>().Select(t => (PackageDocumentName(t), PackageEditorTexts.TypeName(t))).ToList();
        panel.Children.Add(Row(PackageEditorTexts.FieldType, PackageEditorTexts.FieldTypeTip, Combo(types, PackageDocumentName(field.Type), value =>
        {
            if (value is not null && Enum.TryParse<FieldType>(value, ignoreCase: true, out var type) && type != field.Type)
            {
                field.Type = type;
                ShowMask();
            }
        })));
        panel.Children.Add(Row(string.Empty, PackageEditorTexts.FieldRequiredTip, Check(PackageEditorTexts.FieldRequired, PackageEditorTexts.FieldRequiredTip, field.Required, v => field.Required = v)));
        panel.Children.Add(Row(PackageEditorTexts.FieldPattern, PackageEditorTexts.FieldPatternTip, Text(field.Pattern ?? string.Empty, PackageEditorTexts.FieldPatternTip, v => field.Pattern = v, PatternProblem)));
        panel.Children.Add(Row(PackageEditorTexts.FieldDefault, PackageEditorTexts.FieldDefaultTip, Text(field.Default ?? string.Empty, PackageEditorTexts.FieldDefaultTip, v => field.Default = v)));
        if (field.Type is FieldType.Text or FieldType.MultilineText)
        {
            panel.Children.Add(Row(string.Empty, PackageEditorTexts.FieldMultilingualTip, Check(PackageEditorTexts.FieldMultilingual, PackageEditorTexts.FieldMultilingualTip, field.Multilingual, v => field.Multilingual = v)));
        }

        if (field.Type == FieldType.Number && list.LevelId is null)
        {
            // Nur im Kopf und nur am letzten Schlüsselfeld sinnvoll (der Lader lehnt anderes ab); die Maske baut sich neu, damit der Hinweis stimmt.
            panel.Children.Add(Row(string.Empty, PackageEditorTexts.FieldAutoNumberTip,
                Check(PackageEditorTexts.FieldAutoNumber, PackageEditorTexts.FieldAutoNumberTip, field.AutoNumber, v =>
                {
                    field.AutoNumber = v;
                    ShowMask();
                })));
            if (field.AutoNumber && document.KeyFields.LastOrDefault() != field.Id)
            {
                var warning = Note(PackageEditorTexts.FieldAutoNumberNotLast);
                warning.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
                panel.Children.Add(warning);
            }
        }

        if (field.Type == FieldType.Choice)
        {
            panel.Children.Add(Row(PackageEditorTexts.FieldChoices, PackageEditorTexts.FieldChoicesTip,
                Text(string.Join("\n", field.Choices), PackageEditorTexts.FieldChoicesTip, v => field.Choices = Lines(v), multiline: true)));
        }

        if (field.Type == FieldType.Image)
        {
            panel.Children.Add(Row(PackageEditorTexts.FieldAsset, PackageEditorTexts.FieldAssetTip,
                Combo(document.Assets.Select(a => (a.Id, a.Id)).ToList(), field.Asset, v => field.Asset = v)));
        }

        if (field.Type is FieldType.Text or FieldType.MultilineText)
        {
            LookupOption(panel, list, field);
        }

        return panel;
    }

    private static string? PatternProblem(string pattern)
    {
        if (pattern.Length == 0)
        {
            return null;
        }

        try
        {
            _ = new Regex(pattern);
            return null;
        }
        catch (ArgumentException ex)
        {
            return PackageEditorTexts.FieldPatternInvalid(ex.Message);
        }
    }

    private FrameworkElement LevelsMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionLevels);
        panel.Children.Add(Note(PackageEditorTexts.LevelsNote));
        var levels = document.Levels;
        panel.Children.Add(new TextBlock { Text = string.Join("  →  ", levels.Select(l => l.Id)), Margin = new Thickness(0, 6, 0, 6) });
        panel.Children.Add(ButtonRow((levels.Count == 0 ? PackageEditorTexts.AddRootLevel : PackageEditorTexts.AddChildLevel, PackageEditorTexts.AddLevelTip, AddLevel)));
        return panel;
    }

    private void AddLevel()
    {
        var document = _document!;
        var parent = document.Levels.LastOrDefault();
        var id = FreeId(document.Levels.Select(l => l.Id), parent is null ? "groups" : "items");
        var level = document.AddChildLevel(parent, id);
        RebuildTree(new SectionNode(SectionKind.Level, level));
    }

    private FrameworkElement LevelMask(LevelNode level)
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionLevels + ": " + level.Id);
        var oldId = level.Id;
        var id = Text(level.Id, PackageEditorTexts.LevelIdTip, null, value =>
            !PackageDocument.IsValidId(value) ? PackageEditorTexts.IdInvalidName
            : value != oldId && document.Levels.Any(l => l.Id == value) ? PackageEditorTexts.IdDuplicate
            : null);
        id.LostFocus += (_, _) =>
        {
            var value = id.Text.Trim();
            if (value != oldId && PackageDocument.IsValidId(value) && document.Levels.All(l => l.Id != value))
            {
                // Baumeintrag und Überschrift direkt ändern: ein Neuaufbau verschluckte den Klick, mit dem das Feld verlassen wurde.
                document.RenameLevel(level, value);
                oldId = value;
                if (AllItems().FirstOrDefault(i => i.Tag is SectionNode { Kind: SectionKind.Level } n && ReferenceEquals(n.Payload, level)) is { } item)
                {
                    item.Header = new TextBlock { Text = value };
                }

                ((TextBlock)panel.Children[0]).Text = PackageEditorTexts.SectionLevels + ": " + value;
            }
        };
        panel.Children.Add(Row(PackageEditorTexts.FieldId, PackageEditorTexts.LevelIdTip, id));
        var label = new LocalizedTextBox(PackageEditorTexts.FieldLabel, level.Label, PackageEditorTexts.LevelLabelTip);
        label.ValueChanged += value => level.Label = value;
        panel.Children.Add(Row(PackageEditorTexts.FieldLabel, PackageEditorTexts.LevelLabelTip, label));

        var kinds = new StackPanel { Orientation = Orientation.Horizontal };
        var fixedKind = new RadioButton { Content = new TextBlock { Text = PackageEditorTexts.LevelFixed }, ToolTip = PackageEditorTexts.LevelFixedTip, IsChecked = level.ConstantsTable is not null, Margin = new Thickness(0, 0, 12, 0) };
        var countable = new RadioButton { Content = new TextBlock { Text = PackageEditorTexts.LevelCountable }, ToolTip = PackageEditorTexts.LevelCountableTip, IsChecked = level.ConstantsTable is null };
        fixedKind.Checked += (_, _) =>
        {
            if (level.ConstantsTable is null)
            {
                level.ConstantsTable = document.ConstantTables.FirstOrDefault() ?? "table";
                ShowMask();
            }
        };
        countable.Checked += (_, _) =>
        {
            if (level.ConstantsTable is not null)
            {
                level.ConstantsTable = null;
                ShowMask();
            }
        };
        kinds.Children.Add(fixedKind);
        kinds.Children.Add(countable);
        panel.Children.Add(Row(PackageEditorTexts.LevelKind, PackageEditorTexts.LevelCountableTip, kinds));

        if (level.ConstantsTable is not null)
        {
            panel.Children.Add(Row(PackageEditorTexts.LevelTable, PackageEditorTexts.LevelTableTip,
                Combo(document.ConstantTables.Select(t => (t, t)).ToList(), level.ConstantsTable, v => level.ConstantsTable = v ?? level.ConstantsTable)));
        }
        else
        {
            var counts = Problem();
            void CheckCounts() => counts.Text = level.Min > level.Max || level.Initial is { } i && (i < level.Min || i > level.Max) ? PackageEditorTexts.LevelCountsInvalid : string.Empty;
            panel.Children.Add(Row(PackageEditorTexts.LevelMin, PackageEditorTexts.LevelMinTip, Number(level.Min, false, v =>
            {
                level.Min = v ?? 0;
                CheckCounts();
            })));
            panel.Children.Add(Row(PackageEditorTexts.LevelMax, PackageEditorTexts.LevelMaxTip, Number(level.Max == int.MaxValue ? null : level.Max, true, v =>
            {
                level.Max = v ?? int.MaxValue;
                CheckCounts();
            })));
            panel.Children.Add(Row(PackageEditorTexts.LevelInitial, PackageEditorTexts.LevelInitialTip, Number(level.Initial, true, v =>
            {
                level.Initial = v;
                CheckCounts();
            })));
            panel.Children.Add(counts);
            CheckCounts();
        }

        if (level.Parent is { } parent)
        {
            var choices = parent.Fields.Items.Select(f => f.Id).ToList();
            if (parent.ConstantsTable is { } table)
            {
                choices.AddRange(document.ConstantRows(table).SelectMany(r => r.Keys).Distinct().Where(c => !choices.Contains(c)));
            }

            panel.Children.Add(Row(PackageEditorTexts.LevelNumberBase, PackageEditorTexts.LevelNumberBaseTip,
                Combo(new[] { (string.Empty, PackageEditorTexts.None) }.Concat(choices.Select(c => (c, c))).ToList(), level.NumberBaseField ?? string.Empty,
                    v => level.NumberBaseField = string.IsNullOrEmpty(v) ? null : v)));
        }

        var buttons = level.Child is null
            ? ButtonRow((PackageEditorTexts.AddChildLevel, PackageEditorTexts.AddLevelTip, AddLevel), (PackageEditorTexts.DeleteLevel, PackageEditorTexts.DeleteLevelTip, () => DeleteLevel(level)))
            : ButtonRow((PackageEditorTexts.DeleteLevel, PackageEditorTexts.DeleteLevelTip, () => DeleteLevel(level)));
        panel.Children.Add(buttons);
        panel.Children.Add(FieldListMask(level.Fields, level, PackageEditorTexts.FieldsHeader));
        return panel;
    }

    private void DeleteLevel(LevelNode level)
    {
        if (MessageBox.Show(this, PackageEditorTexts.DeleteLevelAsk(level.Id), PackageEditorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _document!.RemoveLevel(level);
            RebuildTree(new SectionNode(SectionKind.Levels));
        }
    }

    private FrameworkElement ConstantsMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionConstants);
        var tables = document.ConstantTables;
        _selectedTable = tables.Contains(_selectedTable ?? string.Empty) ? _selectedTable : tables.FirstOrDefault();
        var tableList = new ListBox { MaxHeight = 120, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var table in tables)
        {
            tableList.Items.Add(new ListBoxItem { Content = new TextBlock { Text = table }, Tag = table, IsSelected = table == _selectedTable });
        }

        tableList.SelectionChanged += (_, _) =>
        {
            _selectedTable = (tableList.SelectedItem as ListBoxItem)?.Tag as string;
            ShowMask();
        };
        panel.Children.Add(Heading(PackageEditorTexts.Tables));
        panel.Children.Add(tableList);
        panel.Children.Add(ButtonRow(
            (PackageEditorTexts.New, PackageEditorTexts.NewTableTip, () =>
            {
                if (AskName(PackageEditorTexts.TableName, string.Empty) is { } name && !document.ConstantTables.Contains(name))
                {
                    document.AddConstantTable(name);
                    _selectedTable = name;
                    ShowMask();
                }
            }),
            (PackageEditorTexts.Rename, PackageEditorTexts.RenameTableTip, () =>
            {
                if (_selectedTable is { } old && AskName(PackageEditorTexts.TableName, old) is { } name && name != old && !document.ConstantTables.Contains(name))
                {
                    document.RenameConstantTable(old, name);
                    _selectedTable = name;
                    ShowMask();
                }
            }),
            (PackageEditorTexts.Delete, PackageEditorTexts.DeleteTableTip, () =>
            {
                if (_selectedTable is not { } table)
                {
                    return;
                }

                var users = document.LevelsUsingTable(table);
                if (users.Count > 0 && MessageBox.Show(this, PackageEditorTexts.DeleteTableAsk(table, string.Join(", ", users)), PackageEditorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }

                document.RemoveConstantTable(table);
                _selectedTable = null;
                ShowMask();
            })));

        if (_selectedTable is null)
        {
            panel.Children.Add(Note(PackageEditorTexts.NoTable));
            return panel;
        }

        panel.Children.Add(new ConstantsGrid(this, document, _selectedTable).Build());
        return panel;
    }

    private FrameworkElement LanguagesMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionLanguages);
        panel.Children.Add(Note(PackageEditorTexts.LanguagesNote));
        var rules = document.Languages;
        panel.Children.Add(Check(PackageEditorTexts.ExtendEnglish, PackageEditorTexts.ExtendEnglishTip, rules.ExtendEnglish, v => document.Languages = document.Languages with { ExtendEnglish = v }));
        panel.Children.Add(Check(PackageEditorTexts.CopyEnglish, PackageEditorTexts.CopyEnglishTip, rules.CopyEnglish, v => document.Languages = document.Languages with { CopyEnglish = v }));
        panel.Children.Add(Check(PackageEditorTexts.EnglishForMissing, PackageEditorTexts.EnglishForMissingTip, rules.EnglishForMissing, v => document.Languages = document.Languages with { EnglishForMissing = v }));
        return panel;
    }

    private FrameworkElement AssetsMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionAssets);
        var assets = document.Assets.ToList();
        void Write()
        {
            // Nicht schreiben, solange zwei Kennungen gleich sind (eine überschriebe das andere Asset); bis dahin bleibt der letzte gute Stand im Paket.
            var ids = assets.Select(a => a.Id).ToList();
            if (ids.Select((id, i) => PackageDocument.IdIssueOf(ids, i, id)).All(issue => issue is not IdIssue.Duplicate))
            {
                document.Assets = assets.ToList();
            }
        }

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        foreach (var width in new[] { 130.0, 260, 120, 80, double.NaN })
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = double.IsNaN(width) ? GridLength.Auto : new GridLength(width) });
        }

        void Cell(UIElement element, int row, int column)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            if (element is FrameworkElement framework)
            {
                framework.Margin = new Thickness(0, 2, 6, 2);
            }

            grid.Children.Add(element);
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Cell(new TextBlock { Text = PackageEditorTexts.AssetId, FontWeight = FontWeights.SemiBold, ToolTip = PackageEditorTexts.AssetIdTip }, 0, 0);
        Cell(new TextBlock { Text = PackageEditorTexts.AssetPath, FontWeight = FontWeights.SemiBold, ToolTip = PackageEditorTexts.AssetPathTip }, 0, 1);
        Cell(new TextBlock { Text = PackageEditorTexts.AssetOrigin, FontWeight = FontWeights.SemiBold, ToolTip = PackageEditorTexts.AssetOriginTip }, 0, 2);
        for (var i = 0; i < assets.Count; i++)
        {
            var index = i;
            var row = i + 1;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var (id, asset) = assets[index];
            // Ungültige oder doppelte Kennungen nicht schreiben: eine doppelte überschriebe in package.json das andere Asset.
            string? AssetIdProblem(string v) => PackageDocument.IdIssueOf(assets.Select(a => a.Id).ToList(), index, v) switch
            {
                IdIssue.Invalid => PackageEditorTexts.IdInvalidName,
                IdIssue.Duplicate => PackageEditorTexts.IdDuplicate,
                _ => null,
            };

            Cell(Text(id, PackageEditorTexts.AssetIdTip, v =>
            {
                assets[index] = (v, assets[index].Asset);
                Write();
            }, AssetIdProblem), row, 0);
            var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            void UpdateStatus(string path)
            {
                var full = _entry is null || !SafeRelativePath.IsSafe(path) ? null : Path.Combine(_entry.DataFolder, path.Replace('/', Path.DirectorySeparatorChar));
                var present = full is not null && (File.Exists(full) || Directory.Exists(full));
                status.Text = present ? PackageEditorTexts.AssetPresent : PackageEditorTexts.AssetMissing;
                status.SetResourceReference(TextBlock.ForegroundProperty, present ? "Theme.TextMuted" : "Theme.Warning");
            }

            Cell(Text(asset.Path, PackageEditorTexts.AssetPathTip, v =>
            {
                assets[index] = (assets[index].Id, assets[index].Asset with { Path = v });
                Write();
                UpdateStatus(v);
            }, v => SafeRelativePath.IsSafe(v) ? null : PackageEditorTexts.AssetPathInvalid), row, 1);
            Cell(Combo(new List<(string, string)> { ("game", PackageEditorTexts.OriginGame), ("mod", PackageEditorTexts.OriginMod) }, asset.Origin == Generators.AssetOrigin.Mod ? "mod" : "game", v =>
            {
                assets[index] = (assets[index].Id, assets[index].Asset with { Origin = v == "mod" ? Generators.AssetOrigin.Mod : Generators.AssetOrigin.Game });
                Write();
            }, PackageEditorTexts.AssetOriginTip), row, 2);
            UpdateStatus(asset.Path);
            Cell(status, row, 3);
            Cell(ButtonRow(
                (PackageEditorTexts.ChooseFile, PackageEditorTexts.ChooseFileTip, () => ChooseAssetFile(assets, index, Write)),
                (PackageEditorTexts.Remove, PackageEditorTexts.RemoveAssetTip, () =>
                {
                    assets.RemoveAt(index);
                    Write();
                    ShowMask();
                })), row, 4);
        }

        panel.Children.Add(grid);
        panel.Children.Add(ButtonRow((PackageEditorTexts.Add, PackageEditorTexts.AddAssetTip, () =>
        {
            assets.Add((FreeId(assets.Select(a => a.Id), "asset"), new AssetDef("assets/", Generators.AssetOrigin.Game)));
            Write();
            ShowMask();
        })));
        return panel;
    }

    private void ChooseAssetFile(List<(string Id, AssetDef Asset)> assets, int index, Action write)
    {
        if (_entry is null)
        {
            return;
        }

        var dialog = new OpenFileDialog { ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("packageEditor.asset") };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var folder = Path.Combine(_entry.DataFolder, "assets");
            Directory.CreateDirectory(folder);
            var name = Path.GetFileName(dialog.FileName);
            File.Copy(dialog.FileName, Path.Combine(folder, name), overwrite: true);
            assets[index] = (assets[index].Id, assets[index].Asset with { Path = "assets/" + name });
            write();
            ShowMask();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, PackageEditorTexts.SaveFailed(ex.Message), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private FrameworkElement MessagesMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionMessages);
        panel.Children.Add(MessageList(PackageEditorTexts.Warnings, () => document.Warnings, v => document.Warnings = v));
        panel.Children.Add(MessageList(PackageEditorTexts.Errors, () => document.Errors, v => document.Errors = v));
        return panel;
    }

    private FrameworkElement MessageList(string title, Func<IReadOnlyList<(string When, LocalizedText Message)>> get, Action<IReadOnlyList<(string When, LocalizedText Message)>> set)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(Heading(title));
        var items = get().ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var index = i;
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var remove = ButtonRow((PackageEditorTexts.Remove, PackageEditorTexts.Remove, () =>
            {
                items.RemoveAt(index);
                set(items.ToList());
                ShowMask();
            }));
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            var when = Text(items[index].When, PackageEditorTexts.ConditionTip, v =>
            {
                items[index] = (v, items[index].Message);
                set(items.ToList());
            });
            when.Width = 260;
            when.FontFamily = new FontFamily("Consolas");
            DockPanel.SetDock(when, Dock.Left);
            row.Children.Add(when);
            var message = new LocalizedTextBox(PackageEditorTexts.Condition, items[index].Message, PackageEditorTexts.MessageTip) { Margin = new Thickness(6, 0, 6, 0) };
            message.ValueChanged += v =>
            {
                items[index] = (items[index].When, v);
                set(items.ToList());
            };
            row.Children.Add(message);
            panel.Children.Add(row);
        }

        panel.Children.Add(ButtonRow((PackageEditorTexts.Add, PackageEditorTexts.AddMessageTip, () =>
        {
            items.Add(("true", LocalizedText.Empty));
            set(items.ToList());
            ShowMask();
        })));
        return panel;
    }

    // ---- Bausteine ----
    private static StackPanel MaskPanel(string title)
    {
        var panel = new StackPanel { Margin = new Thickness(4) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        return panel;
    }

    private static TextBlock Heading(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) };

    private static TextBlock Note(string text)
    {
        var note = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };
        note.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return note;
    }

    private static TextBlock Problem()
    {
        var problem = new TextBlock { TextWrapping = TextWrapping.Wrap };
        problem.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
        return problem;
    }

    /// <summary>Zeile mit Beschriftung links (fester Breite) und Eingabe rechts; der Tooltip steht an beiden.</summary>
    private static FrameworkElement Row(string label, string tip, FrameworkElement input)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var text = new TextBlock { Text = label, Width = LabelWidth, ToolTip = tip, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 8, 0), TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(text, Dock.Left);
        row.Children.Add(text);
        input.ToolTip ??= tip;
        row.Children.Add(input);
        return row;
    }

    /// <summary>Textfeld; <paramref name="set"/> bei jeder Änderung, <paramref name="validate"/> färbt den Rahmen rot und nennt den Grund im Tooltip.</summary>
    private TextBox Text(string value, string tip, Action<string>? set, Func<string, string?>? validate = null, bool multiline = false)
    {
        var box = new TextBox
        {
            Text = value,
            ToolTip = tip,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = multiline ? 60 : 0,
            VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
        };
        var normalBorder = box.BorderBrush;
        void Check()
        {
            var problem = validate?.Invoke(box.Text.Trim());
            if (problem is null)
            {
                box.BorderBrush = normalBorder;
                box.ToolTip = tip;
            }
            else
            {
                box.BorderBrush = (Brush)FindResource("Theme.Danger");
                box.ToolTip = problem;
            }
        }

        box.TextChanged += (_, _) =>
        {
            Check();
            set?.Invoke(multiline ? box.Text : box.Text.Trim());
        };
        Check();
        return box;
    }

    private TextBox Number(int? value, bool allowEmpty, Action<int?> set)
    {
        return Text(value?.ToString() ?? string.Empty, string.Empty, v =>
        {
            if (v.Length == 0 && allowEmpty)
            {
                set(null);
            }
            else if (int.TryParse(v, out var number))
            {
                set(number);
            }
        }, v => v.Length == 0 && allowEmpty || int.TryParse(v, out _) ? null : PackageEditorTexts.NumberInvalid);
    }

    private static CheckBox Check(string text, string tip, bool value, Action<bool> set)
    {
        var box = new CheckBox { Content = new TextBlock { Text = text }, ToolTip = tip, IsChecked = value, Margin = new Thickness(0, 2, 0, 2) };
        box.Click += (_, _) => set(box.IsChecked == true);
        return box;
    }

    /// <summary>Auswahl aus (Wert, Anzeige); ein unbekannter aktueller Wert wird als eigener Eintrag gezeigt.</summary>
    private static ComboBox Combo(IReadOnlyList<(string Value, string Label)> items, string? selected, Action<string?> set, string? tip = null)
    {
        var combo = new ComboBox { ToolTip = tip };
        var all = items.ToList();
        if (!string.IsNullOrEmpty(selected) && all.All(i => i.Value != selected))
        {
            all.Add((selected, selected));
        }

        foreach (var (value, label) in all)
        {
            combo.Items.Add(new ComboBoxItem { Content = new TextBlock { Text = label }, Tag = value, IsSelected = value == (selected ?? string.Empty) });
        }

        combo.SelectionChanged += (_, _) => set((combo.SelectedItem as ComboBoxItem)?.Tag as string);
        return combo;
    }

    private static FrameworkElement ButtonRow(params (string Text, string Tip, Action Click)[] buttons)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
        foreach (var (text, tip, click) in buttons)
        {
            var button = new Button { Content = new TextBlock { Text = text }, ToolTip = tip, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 0) };
            button.Click += (_, _) => click();
            row.Children.Add(button);
        }

        return row;
    }

    private string? AskName(string message, string initial) =>
        TextPromptWindow.Ask(this, PackageEditorTexts.Title, message, initial) is { } name && name.Trim().Length > 0 ? name.Trim() : null;

    private static IReadOnlyList<string> Lines(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    private static string FreeId(IEnumerable<string> used, string stem)
    {
        var taken = used.ToHashSet(StringComparer.Ordinal);
        var n = 1;
        while (taken.Contains(stem + n))
        {
            n++;
        }

        return stem + n;
    }

    private static string PackageDocumentName(FieldType type) => char.ToLowerInvariant(type.ToString()[0]) + type.ToString()[1..];
}
