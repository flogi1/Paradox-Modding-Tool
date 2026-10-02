using System.Windows;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// "Ausblenden: Grafik, Modelle, Audio, Schriften" als Checkboxen (wie im Kompatch-Start), für die Dateilisten des Compare und Update Tools und
/// den Start des Update-Merge. <see cref="Changed"/> meldet jede Änderung.
/// </summary>
public sealed class FileKindFilterBar : Controls.WrapPanel
{
    private static readonly string[] Kinds = CompatchFileFilter.DefaultHiddenExtensions.Values.Distinct().ToArray();

    private readonly Dictionary<string, Controls.CheckBox> _boxes = new(StringComparer.Ordinal);
    private bool _setting;

    public FileKindFilterBar(IEnumerable<string> hiddenKinds)
    {
        var label = new Controls.TextBlock { Text = VanillaUpdateTexts.HideKindsLabel, VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        Children.Add(label);
        ToolTip = VanillaUpdateTexts.HideKindsTip;
        var hidden = hiddenKinds.ToHashSet(StringComparer.Ordinal);
        foreach (var kind in Kinds)
        {
            var box = new Controls.CheckBox
            {
                Content = new Controls.TextBlock { Text = CompatchTexts.FileKind(kind) },
                IsChecked = hidden.Contains(kind),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            };
            box.Click += (_, _) =>
            {
                if (!_setting)
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            };
            _boxes[kind] = box;
            Children.Add(box);
        }
    }

    public event EventHandler? Changed;

    /// <summary>Die angehakten Dateiarten (Namen aus <see cref="CompatchFileFilter.DefaultHiddenExtensions"/>).</summary>
    public IReadOnlyList<string> HiddenKinds => _boxes.Where(b => b.Value.IsChecked == true).Select(b => b.Key).ToList();

    /// <summary>Übernimmt eine Auswahl von außen (andere Leiste im selben Fenster), ohne <see cref="Changed"/> auszulösen.</summary>
    public void SetHidden(IEnumerable<string> hiddenKinds)
    {
        var hidden = hiddenKinds.ToHashSet(StringComparer.Ordinal);
        _setting = true;
        foreach (var (kind, box) in _boxes)
        {
            box.IsChecked = hidden.Contains(kind);
        }

        _setting = false;
    }
}
