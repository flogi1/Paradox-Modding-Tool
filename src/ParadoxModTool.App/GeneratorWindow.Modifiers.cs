using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Editor;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Editing;
using ParadoxModTool.Modifiers;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Modifikatorfelder (Spec 2026-10-05-generator-packages-lookups, Abschnitt 2): Knopf "+ Modifier" mit Suche, Chips mit den Namen wie im Spiel und ✕ zum
/// Entfernen, Kopieren auf höhere Stufen mit Skalierung und Löschen über alle Stufen; die drei Schalter gelten je Spiel.
/// </summary>
public partial class GeneratorWindow
{
    private bool _modifierNamesSubscribed;

    /// <summary>Feld, Knopf "+ Modifier" und darunter die Chips.</summary>
    private FrameworkElement ModifierField(FieldDef field, FormNode node, TextBox box)
    {
        var chips = new WrapPanel { MaxWidth = box.Width + 140, Margin = new Thickness(0, 2, 0, 0) };
        void Refresh() => FillChips(chips, field, node);
        box.TextChanged += (_, _) => Refresh();
        _modifierBoxes.Add((node, field.Id, box, Refresh));
        Refresh();
        EnsureModifierNamesSubscription();

        var add = new Button { Content = new TextBlock { Text = GeneratorTexts.ModifierAdd }, ToolTip = GeneratorTexts.ModifierAddTip, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        add.Click += (_, _) => new ModifierSearchWindow(this, key => InsertModifier(field, node, key)).Show();
        var top = new StackPanel { Orientation = Orientation.Horizontal };
        top.Children.Add(box);
        top.Children.Add(add);
        var panel = new StackPanel();
        panel.Children.Add(top);
        panel.Children.Add(chips);
        return panel;
    }

    /// <summary>Fügt den Modifier mit Startwert ein und kopiert ihn, wenn eingeschaltet, in die höheren Stufen; <see langword="false"/>, wenn er schon im Feld steht.</summary>
    private bool InsertModifier(FieldDef field, FormNode node, string key)
    {
        var text = node.Values.GetValueOrDefault(field.Id) ?? string.Empty;
        if (ModifierLines.HasKey(text, key))
        {
            return false;
        }

        var traits = ModifierValueSettings.Index.Traits.GetValueOrDefault(key);
        var value = ModifierLines.StartValue(traits?.Boolean ?? false, traits?.Bad ?? false);
        SetModifierText(node, field.Id, ModifierLines.Insert(text, key, value));
        var session = _host.Session();
        if (session.GeneratorCopyModifiers && _levelOf.TryGetValue(node, out var level))
        {
            var texts = level.Siblings.Select(n => n.Values.GetValueOrDefault(field.Id) ?? string.Empty).ToList();
            var result = ModifierLines.CopyToHigherLevels(texts, level.Index + 1, key, value, session.GeneratorScaleModifiers);
            for (var i = 0; i < result.Count; i++)
            {
                if (result[i] != texts[i])
                {
                    SetModifierText(level.Siblings[i], field.Id, result[i]);
                }
            }
        }

        return true;
    }

    /// <summary>✕ an einem Chip: Schlüssel aus dem Feld entfernen, mit "Löschen übertragen" auch aus den anderen Stufen.</summary>
    private void RemoveModifier(FieldDef field, FormNode node, string key)
    {
        var targets = _host.Session().GeneratorRemoveAcrossLevels && _levelOf.TryGetValue(node, out var level) ? level.Siblings : new List<FormNode> { node };
        foreach (var target in targets)
        {
            var text = target.Values.GetValueOrDefault(field.Id) ?? string.Empty;
            if (ModifierLines.HasKey(text, key))
            {
                SetModifierText(target, field.Id, ModifierLines.Remove(text, key));
            }
        }
    }

    /// <summary>Schreibt den Text in den Arbeitsstand und ins sichtbare Feld (das Feld aktualisiert dabei seine Chips).</summary>
    private void SetModifierText(FormNode node, string fieldId, string text)
    {
        node.Values[fieldId] = text;
        var shown = _modifierBoxes.FirstOrDefault(b => ReferenceEquals(b.Node, node) && b.FieldId == fieldId);
        if (shown.Box is not null)
        {
            shown.Box.Text = text;
        }

        MarkDirty();
    }

    private void FillChips(WrapPanel chips, FieldDef field, FormNode node)
    {
        chips.Children.Clear();
        var index = ModifierValueSettings.Index;
        var names = ModifierValueSettings.Names;
        foreach (var line in (node.Values.GetValueOrDefault(field.Id) ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            if (ModifierChipText.For(line, index, names) is not { } chip)
            {
                continue;
            }

            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var text = new TextBlock { Text = chip.Text, VerticalAlignment = VerticalAlignment.Center };
            if (!chip.Known)
            {
                text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
            }

            content.Children.Add(text);
            if (chip.Key.Length > 0)
            {
                var remove = new Button { Content = new TextBlock { Text = "✕" }, ToolTip = GeneratorTexts.ModifierRemoveTip, Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(4, 0, 0, 0), BorderThickness = new Thickness(0) };
                remove.SetResourceReference(BackgroundProperty, "Theme.PanelBackground");
                var key = chip.Key;
                remove.Click += (_, _) => RemoveModifier(field, node, key);
                content.Children.Add(remove);
            }

            var border = new Border { Child = content, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(5, 1, 3, 1), Margin = new Thickness(0, 0, 4, 3) };
            border.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
            border.SetResourceReference(Border.BackgroundProperty, "Theme.PanelBackground");
            chips.Children.Add(border);
        }
    }

    /// <summary>Namen kommen nach dem Index; dann alle Chips neu zeichnen.</summary>
    private void EnsureModifierNamesSubscription()
    {
        if (_modifierNamesSubscribed)
        {
            return;
        }

        _modifierNamesSubscribed = true;
        void OnChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
        {
            foreach (var entry in _modifierBoxes)
            {
                entry.RefreshChips();
            }
        });
        ModifierValueSettings.Changed += OnChanged;
        Closed += (_, _) => ModifierValueSettings.Changed -= OnChanged;
    }

    /// <summary>Die drei Schalter für Modifikatorfelder; nur, wenn das Paket solche Felder hat.</summary>
    private UIElement? ModifierSwitchRow()
    {
        var hasModifiers = false;
        for (var level = _package!.Root; level is not null; level = level.Child)
        {
            hasModifiers |= level.Fields.Any(f => f.Type == FieldType.Modifiers);
        }

        if (!hasModifiers)
        {
            return null;
        }

        var session = _host.Session();
        var row = new WrapPanel { Margin = new Thickness(LabelWidth, 4, 0, 0) };
        var scale = SwitchBox(GeneratorTexts.SwitchScale, GeneratorTexts.SwitchScaleTip, session.GeneratorScaleModifiers, v => session.GeneratorScaleModifiers = v);
        scale.IsEnabled = session.GeneratorCopyModifiers;
        var copy = SwitchBox(GeneratorTexts.SwitchCopy, GeneratorTexts.SwitchCopyTip, session.GeneratorCopyModifiers, v =>
        {
            session.GeneratorCopyModifiers = v;
            scale.IsEnabled = v;
        });
        row.Children.Add(copy);
        row.Children.Add(scale);
        row.Children.Add(SwitchBox(GeneratorTexts.SwitchRemoveAll, GeneratorTexts.SwitchRemoveAllTip, session.GeneratorRemoveAcrossLevels, v => session.GeneratorRemoveAcrossLevels = v));
        return row;
    }

    private static CheckBox SwitchBox(string text, string tip, bool value, Action<bool> set)
    {
        var box = new CheckBox { Content = new TextBlock { Text = text }, IsChecked = value, ToolTip = tip, Margin = new Thickness(0, 0, 14, 0) };
        ToolTipService.SetShowOnDisabled(box, true);
        box.Click += (_, _) => set(box.IsChecked == true);
        return box;
    }
}
