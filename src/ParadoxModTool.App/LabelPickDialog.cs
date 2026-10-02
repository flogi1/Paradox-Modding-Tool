using System.Windows;
using ParadoxModTool.Notes;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>Auswahl von Labels eines Repositories per Häkchen (für den Label-Filter einer GitHub-Quelle).</summary>
public sealed class LabelPickDialog : Window
{
    private readonly List<Controls.CheckBox> _boxes = new();

    public LabelPickDialog(string repo, IReadOnlyList<string> labels, IReadOnlyList<string> selected)
    {
        Title = BugTexts.LabelsTitle(repo);
        Width = 340;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;

        var root = new Controls.StackPanel { Margin = new Thickness(12) };
        var list = new Controls.StackPanel();
        foreach (var label in labels)
        {
            var box = new Controls.CheckBox
            {
                Content = new Controls.TextBlock { Text = label },
                Tag = label,
                IsChecked = selected.Contains(label, StringComparer.OrdinalIgnoreCase),
                Margin = new Thickness(0, 2, 0, 2),
            };
            _boxes.Add(box);
            list.Children.Add(box);
        }

        if (labels.Count == 0)
        {
            list.Children.Add(new Controls.TextBlock { Text = BugTexts.LabelsNone });
        }

        root.Children.Add(new Controls.ScrollViewer { Content = list, MaxHeight = 360, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto });
        var ok = new Controls.Button { Content = BugTexts.Save, Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Controls.Button { Content = BugTexts.Cancel, Width = 90, IsCancel = true };
        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);
        Content = root;
    }

    public IReadOnlyList<string> Selected => _boxes.Where(box => box.IsChecked == true).Select(box => (string)box.Tag).ToList();
}
