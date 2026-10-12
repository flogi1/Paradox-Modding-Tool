using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators.Editing.Package;
using TreeViewItem = System.Windows.Controls.TreeViewItem;

namespace ParadoxModTool.App;

/// <summary>Abschnittsbaum des Paket-Editors und Zuordnung Abschnitt → Maske und Anleitungskapitel.</summary>
public sealed partial class PackageEditorWindow
{
    internal enum SectionKind
    {
        Basics,
        Head,
        Levels,
        Level,
        Constants,
        Languages,
        Assets,
        Externals,
        Outputs,
        Templates,
        Messages,
        AfterGenerate,
        Read,
        Lookups,
        Candidates,
    }

    /// <summary>Ein Knoten im Abschnittsbaum; <see cref="Payload"/> z. B. die <see cref="LevelNode"/> einer Ebene.</summary>
    internal sealed record SectionNode(SectionKind Kind, object? Payload = null);

    private SectionNode? CurrentSection => (_tree.SelectedItem as TreeViewItem)?.Tag as SectionNode;

    /// <summary>Baut den Baum neu; wählt den Knoten mit <paramref name="select"/> (Art und gleiche Nutzlast), sonst Grunddaten.</summary>
    private void RebuildTree(SectionNode? select)
    {
        _tree.Items.Clear();
        if (_document is null)
        {
            return;
        }

        TreeViewItem Item(string text, SectionNode node)
        {
            var item = new TreeViewItem { Header = new TextBlock { Text = text }, Tag = node, IsExpanded = true };
            return item;
        }

        _tree.Items.Add(Item(PackageEditorTexts.SectionBasics, new SectionNode(SectionKind.Basics)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionHead, new SectionNode(SectionKind.Head)));
        var levels = Item(PackageEditorTexts.SectionLevels, new SectionNode(SectionKind.Levels));
        foreach (var level in _document.Levels)
        {
            levels.Items.Add(Item(level.Id, new SectionNode(SectionKind.Level, level)));
        }

        _tree.Items.Add(levels);
        _tree.Items.Add(Item(PackageEditorTexts.SectionConstants, new SectionNode(SectionKind.Constants)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionLanguages, new SectionNode(SectionKind.Languages)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionAssets, new SectionNode(SectionKind.Assets)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionExternals, new SectionNode(SectionKind.Externals)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionOutputs, new SectionNode(SectionKind.Outputs)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionTemplates, new SectionNode(SectionKind.Templates)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionMessages, new SectionNode(SectionKind.Messages)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionAfterGenerate, new SectionNode(SectionKind.AfterGenerate)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionRead, new SectionNode(SectionKind.Read)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionLookups, new SectionNode(SectionKind.Lookups)));
        _tree.Items.Add(Item(PackageEditorTexts.SectionCandidates, new SectionNode(SectionKind.Candidates)));

        var target = AllItems().FirstOrDefault(i => i.Tag is SectionNode n && select is not null && n.Kind == select.Kind && Equals(n.Payload, select.Payload))
            ?? AllItems().FirstOrDefault(i => i.Tag is SectionNode n && select is not null && n.Kind == select.Kind)
            ?? (TreeViewItem)_tree.Items[0];
        target.IsSelected = true;
        ShowMask();
    }

    /// <summary>Wählt den ersten Knoten der Art <paramref name="kind"/> im Baum (Sprung aus "Prüfen" oder aus einer Maske).</summary>
    private void SelectSection(SectionKind kind)
    {
        if (AllItems().FirstOrDefault(i => i.Tag is SectionNode n && n.Kind == kind) is { } item)
        {
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

    private IEnumerable<TreeViewItem> AllItems()
    {
        foreach (TreeViewItem item in _tree.Items)
        {
            yield return item;
            foreach (TreeViewItem child in item.Items)
            {
                yield return child;
            }
        }
    }

    /// <summary>Zeigt die Maske des gewählten Knotens; bei mitgelieferten Paketen gesperrt.</summary>
    private void ShowMask()
    {
        if (_document is null || CurrentSection is not { } node)
        {
            return;
        }

        var mask = BuildMask(node);
        if (ReadOnly && node.Kind is SectionKind.Read or SectionKind.Lookups)
        {
            // Probelauf, Vorschlag und Vorschau schreiben nichts: bei mitgelieferten Paketen bleiben sie bedienbar, nur die Eingaben sind gesperrt.
            LockInputs(mask);
        }
        else
        {
            // Vorlagen bleiben bei mitgelieferten Paketen lesbar: der Editor ist dort nur schreibgeschützt, die Knöpfe sind aus.
            mask.IsEnabled = !ReadOnly || node.Kind == SectionKind.Templates;
        }

        _maskHost.Content = mask;
    }

    /// <summary>Kennung der Werkzeuge, die bei mitgelieferten Paketen bedienbar bleiben.</summary>
    private const string ToolTag = "tool";

    private static FrameworkElement Tool(FrameworkElement element)
    {
        element.Tag = ToolTag;
        return element;
    }

    /// <summary>Sperrt Eingaben einer Maske: Textfelder schreibgeschützt, Auswahl, Häkchen und Knöpfe aus; als Werkzeug markierte Teile bleiben bedienbar.</summary>
    private static void LockInputs(DependencyObject root)
    {
        if (root is FrameworkElement { Tag: ToolTag })
        {
            return;
        }

        switch (root)
        {
            case System.Windows.Controls.TextBox text:
                text.IsReadOnly = true;
                return;
            case System.Windows.Controls.ComboBox or System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.DataGrid or LocalizedTextBox:
                ((UIElement)root).IsEnabled = false;
                return;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            LockInputs(child);
        }
    }

    /// <summary>Anleitungskapitel zur offenen Maske (F1).</summary>
    private string? CurrentGuideSection() => CurrentSection?.Kind switch
    {
        SectionKind.Basics => "generatorhelp.basics",
        SectionKind.Head or SectionKind.Levels or SectionKind.Level => "generatorhelp.form",
        SectionKind.Constants => "generatorhelp.constants",
        SectionKind.Languages => "generatorhelp.languages",
        SectionKind.Assets => "generatorhelp.assets",
        SectionKind.Externals or SectionKind.AfterGenerate => "generatorhelp.externals",
        SectionKind.Outputs => "generatorhelp.outputs",
        SectionKind.Templates => "generatorhelp.templates",
        SectionKind.Messages => "generatorhelp.messages",
        SectionKind.Read => "generatorhelp.read",
        SectionKind.Lookups => "generatorhelp.lookups",
        SectionKind.Candidates => "generatorhelp.candidates",
        _ => null,
    };
}
