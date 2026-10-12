using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Editing.Package;
using FontFamily = System.Windows.Media.FontFamily;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>Maske der Ausgaben (Spec Teil 5a, Abschnitt 3): Liste, Knöpfe und die Felder der gewählten Art.</summary>
public sealed partial class PackageEditorWindow
{
    private string? _selectedOutput;

    /// <summary>Hinweis nach einem Artwechsel (entfernte Angaben), einmal nach dem Neuaufbau gezeigt.</summary>
    private string? _outputNote;

    private FrameworkElement OutputsMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionOutputs);
        var outputs = document.Outputs;
        if (outputs.All(o => o.Id != _selectedOutput))
        {
            _selectedOutput = outputs.FirstOrDefault()?.Id;
        }

        var list = new ListBox { MaxHeight = 240, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var output in outputs)
        {
            list.Items.Add(new ListBoxItem { Content = new TextBlock { Text = $"{output.Id}  ·  {output.Kind}  ·  {OutputTarget(output)}" }, Tag = output.Id, IsSelected = output.Id == _selectedOutput });
        }

        list.SelectionChanged += (_, _) =>
        {
            _selectedOutput = (list.SelectedItem as ListBoxItem)?.Tag as string;
            ShowMask();
        };
        panel.Children.Add(list);
        panel.Children.Add(ButtonRow(
            (PackageEditorTexts.New, PackageEditorTexts.NewOutputTip, () =>
            {
                _selectedOutput = document.AddOutput(FreeId(document.Outputs.Select(o => o.Id), "output"), OutputKind.OwnFile).Id;
                ShowMask();
            }),
            (PackageEditorTexts.Duplicate, PackageEditorTexts.DuplicateTip, () =>
            {
                if (_selectedOutput is { } id)
                {
                    _selectedOutput = document.DuplicateOutput(id).Id;
                    ShowMask();
                }
            }),
            (PackageEditorTexts.Remove, PackageEditorTexts.RemoveOutputTip, () =>
            {
                if (_selectedOutput is { } id)
                {
                    document.RemoveOutput(id);
                    _selectedOutput = null;
                    ShowMask();
                }
            }),
            (PackageEditorTexts.Up, PackageEditorTexts.MoveTip, () => MoveOutput(-1)),
            (PackageEditorTexts.Down, PackageEditorTexts.MoveTip, () => MoveOutput(1))));

        if (outputs.FirstOrDefault(o => o.Id == _selectedOutput) is not { } selected)
        {
            panel.Children.Add(Note(PackageEditorTexts.NoOutputs));
            return panel;
        }

        panel.Children.Add(OutputMask(selected, renamed =>
        {
            if (list.SelectedItem is ListBoxItem item)
            {
                item.Tag = renamed.Id;
                item.Content = new TextBlock { Text = $"{renamed.Id}  ·  {renamed.Kind}  ·  {OutputTarget(renamed)}" };
            }
        }));
        return panel;
    }

    private void MoveOutput(int delta)
    {
        if (_selectedOutput is { } id)
        {
            _document!.MoveOutput(id, delta);
            ShowMask();
        }
    }

    private FrameworkElement OutputMask(OutputNode output, Action<OutputNode> renamed)
    {
        var document = _document!;
        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        if (_outputNote is { } note)
        {
            var text = Note(note);
            text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
            panel.Children.Add(text);
            _outputNote = null;
        }

        var oldId = output.Id;
        var id = Text(output.Id, PackageEditorTexts.OutputIdTip, null, value =>
            !PackageDocument.IsValidId(value) ? PackageEditorTexts.IdInvalidName
            : value != oldId && document.Outputs.Any(o => o.Id == value) ? PackageEditorTexts.IdDuplicate
            : null);
        id.LostFocus += (_, _) =>
        {
            var value = id.Text.Trim();
            if (value != oldId && PackageDocument.IsValidId(value) && document.Outputs.All(o => o.Id != value))
            {
                // Nur den Listeneintrag ändern: ein Neuaufbau der Maske verschluckte den Klick, mit dem das Feld verlassen wurde.
                output.Id = value;
                oldId = value;
                _selectedOutput = value;
                renamed(output);
            }
        };
        panel.Children.Add(Row(PackageEditorTexts.FieldId, PackageEditorTexts.OutputIdTip, id));

        var kinds = Enum.GetValues<OutputKind>().Select(k => (k.ToString(), PackageEditorTexts.KindName(k))).ToList();
        panel.Children.Add(Row(PackageEditorTexts.OutputKind, PackageEditorTexts.OutputKindTip, Combo(kinds, output.Kind.ToString(), value =>
        {
            if (value is null || !Enum.TryParse<OutputKind>(value, out var kind) || kind == output.Kind)
            {
                return;
            }

            var keep = OutputNode.KeysOf(kind);
            var dropped = OutputNode.KeysOf(output.Kind).Where(k => !keep.Contains(k) && output.Raw[k] is not null).ToList();
            output.Kind = kind;
            _outputNote = dropped.Count > 0 ? PackageEditorTexts.KindChangeDropped(string.Join(", ", dropped)) : null;
            ShowMask();
        })));

        if (output.Kind == OutputKind.JsonEntry)
        {
            // Der Eintrag geht in eine externe Datei: "path" bleibt leer und hat keine Maske
            panel.Children.Add(Note(PackageEditorTexts.JsonEntryNote));
        }
        else
        {
            var path = Code(output.Path, PackageEditorTexts.OutputPathTip, v => output.Path = v);
            panel.Children.Add(Row(PackageEditorTexts.OutputPath, PackageEditorTexts.OutputPathTip, path));
        }

        if (output.Kind == OutputKind.Localization && !output.Path.Contains("lang", StringComparison.Ordinal))
        {
            var warning = Note(PackageEditorTexts.LangMissing);
            warning.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
            panel.Children.Add(warning);
        }

        foreach (var key in OutputNode.KeysOf(output.Kind))
        {
            switch (key)
            {
                case "template":
                    panel.Children.Add(Row(PackageEditorTexts.OutputTemplate, output.Kind == OutputKind.JsonEntry ? PackageEditorTexts.OutputJsonTemplateTip : PackageEditorTexts.OutputTemplateTip,
                        TemplateChoice(output, key, allowNone: false)));
                    break;
                case "skeleton":
                    panel.Children.Add(Row(PackageEditorTexts.OutputSkeleton, PackageEditorTexts.OutputSkeletonTip, TemplateChoice(output, key, allowNone: true)));
                    break;
                case "block":
                    panel.Children.Add(Row(PackageEditorTexts.OutputBlock, PackageEditorTexts.OutputBlockTip, Code(output.Get(key), PackageEditorTexts.OutputBlockTip, v => output.Set(key, v))));
                    break;
                case "forEach":
                    panel.Children.Add(Row(PackageEditorTexts.OutputForEach, PackageEditorTexts.OutputForEachTip,
                        Combo(new[] { (string.Empty, PackageEditorTexts.None) }.Concat(document.Levels.Select(l => (l.Id, l.Id))).ToList(), output.Get(key) ?? string.Empty,
                            v => output.Set(key, string.IsNullOrEmpty(v) ? null : v))));
                    break;
                case "legacyBegin":
                    panel.Children.Add(Row(PackageEditorTexts.OutputLegacyBegin, PackageEditorTexts.OutputLegacyBeginTip, Code(output.Get(key), PackageEditorTexts.OutputLegacyBeginTip, v => output.Set(key, v))));
                    break;
                case "legacyEnd":
                    panel.Children.Add(Row(PackageEditorTexts.OutputLegacyEnd, PackageEditorTexts.OutputLegacyEndTip, Code(output.Get(key), PackageEditorTexts.OutputLegacyEndTip, v => output.Set(key, v))));
                    break;
                case "legacyPatterns":
                    panel.Children.Add(Row(PackageEditorTexts.OutputLegacyPatterns, PackageEditorTexts.OutputLegacyPatternsTip, CodeLines(output, key, PackageEditorTexts.OutputLegacyPatternsTip)));
                    break;
                case "line":
                    panel.Children.Add(Row(PackageEditorTexts.OutputLine, PackageEditorTexts.OutputLineTip, Code(output.Get(key), PackageEditorTexts.OutputLineTip, v => output.Set(key, v))));
                    break;
                case "keyPattern":
                    panel.Children.Add(Row(PackageEditorTexts.OutputKeyPattern, PackageEditorTexts.OutputKeyPatternTip, Code(output.Get(key), PackageEditorTexts.OutputKeyPatternTip, v => output.Set(key, v))));
                    break;
                case "match":
                    panel.Children.Add(Row(PackageEditorTexts.OutputMatch, PackageEditorTexts.OutputMatchTip,
                        CodeChecked(output.Get(key), PackageEditorTexts.OutputMatchTip, v => output.Set(key, v), PatternCheck.Matching)));
                    break;
                case "insertAfter":
                    panel.Children.Add(Row(PackageEditorTexts.OutputInsertAfter, PackageEditorTexts.OutputInsertAfterTip,
                        CodeChecked(output.Get(key), PackageEditorTexts.OutputInsertAfterTip, v => output.Set(key, string.IsNullOrWhiteSpace(v) ? null : v),
                            v => v.Trim().Length == 0 ? null : PatternCheck.Matching(v))));
                    break;
                case "ifLimit":
                    panel.Children.Add(Row(PackageEditorTexts.OutputIfLimit, PackageEditorTexts.OutputIfLimitTip,
                        CodeText(output.Get(key), PackageEditorTexts.OutputIfLimitTip, v => output.Set(key, string.IsNullOrWhiteSpace(v) ? null : v.Trim()))));
                    break;
                case "external":
                    panel.Children.Add(Row(PackageEditorTexts.OutputExternal, PackageEditorTexts.OutputExternalTip,
                        Combo(document.Externals.Select(e => (e.Id, e.Id)).ToList(), output.Get(key), v => output.Set(key, v), PackageEditorTexts.OutputExternalTip)));
                    break;
                case "jsonPath":
                    panel.Children.Add(Row(PackageEditorTexts.OutputJsonPath, PackageEditorTexts.OutputJsonPathTip, Code(output.Get(key), PackageEditorTexts.OutputJsonPathTip, v => output.Set(key, v))));
                    break;
                case "entryKey":
                    panel.Children.Add(Row(PackageEditorTexts.OutputEntryKey, PackageEditorTexts.OutputEntryKeyTip, Code(output.Get(key), PackageEditorTexts.OutputEntryKeyTip, v => output.Set(key, v))));
                    break;
                case "keys":
                    panel.Children.Add(Row(PackageEditorTexts.OutputKeys, PackageEditorTexts.OutputKeysTip,
                        CodeChecked(output.Get(key), PackageEditorTexts.OutputKeysTip, v => output.Set(key, string.IsNullOrWhiteSpace(v) ? null : v),
                            v => v.Trim().Length == 0 ? null : PatternCheck.Matching(v))));
                    break;
                case "keep":
                    panel.Children.Add(Row(PackageEditorTexts.OutputKeep, PackageEditorTexts.OutputKeepTip, CodeLines(output, key, PackageEditorTexts.OutputKeepTip)));
                    break;
                case "checkUnreferenced":
                    panel.Children.Add(Row(string.Empty, PackageEditorTexts.OutputCheckUnreferencedTip,
                        Check(PackageEditorTexts.OutputCheckUnreferenced, PackageEditorTexts.OutputCheckUnreferencedTip, output.CheckUnreferenced, v => output.CheckUnreferenced = v)));
                    break;
                case "source":
                    panel.Children.Add(Row(PackageEditorTexts.OutputSource, PackageEditorTexts.OutputSourceTip, SourceInput(output)));
                    break;
                case "message":
                {
                    var message = new LocalizedTextBox(PackageEditorTexts.OutputMessage, output.Message, PackageEditorTexts.OutputMessageTip);
                    message.ValueChanged += value => output.Message = value;
                    panel.Children.Add(Row(PackageEditorTexts.OutputMessage, PackageEditorTexts.OutputMessageTip, message));
                    break;
                }
                case "newLine":
                    panel.Children.Add(Row(PackageEditorTexts.OutputNewLine, PackageEditorTexts.OutputNewLineTip,
                        Combo(new List<(string, string)> { (string.Empty, PackageEditorTexts.NewLineAuto), ("lf", "LF"), ("crlf", "CRLF") }, output.Get(key) ?? string.Empty,
                            v => output.Set(key, string.IsNullOrEmpty(v) ? null : v))));
                    break;
            }
        }

        return panel;
    }

    /// <summary>Vorlagenwahl aus <c>templates/</c> (auch noch ungespeicherte) mit Knopf "Öffnen".</summary>
    private FrameworkElement TemplateChoice(OutputNode output, string key, bool allowNone)
    {
        var names = TemplateNames().Select(n => (n, n)).ToList();
        if (allowNone)
        {
            names.Insert(0, (string.Empty, PackageEditorTexts.None));
        }

        var row = new DockPanel();
        var open = ButtonRow((PackageEditorTexts.Open, PackageEditorTexts.OpenTemplateTip, () =>
        {
            if (output.Get(key) is { Length: > 0 } name)
            {
                ShowTemplate(name, null);
            }
        }));
        open.Margin = new Thickness(6, 0, 0, 0);
        DockPanel.SetDock(open, Dock.Right);
        row.Children.Add(open);
        row.Children.Add(Combo(names, output.Get(key) ?? string.Empty, v => output.Set(key, string.IsNullOrEmpty(v) ? null : v)));
        return row;
    }

    /// <summary><c>copyBinary</c>: Asset (Auswahl) und Eintrag (Text) werden zu <c>asset:eintrag</c>.</summary>
    private FrameworkElement SourceInput(OutputNode output)
    {
        var source = output.Get("source") ?? string.Empty;
        var colon = source.IndexOf(':');
        var asset = colon < 0 ? source : source[..colon];
        var entry = colon < 0 ? string.Empty : source[(colon + 1)..];
        var row = new DockPanel();
        TextBox? entryBox = null;
        var assets = Combo(_document!.Assets.Select(a => (a.Id, a.Id)).ToList(), asset, v =>
        {
            asset = v ?? string.Empty;
            output.Set("source", asset + ":" + (entryBox?.Text.Trim() ?? entry));
        }, PackageEditorTexts.FieldAssetTip);
        assets.Width = 160;
        DockPanel.SetDock(assets, Dock.Left);
        row.Children.Add(assets);
        entryBox = Code(entry, PackageEditorTexts.OutputSourceEntryTip, v => output.Set("source", asset + ":" + v));
        entryBox.Margin = new Thickness(6, 0, 0, 0);
        row.Children.Add(entryBox);
        return row;
    }

    /// <summary>Code-Feld mit Sofort-Prüfung (Treffer- und Schlüssel-Muster).</summary>
    private TextBox CodeChecked(string? value, string tip, Action<string> set, Func<string, string?> check)
    {
        var box = Text(value ?? string.Empty, tip, set, check);
        box.FontFamily = new FontFamily("Consolas");
        return box;
    }

    /// <summary>Wohin die Ausgabe schreibt, für die Liste: der Pfad, bei "jsonEntry" die externe Datei.</summary>
    private static string OutputTarget(OutputNode output) => output.Kind == OutputKind.JsonEntry ? "extern/" + output.Get("external") : output.Path;

    /// <summary>Mehrzeiliges Code-Feld ohne Prüfung (Bedingung, die über mehrere Zeilen geht).</summary>
    private TextBox CodeText(string? value, string tip, Action<string> set)
    {
        var box = Text(value ?? string.Empty, tip, set, multiline: true);
        box.FontFamily = new FontFamily("Consolas");
        return box;
    }

    private TextBox Code(string? value, string tip, Action<string> set)
    {
        var box = Text(value ?? string.Empty, tip, set);
        box.FontFamily = new FontFamily("Consolas");
        return box;
    }

    private TextBox CodeLines(OutputNode output, string key, string tip)
    {
        var box = Text(string.Join("\n", output.GetList(key)), tip, v => output.SetList(key, Lines(v)), multiline: true);
        box.FontFamily = new FontFamily("Consolas");
        return box;
    }
}
