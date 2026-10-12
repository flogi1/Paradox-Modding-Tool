using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Editing.Package;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Maske "Einlesen" (Spec Teil 5b, Abschnitt 1): Muster für Schlüssel, Elemente, Lokalisierung, Merkmale und Bilder mit Vorschlag und Probelauf.</summary>
public sealed partial class PackageEditorWindow
{
    /// <summary>Mod für Vorschlag und Probelauf; bleibt für die Sitzung des Fensters gewählt.</summary>
    private string? _exampleMod;

    private FrameworkElement ReadMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionRead);
        var read = document.Read;
        panel.Children.Add(Check(PackageEditorTexts.ReadEnable, PackageEditorTexts.ReadEnableTip, read is not null, on =>
        {
            if (on)
            {
                document.EnableRead();
            }
            else if (MessageBox.Show(this, PackageEditorTexts.ReadDisableConfirm, PackageEditorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                document.DisableRead();
            }

            ShowMask();
        }));
        if (read is null)
        {
            panel.Children.Add(Note(PackageEditorTexts.ReadOffNote));
            return panel;
        }

        panel.Children.Add(Heading(PackageEditorTexts.ReadKeysHeading));
        var fromBlocks = read.KeysBlock.Length > 0;
        panel.Children.Add(Row(PackageEditorTexts.ReadKeysFrom, PackageEditorTexts.ReadKeysFromTip,
            Combo(new List<(string, string)> { ("path", PackageEditorTexts.ReadKeysFromPath), ("blocks", PackageEditorTexts.ReadKeysFromBlocks) }, fromBlocks ? "blocks" : "path", v =>
            {
                if ((v == "blocks") != fromBlocks)
                {
                    // Umschalten: bei Blöcken ein Platzhalter-Muster, damit keys ein Objekt wird; zurück wird keys wieder ein Text
                    read.KeysBlock = v == "blocks" ? "(?<key>.+)" : string.Empty;
                    ShowMask();
                }
            }, PackageEditorTexts.ReadKeysFromTip)));
        if (fromBlocks)
        {
            panel.Children.Add(Row(PackageEditorTexts.ReadKeysFile, PackageEditorTexts.ReadKeysFileTip,
                CodeChecked(read.Keys, PackageEditorTexts.ReadKeysFileTip, v => read.Keys = v, PatternCheck.KeysFile)));
            panel.Children.Add(Row(PackageEditorTexts.ReadKeysBlock, PackageEditorTexts.ReadKeysBlockTip,
                CodeChecked(read.KeysBlock, PackageEditorTexts.ReadKeysBlockTip, v => read.KeysBlock = v, v => PatternCheck.KeysBlock(v, document.KeyFields))));
        }
        else
        {
            panel.Children.Add(Row(PackageEditorTexts.ReadKeys, PackageEditorTexts.ReadKeysTip,
                PatternBox(read.Keys, PackageEditorTexts.ReadKeysTip, v => read.Keys = v, PatternCheck.Keys, PatternKind.Keys, null)));
        }

        panel.Children.Add(Heading(PackageEditorTexts.ReadElementsHeading));
        panel.Children.Add(Row(PackageEditorTexts.ReadElementsFile, PackageEditorTexts.ReadElementsFileTip,
            PatternBox(read.ElementsFile, PackageEditorTexts.ReadElementsFileTip, v => read.ElementsFile = v, PatternCheck.Path, PatternKind.File, null)));
        panel.Children.Add(Row(PackageEditorTexts.ReadElementsBlock, PackageEditorTexts.ReadElementsBlockTip,
            PatternBox(read.ElementsBlock, PackageEditorTexts.ReadElementsBlockTip, v => read.ElementsBlock = v, PatternCheck.Numbered, PatternKind.Block, () => read.ElementsFile)));
        panel.Children.Add(Row(PackageEditorTexts.ReadElementsLevel, PackageEditorTexts.ReadElementsLevelTip,
            Combo(document.Levels.Select(l => (l.Id, l.Id)).ToList(), read.ElementsLevel, v =>
            {
                if (v is not null && v != read.ElementsLevel)
                {
                    read.ElementsLevel = v;
                    ShowMask();
                }
            }, PackageEditorTexts.ReadElementsLevelTip)));
        if (document.Levels.FirstOrDefault(l => l.Id == read.ElementsLevel) is { } level)
        {
            foreach (var field in level.Fields.Items)
            {
                var current = read.ElementFields.GetValueOrDefault(field.Id, (false, Array.Empty<string>()));
                var body = current.Body;
                IReadOnlyList<string> except = current.Except;
                var row = new DockPanel();
                var exceptBox = Text(string.Join(", ", except), PackageEditorTexts.ReadExceptTip, v =>
                {
                    except = v.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    read.SetElementField(field.Id, body, except);
                });
                var check = Check(PackageEditorTexts.ReadBody, PackageEditorTexts.ReadBodyTip, body, v =>
                {
                    body = v;
                    read.SetElementField(field.Id, body, except);
                });
                check.Margin = new Thickness(0, 2, 12, 2);
                DockPanel.SetDock(check, Dock.Left);
                row.Children.Add(check);
                row.Children.Add(Row(PackageEditorTexts.ReadExcept, PackageEditorTexts.ReadExceptTip, exceptBox));
                panel.Children.Add(Row(field.Id, PackageEditorTexts.ReadBodyTip, row));
            }
        }

        // Kopf-Felder (ohne Punkt) und Felder der Ebenen; ein Kopf-Feld braucht im Muster keine Gruppe number
        bool IsText(FieldNode f) => f.Type is FieldType.Text or FieldType.MultilineText;
        var multilingual = document.Head.Items.Where(f => IsText(f) && f.Multilingual).Select(f => (f.Id, f.Id)).Concat(LevelFields(document, f => IsText(f) && f.Multilingual)).ToList();
        panel.Children.Add(Heading(PackageEditorTexts.ReadLocalizationHeading));
        panel.Children.Add(Note(PackageEditorTexts.ReadLocalizationNote));
        var localizationOutputs = document.Outputs.Where(o => o.Kind == OutputKind.Localization).ToList();
        if (localizationOutputs.Count > 1)
        {
            panel.Children.Add(Row(PackageEditorTexts.ReadLocOutput, PackageEditorTexts.ReadLocOutputTip,
                Combo(new[] { (string.Empty, PackageEditorTexts.ReadLocOutputFirst) }.Concat(localizationOutputs.Select(o => (o.Id, o.Id))).ToList(), read.LocalizationOutput,
                    v => read.LocalizationOutput = v ?? string.Empty, PackageEditorTexts.ReadLocOutputTip)));
        }

        RowList(panel, read.Localization, row =>
        {
            var rows = new List<FrameworkElement>
            {
                Row(PackageEditorTexts.ReadLocKey, PackageEditorTexts.ReadLocKeyTip,
                    PatternBox(row.Get("key"), PackageEditorTexts.ReadLocKeyTip, v => row.Set("key", v),
                        v => row.Get("field").Contains('.') ? PatternCheck.Numbered(v) : PatternCheck.Matching(v), PatternKind.Localization, null)),
                Row(PackageEditorTexts.ReadField, PackageEditorTexts.ReadLocFieldTip, Combo(multilingual, row.Get("field"), v => row.Set("field", v ?? string.Empty), PackageEditorTexts.ReadLocFieldTip)),
            };
            if (localizationOutputs.Count > 1)
            {
                // Eigene Ausgabe je Eintrag: leer = die des Abschnitts
                rows.Add(Row(PackageEditorTexts.ReadLocEntryOutput, PackageEditorTexts.ReadLocEntryOutputTip,
                    Combo(new[] { (string.Empty, PackageEditorTexts.ReadLocEntryOutputDefault) }.Concat(localizationOutputs.Select(o => (o.Id, o.Id))).ToList(), row.Get("output"),
                        v =>
                        {
                            if (string.IsNullOrEmpty(v))
                            {
                                row.Remove("output");
                            }
                            else
                            {
                                row.Set("output", v);
                            }
                        },
                        PackageEditorTexts.ReadLocEntryOutputTip)));
            }

            return rows.ToArray();
        });

        var valueFields = document.Head.Items.Where(IsText).Select(f => (f.Id, f.Id)).Concat(LevelFields(document, IsText)).ToList();
        panel.Children.Add(Heading(PackageEditorTexts.ReadValuesHeading));
        panel.Children.Add(Note(PackageEditorTexts.ReadValuesNote));
        RowList(panel, read.Values, row => new[]
        {
            Row(PackageEditorTexts.ReadElementsFile, PackageEditorTexts.ReadValueFileTip,
                PatternBox(row.Get("file"), PackageEditorTexts.ReadValueFileTip, v => row.Set("file", v), PatternCheck.Path, PatternKind.File, null)),
            Row(PackageEditorTexts.ReadElementsBlock, PackageEditorTexts.ReadValueBlockTip,
                PatternBox(row.Get("block"), PackageEditorTexts.ReadValueBlockTip, v => row.Set("block", v), PatternCheck.Matching, PatternKind.Block, () => row.Get("file"))),
            Row(PackageEditorTexts.ReadValuePattern, PackageEditorTexts.ReadValuePatternTip,
                CodeChecked(row.Get("pattern"), PackageEditorTexts.ReadValuePatternTip, v => row.Set("pattern", v),
                    v => v.Length == 0 && row.Get("body").Trim().Length > 0 ? null : PatternCheck.Value(v))),
            Row(PackageEditorTexts.ReadValueBody, PackageEditorTexts.ReadValueBodyTip,
                Code(row.Get("body"), PackageEditorTexts.ReadValueBodyTip, v => row.Set("body", v))),
            Row(PackageEditorTexts.ReadField, PackageEditorTexts.ReadValueFieldTip, Combo(valueFields, row.Get("field"), v => row.Set("field", v ?? string.Empty), PackageEditorTexts.ReadValueFieldTip)),
            Row(PackageEditorTexts.ReadValueJoin, PackageEditorTexts.ReadValueJoinTip,
                Text(row.Get("join").Replace("\n", "\\n"), PackageEditorTexts.ReadValueJoinTip, v => row.Set("join", v.Replace("\\n", "\n")))),
        });

        var bools = document.Head.Items.Where(f => f.Type == FieldType.Bool).Select(f => (f.Id, f.Id)).Concat(LevelFields(document, f => f.Type == FieldType.Bool)).ToList();
        panel.Children.Add(Heading(PackageEditorTexts.ReadFlagsHeading));
        RowList(panel, read.Flags, row => new[]
        {
            Row(PackageEditorTexts.ReadElementsFile, PackageEditorTexts.ReadFlagFileTip,
                PatternBox(row.Get("file"), PackageEditorTexts.ReadFlagFileTip, v => row.Set("file", v), PatternCheck.Path, PatternKind.File, null)),
            Row(PackageEditorTexts.ReadElementsBlock, PackageEditorTexts.ReadFlagBlockTip,
                PatternBox(row.Get("block"), PackageEditorTexts.ReadFlagBlockTip, v => row.Set("block", v), PatternCheck.Numbered, PatternKind.Block, () => row.Get("file"))),
            Row(PackageEditorTexts.ReadContains, PackageEditorTexts.ReadContainsTip,
                Text(row.Get("contains"), PackageEditorTexts.ReadContainsTip, v => row.Set("contains", v),
                    v => v.Length == 0 && row.Get("pattern").Trim().Length == 0 ? GeneratorsTexts.PatternEmpty : null)),
            Row(PackageEditorTexts.ReadFlagPattern, PackageEditorTexts.ReadFlagPatternTip,
                CodeChecked(row.Get("pattern"), PackageEditorTexts.ReadFlagPatternTip, v => row.Set("pattern", v), v => v.Length == 0 ? null : PatternCheck.Matching(v))),
            Row(PackageEditorTexts.ReadField, PackageEditorTexts.ReadFlagFieldTip, Combo(bools, row.Get("field"), v => row.Set("field", v ?? string.Empty), PackageEditorTexts.ReadFlagFieldTip)),
        });

        var images = document.Head.Items.Where(f => f.Type == FieldType.Image).Select(f => (f.Id, f.Id)).ToList();
        panel.Children.Add(Heading(PackageEditorTexts.ReadAssetsHeading));
        RowList(panel, read.Assets, row => new[]
        {
            Row(PackageEditorTexts.ReadField, PackageEditorTexts.ReadAssetFieldTip, Combo(images, row.Get("field"), v => row.Set("field", v ?? string.Empty), PackageEditorTexts.ReadAssetFieldTip)),
            Row(PackageEditorTexts.ReadElementsFile, PackageEditorTexts.ReadAssetFileTip,
                PatternBox(row.Get("file"), PackageEditorTexts.ReadAssetFileTip, v => row.Set("file", v), PatternCheck.Path, PatternKind.File, null)),
        });

        panel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 6) });
        panel.Children.Add(Tool(ButtonRow((PackageEditorTexts.Probe, PackageEditorTexts.ProbeTip, RunProbe))));
        return panel;
    }

    /// <summary>Zeilen einer <c>read</c>-Liste, jede in einem Rahmen mit Entfernen/Hoch/Runter, darunter "Zeile hinzufügen".</summary>
    private void RowList(StackPanel panel, ReadRowList list, Func<ReadRow, FrameworkElement[]> inputs)
    {
        var items = list.Items;
        for (var i = 0; i < items.Count; i++)
        {
            var index = i;
            var box = new StackPanel();
            foreach (var input in inputs(items[i]))
            {
                box.Children.Add(input);
            }

            box.Children.Add(ButtonRow(
                (PackageEditorTexts.Remove, PackageEditorTexts.ReadRowTip, () => { list.Remove(index); ShowMask(); }),
                (PackageEditorTexts.Up, PackageEditorTexts.MoveTip, () => { list.Move(index, -1); ShowMask(); }),
                (PackageEditorTexts.Down, PackageEditorTexts.MoveTip, () => { list.Move(index, 1); ShowMask(); })));
            var border = new Border { Child = box, BorderThickness = new Thickness(1), Padding = new Thickness(6), Margin = new Thickness(0, 2, 0, 4) };
            border.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
            panel.Children.Add(border);
        }

        panel.Children.Add(ButtonRow((PackageEditorTexts.ReadAddRow, PackageEditorTexts.ReadAddRowTip, () => { list.Add(); ShowMask(); })));
    }

    /// <summary>Felder aller Ebenen, die <paramref name="fits"/> erfüllen, als (Ebene.Feld, Ebene.Feld).</summary>
    private static List<(string Value, string Label)> LevelFields(PackageDocument document, Func<FieldNode, bool> fits) =>
        document.Levels.SelectMany(l => l.Fields.Items.Where(fits).Select(f => l.Id + "." + f.Id)).Select(v => (v, v)).ToList();

    /// <summary>Musterfeld mit Sofort-Prüfung und Knopf "Vorschlag"; <paramref name="file"/> nennt bei Block-Mustern die Datei-Vorlage.</summary>
    private FrameworkElement PatternBox(string value, string tip, Action<string> set, Func<string, string?> check, PatternKind kind, Func<string>? file)
    {
        var box = Text(value, tip, set, check);
        box.FontFamily = new System.Windows.Media.FontFamily("Consolas");
        var suggest = new Button { Content = new TextBlock { Text = PackageEditorTexts.Suggest }, ToolTip = PackageEditorTexts.SuggestTip, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0), Tag = ToolTag };
        suggest.Click += (_, _) =>
        {
            if (Suggest(kind, file?.Invoke()) is not { } pattern)
            {
                return;
            }

            if (ReadOnly)
            {
                // Mitgeliefertes Paket: das Feld bleibt gesperrt, der Vorschlag nur zum Kopieren
                TextPromptWindow.Ask(this, PackageEditorTexts.Suggest, PackageEditorTexts.SuggestReadOnly, pattern);
                return;
            }

            box.Text = pattern;
        };
        var row = new DockPanel();
        DockPanel.SetDock(suggest, Dock.Right);
        row.Children.Add(suggest);
        row.Children.Add(box);
        return row;
    }

    private string? Suggest(PatternKind kind, string? fileTemplate)
    {
        if (ChooseExampleMod() is not { } mod)
        {
            return null;
        }

        var document = _document!;
        var keyField = document.Head.Items.FirstOrDefault(f => f.Id == document.KeyField);
        var keyValue = ReadProbe.Keys(document, _files!, _pending, mod, _entry?.DataFolder).Keys.FirstOrDefault() ?? string.Empty;
        // Datei-Vorlage mit dem Schlüssel einsetzen, damit der Dialog gleich die Blöcke dieser Datei zeigt.
        var file = fileTemplate is null ? null : Regex.Replace(fileTemplate, @"\{\{.*?\}\}", keyValue);
        return PatternSuggestDialog.Ask(this, kind, mod, keyValue, document.KeyField, keyField?.Pattern, file);
    }

    /// <summary>Mod für Beispiele und Probelauf: Liste mit Suchfeld, die aktive Mod oben; die Wahl bleibt bis zum Schließen des Fensters.</summary>
    private string? ChooseExampleMod()
    {
        var mods = _host.KnownMods().ToList();
        if (mods.Count == 0)
        {
            MessageBox.Show(this, PackageEditorTexts.NoMods, PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        var active = _host.ActiveModRoot();
        var preferred = _exampleMod ?? active;
        var ordered = mods.OrderByDescending(m => string.Equals(m.Path, preferred, StringComparison.OrdinalIgnoreCase)).ToList();
        var labels = ordered.Select(m => string.Equals(m.Path, active, StringComparison.OrdinalIgnoreCase) ? PackageEditorTexts.ActiveModMark(m.Name) : m.Name).ToList();
        if (PickListDialog.Ask(this, PackageEditorTexts.ChooseMod, PackageEditorTexts.ChooseModMessage, labels, multi: false) is not [var index])
        {
            return null;
        }

        _exampleMod = ordered[index].Path;
        return _exampleMod;
    }

    private void RunProbe()
    {
        if (_document is null || _files is null || ChooseExampleMod() is not { } mod)
        {
            return;
        }

        var (keys, error) = ReadProbe.Keys(_document, _files, _pending, mod, _entry?.DataFolder);
        if (error is not null || keys.Count == 0)
        {
            MessageBox.Show(this, error ?? PackageEditorTexts.ProbeNoKeys, PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (PickListDialog.Ask(this, PackageEditorTexts.ChooseKey, PackageEditorTexts.ChooseKeyMessage, keys, multi: false) is not [var index])
        {
            return;
        }

        var languages = _host.ProfileLanguages();
        var result = ReadProbe.Run(_document, _files, _pending, mod, keys[index], languages.Count > 0 ? languages : new[] { "english" }, _entry?.DataFolder);
        new ReadProbeWindow(this, keys[index], result).ShowDialog();
    }
}
