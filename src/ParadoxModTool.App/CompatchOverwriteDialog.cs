using System.Windows;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

public enum OverwriteChoice
{
    All,
    OnlyOpen,
    Cancel,
}

/// <summary>
/// Nachfrage vor einer Sammelentscheidung (Nutzerwunsch): Würde sie schon getroffene Entscheidungen ändern, entscheidet der Nutzer, ob alle
/// Stellen überschrieben werden, nur die noch offenen, oder gar keine.
/// </summary>
public sealed class CompatchOverwriteDialog : Window
{
    private OverwriteChoice _choice = OverwriteChoice.Cancel;

    private CompatchOverwriteDialog(string title, string question, string allText, string subsetText)
    {
        Title = title;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new Controls.StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new Controls.TextBlock { Text = question, TextWrapping = TextWrapping.Wrap });
        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(Button(allText, OverwriteChoice.All, isDefault: false));
        buttons.Children.Add(Button(subsetText, OverwriteChoice.OnlyOpen, isDefault: true));
        var cancel = Button(CompatchTexts.Cancel, OverwriteChoice.Cancel, isDefault: false);
        cancel.IsCancel = true;
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;
    }

    /// <summary>Fragt nach; Schließen des Fensters zählt als Abbrechen.</summary>
    public static OverwriteChoice Ask(Window owner, int overwritten, int total) =>
        Show(owner, CompatchTexts.OverwriteTitle, CompatchTexts.OverwriteQuestion(overwritten, total), CompatchTexts.OverwriteAll, CompatchTexts.OverwriteOnlyOpen);

    /// <summary>
    /// Nachfrage vor "Alle als gelöst markieren", wenn offene Konflikte darunter sind (sie bekämen stillschweigend den Vorschlag).
    /// <see cref="OverwriteChoice.OnlyOpen"/> heißt hier: ohne die offenen Konflikte.
    /// </summary>
    public static OverwriteChoice AskConfirm(Window owner, int openConflicts, int total) =>
        Show(owner, CompatchTexts.ConfirmAllTitle, CompatchTexts.ConfirmAllQuestion(openConflicts, total), CompatchTexts.ConfirmAllEverything, CompatchTexts.ConfirmAllWithoutConflicts);

    private static OverwriteChoice Show(Window owner, string title, string question, string allText, string subsetText)
    {
        var dialog = new CompatchOverwriteDialog(title, question, allText, subsetText) { Owner = owner };
        dialog.ShowDialog();
        return dialog._choice;
    }

    private Controls.Button Button(string text, OverwriteChoice choice, bool isDefault)
    {
        var button = new Controls.Button { Content = new Controls.TextBlock { Text = text }, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(6, 0, 0, 0), IsDefault = isDefault };
        button.Click += (_, _) =>
        {
            _choice = choice;
            DialogResult = choice != OverwriteChoice.Cancel;
        };
        return button;
    }
}
