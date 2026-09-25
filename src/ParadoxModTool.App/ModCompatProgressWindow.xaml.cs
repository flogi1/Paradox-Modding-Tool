using System.Windows;
using ParadoxModTool.ModCompat;

namespace ParadoxModTool.App;

/// <summary>Eigenes Fenster für den Fortschritt einer Mod-Kompatibilitätsprüfung, siehe <c>ModCompatWindow.Analysis.cs</c>.</summary>
public partial class ModCompatProgressWindow : Window
{
    private readonly Action _onCancel;
    private bool _closedByCaller;

    public ModCompatProgressWindow(Action onCancel)
    {
        InitializeComponent();
        _onCancel = onCancel;
        Closed += (_, _) =>
        {
            if (!_closedByCaller)
            {
                _onCancel();
            }
        };
    }

    /// <summary>Schließt das Fenster, ohne <c>onCancel</c> auszulösen (die Prüfung ist bereits fertig oder abgebrochen).</summary>
    public void CloseAfterFinished()
    {
        _closedByCaller = true;
        Close();
    }

    public void Report(CompatProgress progress)
    {
        Bar.IsIndeterminate = progress.Total <= 0;
        if (progress.Total > 0)
        {
            Bar.Maximum = progress.Total;
            Bar.Value = progress.Done;
            StatusText.Text = $"{progress.Text} ({progress.Done}/{progress.Total})";
        }
        else
        {
            StatusText.Text = progress.Text + "…";
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _onCancel();
}
