using System.Windows;
using ParadoxModTool.Compatch;
using ParadoxModTool.Mods;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>Zielliste eines Kompatchs bearbeiten (Register, Workshop, Ordner).</summary>
public sealed class CompatchTargetsDialog : Window
{
    private readonly CompatchPickContext _context;
    private readonly List<CompatchTarget> _targets;
    private readonly Controls.ListBox _list = new() { MinHeight = 120 };

    private CompatchTargetsDialog(CompatchPickContext context, ModInfo compatch)
    {
        _context = context;
        _targets = compatch.CompatchTargets.Select(t => new CompatchTarget { ModId = t.ModId, WorkshopId = t.WorkshopId, Folder = t.Folder, DisplayName = t.DisplayName }).ToList();
        Title = CompatchTexts.TargetsTitle(compatch.Name);
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new Controls.StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new Controls.TextBlock { Text = CompatchTexts.TargetsInfo, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_list);
        var add = new Controls.WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        add.Children.Add(Button(CompatchTexts.FromRegister, () => Add(CompatchModPicker.FromRegister(this, _context, m => m.Id != compatch.Id))));
        add.Children.Add(Button(CompatchTexts.FromWorkshop, () => Add(CompatchModPicker.FromWorkshop(this, _context))));
        add.Children.Add(Button(CompatchTexts.FromFolder, () => Add(CompatchModPicker.FromFolder(this, _context))));
        add.Children.Add(Button(CompatchTexts.Remove, () =>
        {
            if (_list.SelectedIndex >= 0)
            {
                _targets.RemoveAt(_list.SelectedIndex);
                Refresh();
            }
        }));
        panel.Children.Add(add);

        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var ok = new Controls.Button { Content = CompatchTexts.Ok, Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(ok);
        buttons.Children.Add(new Controls.Button { Content = CompatchTexts.Cancel, Width = 90, IsCancel = true });
        panel.Children.Add(buttons);
        Content = panel;
        Refresh();
    }

    public static IReadOnlyList<CompatchTarget>? Ask(Window owner, CompatchPickContext context, ModInfo compatch)
    {
        var dialog = new CompatchTargetsDialog(context, compatch) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog._targets : null;
    }

    private static Controls.Button Button(string text, Action click)
    {
        var button = new Controls.Button { Content = text, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        button.Click += (_, _) => click();
        return button;
    }

    private void Add(PickedMod? picked)
    {
        if (picked is null)
        {
            return;
        }

        var target = picked.ToTarget();
        if (_targets.All(t => t.Key != target.Key))
        {
            _targets.Add(target);
            Refresh();
        }
    }

    private void Refresh() => _list.ItemsSource = _targets.Select(Describe).ToList();

    private string Describe(CompatchTarget target)
    {
        var where = target.ModId is not null ? CompatchTexts.FromRegister : target.WorkshopId is not null ? "Workshop " + target.WorkshopId : target.Folder;
        var missing = CompatchTargets.Resolve(_context.Registry, target, id => CompatchModPicker.WorkshopFolder(_context, id)) is null ? "  (" + CompatchTexts.TargetMissing + ")" : string.Empty;
        return $"{target.DisplayName}  [{where}]{missing}";
    }
}
