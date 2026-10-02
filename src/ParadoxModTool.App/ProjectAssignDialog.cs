using System.Windows;
using ParadoxModTool.Localization;
using ParadoxModTool.Mods;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Orientation = System.Windows.Controls.Orientation;
using RadioButton = System.Windows.Controls.RadioButton;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;

namespace ParadoxModTool.App;

/// <summary>
/// Ergebnis des Dialogs. Modus "gehört zu einem Hauptmod": <see cref="ParentModId"/> (<see langword="null"/> = kein Projekt) und <see cref="Role"/>, <see cref="Members"/> ist <see langword="null"/>.
/// Modus "ist Hauptmod": <see cref="Members"/> enthält die zugeordneten Mods mit Rolle (leer = alle lösen).
/// </summary>
public sealed record ProjectAssignResult(string? ParentModId, ModRole Role, IReadOnlyList<(string ModId, ModRole Role)>? Members);

/// <summary>Ordnet eine Mod einem Projekt zu: entweder "hängt an einem Hauptmod" oder "ist Hauptmod und bekommt Mods zugeordnet".</summary>
public sealed class ProjectAssignDialog : Window
{
    private readonly ModInfo _mod;
    private readonly RadioButton _asChild = new() { Content = MainTexts.AssignAsChild, GroupName = "mode" };
    private readonly RadioButton _asMain = new() { Content = MainTexts.AssignAsMain, GroupName = "mode", Margin = new Thickness(0, 4, 0, 0) };
    private readonly ComboBox _parent = new();
    private readonly ComboBox _role = new();
    private readonly StackPanel _childPanel = new();
    private readonly StackPanel _mainPanel = new();
    private readonly List<(CheckBox Check, ComboBox Role, string ModId)> _candidates = new();

    private ProjectAssignDialog(ModRegistry registry, ModInfo mod)
    {
        _mod = mod;
        Title = MainTexts.AssignTitle;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 640;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var hasChildren = registry.Mods.Any(m => m.ParentModId == mod.Id);
        var canBeMain = mod.ParentModId is null;

        _parent.Items.Add(new Choice<string?>(MainTexts.AssignNoProject, null));
        foreach (var candidate in registry.Mods.Where(m => m.Id != mod.Id && m.ParentModId is null && !hasChildren))
        {
            _parent.Items.Add(new Choice<string?>(candidate.Name, candidate.Id));
        }

        _role.Items.Add(new Choice<ModRole>(MainTexts.AssignRoleDevLong, ModRole.DevBranch));
        _role.Items.Add(new Choice<ModRole>(MainTexts.AssignRoleSubmodLong, ModRole.Submod));
        _parent.SelectedIndex = Math.Max(0, _parent.Items.Cast<Choice<string?>>().ToList().FindIndex(c => c.Value == mod.ParentModId));
        _role.SelectedIndex = mod.Role == ModRole.Submod ? 1 : 0;
        _role.IsEnabled = _parent.SelectedIndex > 0;
        _parent.SelectionChanged += (_, _) => _role.IsEnabled = _parent.SelectedIndex > 0;
        _childPanel.Children.Add(new TextBlock { Text = MainTexts.AssignMainMod, Margin = new Thickness(0, 0, 0, 2) });
        _childPanel.Children.Add(_parent);
        _childPanel.Children.Add(new TextBlock { Text = MainTexts.AssignRole, Margin = new Thickness(0, 8, 0, 2) });
        _childPanel.Children.Add(_role);

        var list = new StackPanel();
        foreach (var candidate in registry.Mods.Where(m => m.Id != mod.Id && (m.ParentModId == mod.Id || (m.ParentModId is null && !registry.Mods.Any(x => x.ParentModId == m.Id)))))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            var check = new CheckBox { Content = candidate.Name, Width = 250, IsChecked = candidate.ParentModId == mod.Id, VerticalAlignment = VerticalAlignment.Center };
            var role = new ComboBox { Width = 200 };
            role.Items.Add(new Choice<ModRole>(MainTexts.AssignRoleDev, ModRole.DevBranch));
            role.Items.Add(new Choice<ModRole>("Submod", ModRole.Submod));
            role.SelectedIndex = candidate.Role == ModRole.Submod ? 1 : 0;
            role.IsEnabled = check.IsChecked == true;
            check.Click += (_, _) => role.IsEnabled = check.IsChecked == true;
            row.Children.Add(check);
            row.Children.Add(role);
            list.Children.Add(row);
            _candidates.Add((check, role, candidate.Id));
        }

        _mainPanel.Children.Add(new TextBlock { Text = MainTexts.AssignMembers, Margin = new Thickness(0, 0, 0, 4) });
        _mainPanel.Children.Add(_candidates.Count == 0
            ? new TextBlock { Text = MainTexts.AssignNoCandidates, Foreground = System.Windows.Media.Brushes.Gray }
            : new ScrollViewer { Content = list, MaxHeight = 320, VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto });

        void UpdateMode()
        {
            _childPanel.Visibility = _asChild.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            _mainPanel.Visibility = _asMain.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        _asChild.Checked += (_, _) => UpdateMode();
        _asMain.Checked += (_, _) => UpdateMode();
        _asChild.IsEnabled = !hasChildren;
        _asMain.IsEnabled = canBeMain;
        (hasChildren || (canBeMain && mod.ParentModId is null && _candidates.Any(c => c.Check.IsChecked == true)) ? _asMain : _asChild).IsChecked = true;
        UpdateMode();

        var ok = new Button { Content = CommonTexts.Ok, Width = 90, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = CommonTexts.Cancel, Width = 90, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(new TextBlock { Text = $"Mod: {mod.Name}", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(_asChild);
        panel.Children.Add(_asMain);
        panel.Children.Add(new System.Windows.Controls.Separator { Margin = new Thickness(0, 8, 0, 8) });
        panel.Children.Add(_childPanel);
        panel.Children.Add(_mainPanel);
        panel.Children.Add(buttons);
        Content = panel;
    }

    /// <summary>Zeigt den Dialog; <see langword="null"/> bei Abbruch. Ob die Zuordnung zulässig ist, prüfen <see cref="ModRegistry.SetProject"/> und <see cref="ModRegistry.SetProjectMembers"/>.</summary>
    public static ProjectAssignResult? Ask(Window owner, ModRegistry registry, ModInfo mod)
    {
        var dialog = new ProjectAssignDialog(registry, mod) { Owner = owner };
        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        if (dialog._asMain.IsChecked == true)
        {
            var members = dialog._candidates
                .Where(c => c.Check.IsChecked == true)
                .Select(c => (c.ModId, ((Choice<ModRole>)c.Role.SelectedItem).Value))
                .ToList();
            return new ProjectAssignResult(null, ModRole.Main, members);
        }

        var parent = ((Choice<string?>)dialog._parent.SelectedItem).Value;
        return parent is null
            ? new ProjectAssignResult(null, ModRole.Main, null)
            : new ProjectAssignResult(parent, ((Choice<ModRole>)dialog._role.SelectedItem).Value, null);
    }

    private sealed class Choice<T>
    {
        public Choice(string text, T value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }

        public T Value { get; }

        public override string ToString() => Text;
    }
}
