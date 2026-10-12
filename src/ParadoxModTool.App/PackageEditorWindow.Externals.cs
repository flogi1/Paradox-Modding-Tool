using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators.Editing.Package;

namespace ParadoxModTool.App;

/// <summary>Masken "Externe Dateien" (<c>externals</c>) und "Hinweis nach dem Erzeugen" (<c>afterGenerate</c>) des Paket-Editors.</summary>
public sealed partial class PackageEditorWindow
{
    private FrameworkElement ExternalsMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionExternals);
        panel.Children.Add(Note(PackageEditorTexts.ExternalsNote));
        var externals = document.Externals.ToList();
        var writtenIds = externals.Select(e => e.Id).ToList();

        void Write()
        {
            // Nicht schreiben, solange eine Kennung ungültig oder doppelt ist (eine doppelte überschriebe die andere Datei); bis dahin bleibt der letzte gute Stand im Paket.
            var ids = externals.Select(e => e.Id).ToList();
            if (ids.Select((id, i) => PackageDocument.IdIssueOf(ids, i, id)).All(issue => issue is null))
            {
                document.Externals = externals.ToList();

                // Die Ausgaben nennen die Datei bei ihrer Kennung: erst mit dem Schreiben mit umbenennen, damit nichts ins Leere zeigt
                for (var i = 0; i < ids.Count; i++)
                {
                    if (writtenIds[i] != ids[i])
                    {
                        document.RenameExternalReferences(writtenIds[i], ids[i]);
                        writtenIds[i] = ids[i];
                    }
                }
            }
        }

        for (var i = 0; i < externals.Count; i++)
        {
            var index = i;
            var entry = new StackPanel { Margin = new Thickness(0, 6, 0, 10) };
            string? IdProblem(string v) => PackageDocument.IdIssueOf(externals.Select(e => e.Id).ToList(), index, v) switch
            {
                IdIssue.Invalid => PackageEditorTexts.IdInvalidName,
                IdIssue.Duplicate => PackageEditorTexts.IdDuplicate,
                _ => null,
            };

            entry.Children.Add(Row(PackageEditorTexts.ExternalId, PackageEditorTexts.ExternalIdTip, Text(externals[index].Id, PackageEditorTexts.ExternalIdTip, v =>
            {
                externals[index] = (v, externals[index].Label, externals[index].Hint);
                Write();
            }, IdProblem)));
            var label = new LocalizedTextBox(PackageEditorTexts.ExternalLabel, externals[index].Label, PackageEditorTexts.ExternalLabelTip);
            label.ValueChanged += v =>
            {
                externals[index] = (externals[index].Id, v, externals[index].Hint);
                Write();
            };
            entry.Children.Add(Row(PackageEditorTexts.ExternalLabel, PackageEditorTexts.ExternalLabelTip, label));
            var hint = new LocalizedTextBox(PackageEditorTexts.ExternalHint, externals[index].Hint, PackageEditorTexts.ExternalHintTip);
            hint.ValueChanged += v =>
            {
                externals[index] = (externals[index].Id, externals[index].Label, v);
                Write();
            };
            entry.Children.Add(Row(PackageEditorTexts.ExternalHint, PackageEditorTexts.ExternalHintTip, hint));
            entry.Children.Add(ButtonRow((PackageEditorTexts.Remove, PackageEditorTexts.RemoveExternalTip, () =>
            {
                externals.RemoveAt(index);
                writtenIds.RemoveAt(index);
                Write();
                ShowMask();
            })));
            panel.Children.Add(entry);
        }

        panel.Children.Add(ButtonRow((PackageEditorTexts.Add, PackageEditorTexts.AddExternalTip, () =>
        {
            externals.Add((FreeId(externals.Select(e => e.Id), "external"), LocalizedText.Empty, LocalizedText.Empty));
            writtenIds.Add(externals[^1].Id);
            Write();
            ShowMask();
        })));
        return panel;
    }

    private FrameworkElement AfterGenerateMask()
    {
        var document = _document!;
        var panel = MaskPanel(PackageEditorTexts.SectionAfterGenerate);
        panel.Children.Add(Note(PackageEditorTexts.AfterGenerateNote));
        var hint = new LocalizedTextBox(PackageEditorTexts.AfterGenerateLabel, document.AfterGenerate, PackageEditorTexts.AfterGenerateTip);
        hint.ValueChanged += v => document.AfterGenerate = v;
        panel.Children.Add(Row(PackageEditorTexts.AfterGenerateLabel, PackageEditorTexts.AfterGenerateTip, hint));
        return panel;
    }
}
