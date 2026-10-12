using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators.Editing.Package;

namespace ParadoxModTool.App;

/// <summary>Maske "Modifier ohne Idee" (<c>candidates</c>) des Paket-Editors: Kategorie, Verwendungen, Gruppen und Angaben zum Anlegen.</summary>
public sealed partial class PackageEditorWindow
{
    private FrameworkElement CandidatesMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionCandidates);
        panel.Children.Add(Note(PackageEditorTexts.CandidatesNote));
        if (document.Candidates is not { } start)
        {
            panel.Children.Add(ButtonRow((PackageEditorTexts.CandidatesAdd, PackageEditorTexts.CandidatesAddTip, () =>
            {
                document.Candidates = new CandidatesDoc(string.Empty, Array.Empty<(string, string, string)>(), Array.Empty<(string, LocalizedText, string, bool)>(), string.Empty, string.Empty, string.Empty);
                ShowMask();
            })));
            return panel;
        }

        var category = start.Category;
        var used = start.Used.ToList();
        var groups = start.Groups.ToList();
        var ask = start.Ask;
        var name = start.Name;
        var modifiers = start.Modifiers;

        void Write()
        {
            // Nicht schreiben, solange eine Gruppen-Kennung ungültig oder doppelt ist; bis dahin bleibt der letzte gute Stand im Paket.
            var ids = groups.Select(g => g.Id).ToList();
            if (ids.Select((id, i) => PackageDocument.IdIssueOf(ids, i, id)).All(issue => issue is null))
            {
                document.Candidates = new CandidatesDoc(category, used.ToList(), groups.ToList(), ask, name, modifiers);
            }
        }

        panel.Children.Add(Row(PackageEditorTexts.CandidatesCategory, PackageEditorTexts.CandidatesCategoryTip,
            Text(category, PackageEditorTexts.CandidatesCategoryTip, v =>
            {
                category = v;
                Write();
            })));

        // Verwendungen
        panel.Children.Add(Heading(PackageEditorTexts.CandidatesUsedHeading));
        panel.Children.Add(Note(PackageEditorTexts.CandidatesUsedNote));
        for (var i = 0; i < used.Count; i++)
        {
            var index = i;
            var entry = new StackPanel();
            entry.Children.Add(Row(PackageEditorTexts.CandidatesUsedFile, PackageEditorTexts.CandidatesUsedFileTip, Text(used[index].File, PackageEditorTexts.CandidatesUsedFileTip, v =>
            {
                used[index] = (v, used[index].Block, used[index].Except);
                Write();
            }, PatternProblem)));
            entry.Children.Add(Row(PackageEditorTexts.CandidatesUsedBlock, PackageEditorTexts.CandidatesUsedBlockTip, Text(used[index].Block, PackageEditorTexts.CandidatesUsedBlockTip, v =>
            {
                used[index] = (used[index].File, v, used[index].Except);
                Write();
            }, PatternProblem)));
            entry.Children.Add(Row(PackageEditorTexts.CandidatesUsedExcept, PackageEditorTexts.CandidatesUsedExceptTip, Text(used[index].Except, PackageEditorTexts.CandidatesUsedExceptTip, v =>
            {
                used[index] = (used[index].File, used[index].Block, v);
                Write();
            })));
            entry.Children.Add(ButtonRow((PackageEditorTexts.Remove, PackageEditorTexts.CandidatesRemoveUsedTip, () =>
            {
                used.RemoveAt(index);
                Write();
                ShowMask();
            })));
            panel.Children.Add(CandidatesBox(entry));
        }

        panel.Children.Add(ButtonRow((PackageEditorTexts.Add, PackageEditorTexts.CandidatesAddUsedTip, () =>
        {
            used.Add((string.Empty, string.Empty, string.Empty));
            Write();
            ShowMask();
        })));

        // Gruppen (die Reihenfolge zählt: die erste passende gewinnt)
        panel.Children.Add(Heading(PackageEditorTexts.CandidatesGroupsHeading));
        panel.Children.Add(Note(PackageEditorTexts.CandidatesGroupsNote));

        void MoveGroup(int index, int delta)
        {
            var target = index + delta;
            if (target < 0 || target >= groups.Count)
            {
                return;
            }

            (groups[index], groups[target]) = (groups[target], groups[index]);
            Write();
            ShowMask();
        }

        for (var i = 0; i < groups.Count; i++)
        {
            var index = i;
            var entry = new StackPanel();
            string? IdProblem(string v) => PackageDocument.IdIssueOf(groups.Select(g => g.Id).ToList(), index, v) switch
            {
                IdIssue.Invalid => PackageEditorTexts.IdInvalidName,
                IdIssue.Duplicate => PackageEditorTexts.IdDuplicate,
                _ => null,
            };

            entry.Children.Add(Row(PackageEditorTexts.CandidatesGroupId, PackageEditorTexts.CandidatesGroupIdTip, Text(groups[index].Id, PackageEditorTexts.CandidatesGroupIdTip, v =>
            {
                groups[index] = (v, groups[index].Label, groups[index].Pattern, groups[index].Hidden);
                Write();
            }, IdProblem)));
            var label = new LocalizedTextBox(PackageEditorTexts.CandidatesGroupLabel, groups[index].Label, PackageEditorTexts.CandidatesGroupLabelTip);
            label.ValueChanged += v =>
            {
                groups[index] = (groups[index].Id, v, groups[index].Pattern, groups[index].Hidden);
                Write();
            };
            entry.Children.Add(Row(PackageEditorTexts.CandidatesGroupLabel, PackageEditorTexts.CandidatesGroupLabelTip, label));
            entry.Children.Add(Row(PackageEditorTexts.CandidatesGroupPattern, PackageEditorTexts.CandidatesGroupPatternTip, Text(groups[index].Pattern, PackageEditorTexts.CandidatesGroupPatternTip, v =>
            {
                groups[index] = (groups[index].Id, groups[index].Label, v, groups[index].Hidden);
                Write();
            }, PatternProblem)));
            entry.Children.Add(Row(string.Empty, PackageEditorTexts.CandidatesGroupHiddenTip, Check(PackageEditorTexts.CandidatesGroupHidden, PackageEditorTexts.CandidatesGroupHiddenTip, groups[index].Hidden, v =>
            {
                groups[index] = (groups[index].Id, groups[index].Label, groups[index].Pattern, v);
                Write();
            })));
            entry.Children.Add(ButtonRow(
                (PackageEditorTexts.Remove, PackageEditorTexts.CandidatesRemoveGroupTip, () =>
                {
                    groups.RemoveAt(index);
                    Write();
                    ShowMask();
                }),
                (PackageEditorTexts.Up, PackageEditorTexts.MoveTip, () => MoveGroup(index, -1)),
                (PackageEditorTexts.Down, PackageEditorTexts.MoveTip, () => MoveGroup(index, 1))));
            panel.Children.Add(CandidatesBox(entry));
        }

        panel.Children.Add(ButtonRow((PackageEditorTexts.Add, PackageEditorTexts.CandidatesAddGroupTip, () =>
        {
            groups.Add((FreeId(groups.Select(g => g.Id), "group"), LocalizedText.Empty, string.Empty, false));
            Write();
            ShowMask();
        })));

        // Anlegen
        panel.Children.Add(Heading(PackageEditorTexts.CandidatesCreateHeading));
        panel.Children.Add(Row(PackageEditorTexts.CandidatesAsk, PackageEditorTexts.CandidatesAskTip, Text(ask, PackageEditorTexts.CandidatesAskTip, v =>
        {
            ask = v;
            Write();
        })));
        panel.Children.Add(Row(PackageEditorTexts.CandidatesName, PackageEditorTexts.CandidatesNameTip, Text(name, PackageEditorTexts.CandidatesNameTip, v =>
        {
            name = v;
            Write();
        })));
        panel.Children.Add(Row(PackageEditorTexts.CandidatesModifiers, PackageEditorTexts.CandidatesModifiersTip, Text(modifiers, PackageEditorTexts.CandidatesModifiersTip, v =>
        {
            modifiers = v;
            Write();
        })));

        panel.Children.Add(ButtonRow((PackageEditorTexts.CandidatesRemove, PackageEditorTexts.CandidatesRemoveTip, () =>
        {
            document.Candidates = null;
            ShowMask();
        })));
        return panel;
    }

    /// <summary>Rahmen um einen Eintrag der Listen in der Maske "Modifier ohne Idee".</summary>
    private static Border CandidatesBox(UIElement content)
    {
        var border = new Border { Child = content, BorderThickness = new Thickness(1), Padding = new Thickness(6), Margin = new Thickness(0, 2, 0, 4) };
        border.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
        return border;
    }
}
