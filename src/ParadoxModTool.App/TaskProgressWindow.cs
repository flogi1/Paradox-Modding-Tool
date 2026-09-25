using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.ModCompat;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Allgemeines Fortschrittsfenster für längere Aufgaben (Regel: jede Aufgabe, die spürbar dauert, zeigt Fortschritt und lässt sich abbrechen).
/// PMT-Icon, Titel, Fortschrittsbalken (bestimmt, wenn <see cref="CompatProgress.Total"/> größer 0 ist, sonst laufend), Statuszeile und Abbrechen.
/// Der Aufrufer schließt es mit <see cref="CloseAfterFinished"/>; schließt der Nutzer es selbst, gilt das als Abbruch.
/// </summary>
public sealed class TaskProgressWindow : Window
{
    private readonly Action _onCancel;
    private readonly Func<string, int, int, string> _countText;
    private readonly Controls.ProgressBar _bar = new() { Height = 10, Minimum = 0, Maximum = 1, IsIndeterminate = true };
    private readonly Controls.TextBlock _status = new() { Margin = new Thickness(0, 6, 0, 12), TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis };
    private bool _closedByCaller;

    /// <param name="title">Titel im Fenster und in der Titelleiste.</param>
    /// <param name="hint">Erklärzeile unter dem Titel (z. B. "Das kann eine Weile dauern"); leer = keine.</param>
    /// <param name="cancelText">Beschriftung des Abbrechen-Knopfs.</param>
    /// <param name="countText">Bildet aus Text, erledigt und gesamt die Statuszeile (übersetzt).</param>
    /// <param name="onCancel">Wird beim Abbrechen aufgerufen (Knopf oder Fenster schließen).</param>
    public TaskProgressWindow(string title, string hint, string cancelText, Func<string, int, int, string> countText, Action onCancel)
    {
        _onCancel = onCancel;
        _countText = countText;
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var grid = new Controls.Grid { Margin = new Thickness(16) };
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = new Controls.Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/Assets/app-icon.png")),
            Width = 56,
            Height = 56,
            VerticalAlignment = System.Windows.VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 14, 0),
        };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        grid.Children.Add(icon);

        var panel = new Controls.StackPanel();
        Controls.Grid.SetColumn(panel, 1);
        panel.Children.Add(new Controls.TextBlock { Text = title, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
        if (hint.Length > 0)
        {
            panel.Children.Add(new Controls.TextBlock { Text = hint, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        }

        panel.Children.Add(_bar);
        panel.Children.Add(_status);
        var cancel = new Controls.Button { Content = cancelText, Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        cancel.Click += (_, _) => _onCancel();
        panel.Children.Add(cancel);
        grid.Children.Add(panel);
        Content = grid;

        Closed += (_, _) =>
        {
            if (!_closedByCaller)
            {
                _onCancel();
            }
        };
    }

    /// <summary>Schließt das Fenster, ohne den Abbruch auszulösen (die Aufgabe ist fertig oder schon abgebrochen).</summary>
    public void CloseAfterFinished()
    {
        _closedByCaller = true;
        Close();
    }

    public void Report(CompatProgress progress)
    {
        _bar.IsIndeterminate = progress.Total <= 0;
        if (progress.Total > 0)
        {
            _bar.Maximum = progress.Total;
            _bar.Value = progress.Done;
            _status.Text = _countText(progress.Text, progress.Done, progress.Total);
        }
        else
        {
            _status.Text = progress.Text;
        }
    }
}
