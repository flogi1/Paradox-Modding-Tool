using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Lookups;
using ParadoxModTool.Localization;
using DataGrid = System.Windows.Controls.DataGrid;
using DataGridTextColumn = System.Windows.Controls.DataGridTextColumn;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Maske "Nachschlagen" (Spec Teil 5b, Abschnitt 2) und die Feld-Angabe "Aus Datenbank" (Abschnitt 3).</summary>
public sealed partial class PackageEditorWindow
{
    private string? _selectedSource;

    /// <summary>Hinweis nach einem Artwechsel, einmal nach dem Neuaufbau gezeigt.</summary>
    private string? _sourceNote;

    private FrameworkElement LookupsMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionLookups);
        panel.Children.Add(Note(PackageEditorTexts.LookupsIntro));
        var sources = document.LookupSources;
        if (sources.All(s => s.Id != _selectedSource))
        {
            _selectedSource = sources.FirstOrDefault()?.Id;
        }

        var list = new ListBox { MaxHeight = 200, Margin = new Thickness(0, 4, 0, 4) };
        foreach (var source in sources)
        {
            list.Items.Add(new ListBoxItem { Content = new TextBlock { Text = SourceLine(source) }, Tag = source.Id, IsSelected = source.Id == _selectedSource });
        }

        list.SelectionChanged += (_, _) =>
        {
            _selectedSource = (list.SelectedItem as ListBoxItem)?.Tag as string;
            ShowMask();
        };
        panel.Children.Add(list);
        panel.Children.Add(ButtonRow(
            (PackageEditorTexts.New, PackageEditorTexts.NewSourceTip, () =>
            {
                _selectedSource = document.AddLookupSource(FreeId(document.LookupSources.Select(s => s.Id), "source"), LookupKind.GameKeys).Id;
                ShowMask();
            }),
            (PackageEditorTexts.Duplicate, PackageEditorTexts.DuplicateSourceTip, () =>
            {
                if (_selectedSource is { } id)
                {
                    _selectedSource = document.DuplicateLookupSource(id).Id;
                    ShowMask();
                }
            }),
            (PackageEditorTexts.Remove, PackageEditorTexts.RemoveSourceTip, () =>
            {
                if (_selectedSource is not { } id)
                {
                    return;
                }

                var users = document.FieldsUsingSource(id).Count;
                if (users > 0 && MessageBox.Show(this, PackageEditorTexts.RemoveSourceConfirm(id, users), PackageEditorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                document.RemoveLookupSource(id);
                _selectedSource = null;
                ShowMask();
            }),
            (PackageEditorTexts.Up, PackageEditorTexts.MoveTip, () => MoveSource(-1)),
            (PackageEditorTexts.Down, PackageEditorTexts.MoveTip, () => MoveSource(1))));

        if (sources.FirstOrDefault(s => s.Id == _selectedSource) is not { } selected)
        {
            panel.Children.Add(Note(PackageEditorTexts.LookupsNone));
            return panel;
        }

        panel.Children.Add(SourceMask(selected, renamed =>
        {
            if (list.SelectedItem is ListBoxItem item)
            {
                item.Tag = renamed.Id;
                item.Content = new TextBlock { Text = SourceLine(renamed) };
            }
        }));
        return panel;
    }

    private static string SourceLine(LookupSourceNode source) =>
        $"{source.Id}  ·  {(source.Kind == LookupKind.Json ? PackageEditorTexts.KindJson : PackageEditorTexts.KindGameKeys)}  ·  {source.Label.Resolve(UiText.Current.Language)}";

    private void MoveSource(int delta)
    {
        if (_selectedSource is { } id)
        {
            _document!.MoveLookupSource(id, delta);
            ShowMask();
        }
    }

    private FrameworkElement SourceMask(LookupSourceNode source, Action<LookupSourceNode> renamed)
    {
        var document = _document!;
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        if (_sourceNote is { } note)
        {
            var text = Note(note);
            text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
            panel.Children.Add(text);
            _sourceNote = null;
        }

        var oldId = source.Id;
        var id = Text(source.Id, PackageEditorTexts.SourceIdTip, null, value =>
            !PackageDocument.IsValidId(value) ? PackageEditorTexts.IdInvalidName
            : value != oldId && document.LookupSources.Any(s => s.Id == value) ? PackageEditorTexts.IdDuplicate
            : null);
        id.LostFocus += (_, _) =>
        {
            var value = id.Text.Trim();
            if (value != oldId && PackageDocument.IsValidId(value) && document.LookupSources.All(s => s.Id != value))
            {
                // Nur den Listeneintrag ändern: ein Neuaufbau der Maske verschluckte den Klick, mit dem das Feld verlassen wurde.
                document.RenameLookupSource(source, value);
                oldId = value;
                _selectedSource = value;
                renamed(source);
            }
        };
        panel.Children.Add(Row(PackageEditorTexts.SourceId, PackageEditorTexts.SourceIdTip, id));
        var label = new LocalizedTextBox(PackageEditorTexts.SourceLabel, source.Label, PackageEditorTexts.SourceLabelTip);
        label.ValueChanged += value => source.Label = value;
        panel.Children.Add(Row(PackageEditorTexts.SourceLabel, PackageEditorTexts.SourceLabelTip, label));
        var kinds = new List<(string, string)> { ("gameKeys", PackageEditorTexts.KindGameKeys), ("json", PackageEditorTexts.KindJson) };
        panel.Children.Add(Row(PackageEditorTexts.SourceKind, PackageEditorTexts.SourceKindTip,
            Combo(kinds, source.Kind == LookupKind.Json ? "json" : "gameKeys", value =>
            {
                var kind = value == "json" ? LookupKind.Json : LookupKind.GameKeys;
                if (value is null || kind == source.Kind)
                {
                    return;
                }

                source.Kind = kind;
                _sourceNote = PackageEditorTexts.KindChanged;
                ShowMask();
            }, PackageEditorTexts.SourceKindTip)));

        if (source.Kind == LookupKind.GameKeys)
        {
            panel.Children.Add(Row(PackageEditorTexts.SourceFolder, PackageEditorTexts.SourceFolderTip,
                Text(source.Get("folder") ?? string.Empty, PackageEditorTexts.SourceFolderTip, v => source.SetText("folder", v), PatternCheck.Path)));
            panel.Children.Add(Row(PackageEditorTexts.SourceDepth, PackageEditorTexts.SourceDepthTip,
                Combo(new List<(string, string)> { ("1", "1"), ("2", "2") }, source.Depth.ToString(), v => source.Depth = v == "2" ? 2 : 1, PackageEditorTexts.SourceDepthTip)));
            panel.Children.Add(Row(string.Empty, PackageEditorTexts.SourceNamedOnlyTip,
                Check(PackageEditorTexts.SourceNamedOnly, PackageEditorTexts.SourceNamedOnlyTip, source.NamedOnly, v => source.NamedOnly = v)));
            panel.Children.Add(Row(PackageEditorTexts.SourceTagFromFile, PackageEditorTexts.SourceTagFromFileTip,
                Text(source.Get("tagFromFile") ?? string.Empty, PackageEditorTexts.SourceTagFromFileTip, v => source.Set("tagFromFile", v), v => v.Length == 0 ? null : PatternCheck.TagFromFile(v))));
            panel.Children.Add(Row(PackageEditorTexts.SourceTagInBlock, PackageEditorTexts.SourceTagInBlockTip,
                Text(string.Join(", ", source.TagInBlock), PackageEditorTexts.SourceTagInBlockTip,
                    v => source.TagInBlock = v.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList())));
        }
        else
        {
            var assets = document.Assets.Select(a => (a.Id, a.Id)).ToList();
            panel.Children.Add(Row(PackageEditorTexts.SourceAsset, PackageEditorTexts.SourceAssetTip,
                Combo(assets, source.Get("asset"), v => source.SetText("asset", v ?? string.Empty), PackageEditorTexts.SourceAssetTip)));
            panel.Children.Add(Row(PackageEditorTexts.SourceItems, PackageEditorTexts.SourceItemsTip,
                Text(source.Get("items") ?? string.Empty, PackageEditorTexts.SourceItemsTip, v => source.SetText("items", v), v => v.Length == 0 ? GeneratorsTexts.PatternEmpty : null)));
            panel.Children.Add(Heading(PackageEditorTexts.SourceFieldsHeading));
            foreach (var (name, text) in new[]
            {
                ("key", PackageEditorTexts.EntryKey), ("name", PackageEditorTexts.EntryName), ("desc", PackageEditorTexts.EntryDesc),
                ("tag", PackageEditorTexts.EntryTag), ("nation", PackageEditorTexts.EntryNation),
            })
            {
                panel.Children.Add(Row(text, PackageEditorTexts.SourceFieldTip,
                    Text(source.JsonField(name) ?? string.Empty, PackageEditorTexts.SourceFieldTip, v => source.SetJsonField(name, v))));
            }
        }

        var result = new ContentControl { Tag = ToolTag };
        panel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 6) });
        panel.Children.Add(Tool(ButtonRow((PackageEditorTexts.Preview, PackageEditorTexts.PreviewTip, () => result.Content = PreviewView(source.Id, result)))));
        panel.Children.Add(result);
        return panel;
    }

    /// <summary>Zählzeile und Tabelle der ersten Einträge, oder der Grund samt Knopf, wenn nichts kommt.</summary>
    private FrameworkElement PreviewView(string sourceId, ContentControl host)
    {
        var preview = LookupPreview.Run(_document!, _files!, _pending, sourceId, _host.GameRoots(), dataFolder: _entry?.DataFolder);
        var panel = new StackPanel();
        if (preview.Reason is { } reason)
        {
            var text = Note(reason);
            text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
            panel.Children.Add(text);
            if (preview.ReasonKind == "gameFolder")
            {
                panel.Children.Add(ButtonRow((PackageEditorTexts.PreviewChooseGameFolder, PackageEditorTexts.PreviewChooseGameFolder, () =>
                {
                    if (_host.ChooseGameFolder())
                    {
                        host.Content = PreviewView(sourceId, host);
                    }
                })));
            }
            else if (preview.ReasonKind == "asset")
            {
                panel.Children.Add(ButtonRow((PackageEditorTexts.PreviewChooseAsset, PackageEditorTexts.PreviewChooseAsset, () => SelectSection(SectionKind.Assets))));
            }

            return panel;
        }

        panel.Children.Add(new TextBlock { Text = PackageEditorTexts.PreviewCount(preview.Count), Margin = new Thickness(0, 0, 0, 4) });
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, MaxHeight = 320, HeadersVisibility = DataGridHeadersVisibility.Column };
        foreach (var (header, path) in new[]
        {
            (PackageEditorTexts.EntryKey, nameof(LookupEntry.Key)), (PackageEditorTexts.EntryName, nameof(LookupEntry.Name)),
            (PackageEditorTexts.EntryTag, nameof(LookupEntry.Tag)), (PackageEditorTexts.EntryNation, nameof(LookupEntry.Nation)),
        })
        {
            grid.Columns.Add(new DataGridTextColumn { Header = new TextBlock { Text = header }, Binding = new System.Windows.Data.Binding(path) });
        }

        grid.ItemsSource = preview.Entries;
        panel.Children.Add(grid);
        return panel;
    }

    /// <summary>Feld-Angabe "Aus Datenbank" in der Feldmaske von Text- und Mehrzeilen-Feldern (Spec Teil 5b, Abschnitt 3).</summary>
    private void LookupOption(StackPanel panel, FieldList list, FieldNode field)
    {
        var document = _document!;
        var lookup = field.Lookup;
        panel.Children.Add(Row(string.Empty, PackageEditorTexts.LookupEnableTip, Check(PackageEditorTexts.LookupEnable, PackageEditorTexts.LookupEnableTip, lookup is not null, on =>
        {
            if (on)
            {
                var created = field.EnableLookup();
                created.SetFill("name", field.Id);
            }
            else
            {
                field.DisableLookup();
            }

            ShowMask();
        })));
        if (lookup is null)
        {
            return;
        }

        var sources = document.LookupSources;
        if (sources.Count == 0)
        {
            panel.Children.Add(Note(PackageEditorTexts.LookupNoSources));
            panel.Children.Add(ButtonRow((PackageEditorTexts.LookupGoToSources, PackageEditorTexts.LookupNoSources, () => SelectSection(SectionKind.Lookups))));
            return;
        }

        var checks = new StackPanel();
        foreach (var source in sources)
        {
            var id = source.Id;
            checks.Children.Add(Check($"{id}  ·  {source.Label.Resolve(UiText.Current.Language)}", PackageEditorTexts.LookupSourcesTip, lookup.Sources.Contains(id), on =>
            {
                var chosen = lookup.Sources.Where(s => s != id).ToHashSet(StringComparer.Ordinal);
                if (on)
                {
                    chosen.Add(id);
                }

                // Reihenfolge wie unter "Nachschlagen"; unbekannte Quellen bleiben am Ende stehen.
                lookup.Sources = document.LookupSources.Select(s => s.Id).Where(chosen.Contains)
                    .Concat(lookup.Sources.Where(s => chosen.Contains(s) && document.LookupSources.All(x => x.Id != s))).ToList();
            }));
        }

        panel.Children.Add(Row(PackageEditorTexts.LookupSources, PackageEditorTexts.LookupSourcesTip, checks));
        var texts = new List<(string, string)> { (string.Empty, PackageEditorTexts.LookupFillNone) };
        texts.AddRange(list.Items.Where(f => f.Type is FieldType.Text or FieldType.MultilineText).Select(f => (f.Id, f.Id)));
        panel.Children.Add(Row(PackageEditorTexts.LookupFillName, PackageEditorTexts.LookupFillTip,
            Combo(texts, lookup.Fill("name") ?? string.Empty, v => lookup.SetFill("name", string.IsNullOrEmpty(v) ? null : v), PackageEditorTexts.LookupFillTip)));
        panel.Children.Add(Row(PackageEditorTexts.LookupFillDesc, PackageEditorTexts.LookupFillTip,
            Combo(texts, lookup.Fill("desc") ?? string.Empty, v => lookup.SetFill("desc", string.IsNullOrEmpty(v) ? null : v), PackageEditorTexts.LookupFillTip)));
    }

    /// <summary>Sprung aus "Prüfen" zu einer Quelle oder zur Feld-Angabe eines Felds.</summary>
    private void JumpToLookup(string where)
    {
        if (where.StartsWith("lookups[", StringComparison.Ordinal))
        {
            _selectedSource = where[8..where.IndexOf(']')];
            SelectSection(SectionKind.Lookups);
            return;
        }

        // fields[<Ebene oder head>.<Feld>].lookup
        var inner = where[7..where.IndexOf(']')];
        var dot = inner.IndexOf('.');
        var (levelId, fieldId) = (inner[..dot], inner[(dot + 1)..]);
        if (levelId == "head")
        {
            _selectedField["head"] = fieldId;
            SelectSection(SectionKind.Head);
            return;
        }

        if (AllItems().FirstOrDefault(i => i.Tag is SectionNode { Kind: SectionKind.Level, Payload: LevelNode level } && level.Id == levelId) is { Tag: SectionNode { Payload: { } payload } } item)
        {
            _selectedField[payload] = fieldId;
            if (item.IsSelected)
            {
                ShowMask();
            }
            else
            {
                item.IsSelected = true;
            }
        }
    }
}
