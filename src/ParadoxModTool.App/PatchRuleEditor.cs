using System.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ParadoxModTool.Patches;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Baut den Editor der gewählten Regel (Spec, Reiter "Regeln"): gemeinsame Felder plus die Felder der Regelart. Jede Eingabe schreibt
/// sofort in die Regel und ruft <c>changed</c>; Änderungen, die andere Felder ein- oder ausblenden, rufen <c>rebuild</c>.
/// </summary>
public static class PatchRuleEditor
{
    private static readonly string[] Scopes = { PatchRecipeTexts.ScopePerRow, PatchRecipeTexts.ScopeGlobal };
    private static readonly string[] MatchKinds = { PatchRecipeTexts.MatchText, PatchRecipeTexts.MatchRegex };
    private static readonly string[] Actions = { PatchRecipeTexts.ActionComment, PatchRecipeTexts.ActionDelete, PatchRecipeTexts.ActionReplace, PatchRecipeTexts.ActionRemoveBlock };
    private static readonly string[] EditKinds = { PatchRecipeTexts.EditRemove, PatchRecipeTexts.EditInsertInto, PatchRecipeTexts.EditInsertAfter, PatchRecipeTexts.EditAtStart, PatchRecipeTexts.EditAtEnd };

    public static FrameworkElement Build(PatchRule rule, IHighlightingDefinition? highlighting, Action changed, Action rebuild)
    {
        var panel = new Controls.StackPanel();
        panel.Children.Add(Field(PatchRecipeTexts.FieldName, Text(rule.Name, v => rule.Name = v, changed)));
        panel.Children.Add(Check(PatchRecipeTexts.FieldEnabled, rule.Enabled, v => rule.Enabled = v, changed));
        panel.Children.Add(Field(PatchRecipeTexts.FieldScope, Choice(Scopes, (int)rule.Scope, i => rule.Scope = (RuleScope)i, changed)));
        panel.Children.Add(new Controls.Separator { Margin = new Thickness(0, 6, 0, 8) });

        switch (rule)
        {
            case LineRule line:
                panel.Children.Add(Field(PatchRecipeTexts.FieldFiles, WithTip(Text(line.Files, v => line.Files = v, changed), PatchRecipeTexts.FilesTip)));
                panel.Children.Add(Field(PatchRecipeTexts.FieldMatch, Text(line.Match, v => line.Match = v, changed)));
                panel.Children.Add(Field(PatchRecipeTexts.FieldMatchKind, Choice(MatchKinds, (int)line.MatchKind, i => line.MatchKind = (MatchKind)i, changed)));
                panel.Children.Add(Field(PatchRecipeTexts.FieldAction, Choice(Actions, (int)line.Action, i => line.Action = (LineAction)i, () => { changed(); rebuild(); })));
                if (line.Action == LineAction.Replace)
                {
                    panel.Children.Add(Field(PatchRecipeTexts.FieldReplacement, Text(line.Replacement, v => line.Replacement = v, changed)));
                }

                if (line.Action == LineAction.RemoveBlock)
                {
                    panel.Children.Add(Check(PatchRecipeTexts.FollowNext, line.FollowNext, v => line.FollowNext = v, changed));
                }

                panel.Children.Add(Check(PatchRecipeTexts.CreateCopy, line.CreateCopy, v => line.CreateCopy = v, changed));
                var vanillaOnly = Check(PatchRecipeTexts.VanillaPathsOnly, line.VanillaPathsOnly, v => line.VanillaPathsOnly = v, changed);
                vanillaOnly.ToolTip = PatchRecipeTexts.VanillaPathsOnlyTip;
                panel.Children.Add(vanillaOnly);
                break;

            case BlockCopyRule copy:
                panel.Children.Add(Field(PatchRecipeTexts.FieldSource, WithTip(Text(copy.SourceFiles, v => copy.SourceFiles = v, changed), PatchRecipeTexts.FilesTip)));
                panel.Children.Add(Field(PatchRecipeTexts.FieldBlock, Text(copy.BlockKey, v => copy.BlockKey = v, changed)));
                panel.Children.Add(Check(PatchRecipeTexts.FollowNext, copy.FollowNext, v => copy.FollowNext = v, changed));
                panel.Children.Add(Field(PatchRecipeTexts.FieldTarget, Text(copy.Target, v => copy.Target = v, changed)));
                var adopt = Check(PatchRecipeTexts.AdoptExisting, copy.AdoptExisting, v => copy.AdoptExisting = v, changed);
                adopt.ToolTip = PatchRecipeTexts.AdoptExistingTip;
                panel.Children.Add(adopt);
                panel.Children.Add(new Controls.TextBlock { Text = PatchRecipeTexts.Edits, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) });
                foreach (var edit in copy.Edits.ToList())
                {
                    panel.Children.Add(EditBox(copy.Edits, edit, highlighting, changed, rebuild));
                }

                panel.Children.Add(Button(PatchRecipeTexts.AddEdit, () =>
                {
                    copy.Edits.Add(new BlockEdit { Kind = BlockEditKind.InsertInto });
                    changed();
                    rebuild();
                }));
                break;

            case BlockEditRule blockEdit:
                panel.Children.Add(Field(PatchRecipeTexts.FieldFiles, WithTip(Text(blockEdit.Files, v => blockEdit.Files = v, changed), PatchRecipeTexts.FilesTip)));
                panel.Children.Add(Field(PatchRecipeTexts.FieldBlock, Text(blockEdit.BlockKey, v => blockEdit.BlockKey = v, changed)));
                panel.Children.Add(Check(PatchRecipeTexts.FollowNext, blockEdit.FollowNext, v => blockEdit.FollowNext = v, changed));
                var blockEditCopy = Check(PatchRecipeTexts.CreateCopy, blockEdit.CreateCopy, v => blockEdit.CreateCopy = v, changed);
                blockEditCopy.ToolTip = PatchRecipeTexts.BlockEditCopyTip;
                panel.Children.Add(blockEditCopy);
                panel.Children.Add(new Controls.TextBlock { Text = PatchRecipeTexts.Edits, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) });
                foreach (var edit in blockEdit.Edits.ToList())
                {
                    panel.Children.Add(EditBox(blockEdit.Edits, edit, highlighting, changed, rebuild));
                }

                panel.Children.Add(Button(PatchRecipeTexts.AddEdit, () =>
                {
                    blockEdit.Edits.Add(new BlockEdit { Kind = BlockEditKind.InsertInto });
                    changed();
                    rebuild();
                }));
                break;

            case TemplateRule template:
                panel.Children.Add(Field(PatchRecipeTexts.FieldTarget, Text(template.Target, v => template.Target = v, changed)));
                panel.Children.Add(Field(PatchRecipeTexts.FieldInside, Text(template.Inside ?? string.Empty, v => template.Inside = v.Length == 0 ? null : v, changed)));
                panel.Children.Add(new Controls.TextBlock { Text = PatchRecipeTexts.FieldText, Margin = new Thickness(0, 4, 0, 2) });
                panel.Children.Add(Editor(template.Text, v => template.Text = v, changed, highlighting, 220));
                break;

            case ListEntryRule list:
                panel.Children.Add(Field(PatchRecipeTexts.FieldTarget, Text(list.Target, v => list.Target = v, changed)));
                panel.Children.Add(Field(PatchRecipeTexts.FieldListPath, Text(list.ListPath, v => list.ListPath = v, changed)));
                panel.Children.Add(Field(PatchRecipeTexts.FieldEntry, Text(list.Entry, v => list.Entry = v, changed)));
                panel.Children.Add(Check(PatchRecipeTexts.CreateCopy, list.CreateCopy, v => list.CreateCopy = v, changed));
                break;
        }

        return panel;
    }

    /// <summary>Eine Bearbeitung der Block-Kopie: Art, Schlüssel, Wert (nur Entfernen) oder Text (sonst), Entfernen-Knopf.</summary>
    private static FrameworkElement EditBox(List<BlockEdit> edits, BlockEdit edit, IHighlightingDefinition? highlighting, Action changed, Action rebuild)
    {
        var box = new Controls.StackPanel();
        var head = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var remove = Button(PatchRecipeTexts.EditDelete, () =>
        {
            edits.Remove(edit);
            changed();
            rebuild();
        });
        Controls.DockPanel.SetDock(remove, Controls.Dock.Right);
        head.Children.Add(remove);
        var enabled = Check(PatchRecipeTexts.FieldEnabled, edit.Enabled, v => edit.Enabled = v, changed);
        enabled.ToolTip = PatchRecipeTexts.EditEnabledTip;
        Controls.DockPanel.SetDock(enabled, Controls.Dock.Right);
        enabled.Margin = new Thickness(0, 0, 8, 0);
        head.Children.Add(enabled);
        head.Children.Add(Choice(EditKinds, (int)edit.Kind, i => edit.Kind = (BlockEditKind)i, () => { changed(); rebuild(); }));
        box.Children.Add(head);
        if (edit.Kind is not BlockEditKind.InsertAtStart and not BlockEditKind.InsertAtEnd)
        {
            box.Children.Add(Field(PatchRecipeTexts.FieldKey, Text(edit.Key, v => edit.Key = v, changed)));
        }

        if (edit.Kind == BlockEditKind.Remove)
        {
            box.Children.Add(Field(PatchRecipeTexts.FieldValue, Text(edit.Value ?? string.Empty, v => edit.Value = v.Length == 0 ? null : v, changed)));
        }
        else
        {
            box.Children.Add(Editor(edit.Text, v => edit.Text = v, changed, highlighting, 80));
        }

        var border = new Controls.Border { BorderThickness = new Thickness(1), Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 6), Child = box };
        border.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.ControlBorder");
        return border;
    }

    private static Controls.TextBox Text(string value, Action<string> set, Action changed)
    {
        var box = new Controls.TextBox { Text = value };
        box.TextChanged += (_, _) =>
        {
            set(box.Text);
            changed();
        };
        return box;
    }

    private static Controls.TextBox WithTip(Controls.TextBox box, string tip)
    {
        box.ToolTip = tip;
        return box;
    }

    private static Controls.CheckBox Check(string label, bool value, Action<bool> set, Action changed)
    {
        var box = new Controls.CheckBox { Content = new Controls.TextBlock { Text = label }, IsChecked = value, Margin = new Thickness(0, 2, 0, 6) };
        box.Click += (_, _) =>
        {
            set(box.IsChecked == true);
            changed();
        };
        return box;
    }

    private static Controls.ComboBox Choice(string[] items, int selected, Action<int> set, Action changed)
    {
        var box = new Controls.ComboBox { ItemsSource = items, SelectedIndex = Math.Clamp(selected, 0, items.Length - 1), MinWidth = 160, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
        box.SelectionChanged += (_, _) =>
        {
            set(Math.Max(0, box.SelectedIndex));
            changed();
        };
        return box;
    }

    private static TextEditor Editor(string value, Action<string> set, Action changed, IHighlightingDefinition? highlighting, double height)
    {
        var editor = new TextEditor
        {
            Text = value,
            SyntaxHighlighting = highlighting,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            Height = height,
            ShowLineNumbers = true,
            HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(1),
        };
        editor.SetResourceReference(Controls.Control.BackgroundProperty, "Theme.EditorBackground");
        editor.SetResourceReference(Controls.Control.ForegroundProperty, "Theme.EditorForeground");
        editor.SetResourceReference(Controls.Control.BorderBrushProperty, "Theme.ControlBorder");
        editor.SetResourceReference(TextEditor.LineNumbersForegroundProperty, "Theme.LineNumbers");
        editor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Theme.EditorSelection");
        editor.TextChanged += (_, _) =>
        {
            set(editor.Text);
            changed();
        };
        return editor;
    }

    private static Controls.Button Button(string text, Action click)
    {
        var button = new Controls.Button { Content = new Controls.TextBlock { Text = text }, Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6) };
        button.Click += (_, _) => click();
        return button;
    }

    private static Controls.DockPanel Field(string label, UIElement input)
    {
        var panel = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var text = new Controls.TextBlock { Text = label, Width = 140, VerticalAlignment = VerticalAlignment.Center };
        Controls.DockPanel.SetDock(text, Controls.Dock.Left);
        panel.Children.Add(text);
        panel.Children.Add(input);
        return panel;
    }
}
