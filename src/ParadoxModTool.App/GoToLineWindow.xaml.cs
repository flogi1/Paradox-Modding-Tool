using System.Windows;

namespace ParadoxModTool.App;

/// <summary>
/// Minimaler Dialog für "Gehe zu Zeile" (Nutzer-Feedback: "Es sollte eine Möglichkeit geben,
/// direkt zu einer Zeile zu springen"; Strg+G wie in Notepad++ - siehe MainWindow.xaml.cs).
/// Liefert die eingegebene Zeilennummer über <see cref="LineNumber"/>, sofern der Nutzer mit OK
/// bestätigt (<c>ShowDialog() == true</c>) und eine gültige positive Zahl eingegeben wurde -
/// andernfalls bleibt der Dialog offen und markiert die Eingabe erneut zur Korrektur.
/// </summary>
public partial class GoToLineWindow : Window
{
    public GoToLineWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LineNumberBox.Focus();
            LineNumberBox.SelectAll();
        };
    }

    public int LineNumber { get; private set; }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(LineNumberBox.Text.Trim(), out var value) && value > 0)
        {
            LineNumber = value;
            DialogResult = true;
        }
        else
        {
            LineNumberBox.SelectAll();
            LineNumberBox.Focus();
        }
    }
}
