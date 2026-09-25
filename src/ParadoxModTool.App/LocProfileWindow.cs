using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Templates;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>
/// Dialog "Profil": zeigt die Endungen und Felder des geltenden Lokalisierungs-Profils mit ihrem Anteil und lässt einzelne ein- oder ausschalten. Die Änderungen
/// gelten nur für die Vorlage (<see cref="LocalizationSettings.ProfileChoices"/>); die Haken sind wie im Profil vorbelegt. Unten stehen die weiteren Endungen der Vorlage.
/// </summary>
public sealed class LocProfileWindow : Window
{
    private readonly List<(CheckBox Box, string Key, string WildcardKey, bool Preselected)> _boxes = new();
    private readonly TextBox _extras = new();

    private LocProfileWindow(ResolvedLocProfile profile, LocalizationSettings settings)
    {
        Title = "Lokalisierungs-Profil: " + profile.Description;
        Width = 540;
        Height = 520;
        MinWidth = 420;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock
        {
            Text = "Diese Auswahl gilt nur für diese Vorlage. Die Haken sind wie im Profil vorbelegt (gelernt aus den Spieldateien).",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });

        panel.Children.Add(Heading("Schlüssel aus dem Objektnamen (Name = X)"));
        foreach (var o in profile.Objects.Where(o => !o.Extra))
        {
            var label = o.Prefix + "Name" + o.Suffix + Share(o.Share);
            AddBox(panel, label, LocalizationProfileResolver.ObjectKey(profile.Path, o.Prefix, o.Suffix), LocalizationProfileResolver.ObjectKey(LocalizationProfileResolver.AllProfiles, o.Prefix, o.Suffix), o.Active, o.Preselected);
        }

        if (profile.Fields.Count > 0)
        {
            panel.Children.Add(Heading("Werte in Feldern (feld = Schlüssel)"));
            foreach (var f in profile.Fields)
            {
                AddBox(panel, f.Name + " = …" + Share(f.Share), LocalizationProfileResolver.FieldKey(profile.Path, f.Name), LocalizationProfileResolver.FieldKey(LocalizationProfileResolver.AllProfiles, f.Name), f.Active, f.Preselected);
            }
        }

        panel.Children.Add(Heading("Weitere Endungen (durch Leerzeichen getrennt)"));
        _extras.Text = string.Join(" ", settings.ExtraSuffixes);
        _extras.ToolTip = "Endungen, die zusätzlich zu jedem Objektnamen als Schlüssel vorgeschlagen werden, z. B. _tooltip";
        panel.Children.Add(_extras);

        var ok = new Button { Content = "OK", Width = 90, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = "Abbrechen", Width = 90, IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
    }

    /// <summary>Öffnet den Dialog; bei OK stehen die Haken und die weiteren Endungen in <paramref name="settings"/>.</summary>
    public static bool Edit(Window owner, ResolvedLocProfile profile, LocalizationSettings settings)
    {
        var window = new LocProfileWindow(profile, settings) { Owner = owner };
        if (window.ShowDialog() != true)
        {
            return false;
        }

        window.Apply(settings);
        return true;
    }

    private static string Share(double? share) => share is { } value ? $"   ({value:P0} der Objekte)" : string.Empty;

    private static TextBlock Heading(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };

    private void AddBox(StackPanel panel, string label, string key, string wildcardKey, bool active, bool preselected)
    {
        var box = new CheckBox { Content = label, IsChecked = active, Margin = new Thickness(0, 0, 0, 2) };
        _boxes.Add((box, key, wildcardKey, preselected));
        panel.Children.Add(box);
    }

    /// <summary>
    /// Schreibt nur die Haken, die vom Profil abweichen (oder bei denen ein Haken für alle Profile besteht, den ein eigener überstimmen muss); so folgen die übrigen
    /// weiter dem Profil, wenn es neu gelernt wird.
    /// </summary>
    private void Apply(LocalizationSettings settings)
    {
        foreach (var (box, key, wildcardKey, preselected) in _boxes)
        {
            var isChecked = box.IsChecked == true;
            settings.ProfileChoices.Remove(key);
            if (isChecked != preselected || settings.ProfileChoices.ContainsKey(wildcardKey))
            {
                settings.ProfileChoices[key] = isChecked;
            }
        }

        settings.ExtraSuffixes = _extras.Text.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToList();
    }
}
