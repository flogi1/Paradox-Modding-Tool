using System.Windows;

namespace ParadoxModTool.App;

/// <summary>
/// Dialog für das Formatierungs-Werkzeug (Konzept, Abschnitt 10). Beide Optionen sind unabhängig
/// wählbar; <see cref="MainWindow.FormatDocument_Click"/> wendet die Leerzeichen-Bereinigung
/// (<see cref="ParadoxModTool.Tools.WhitespaceCleaner"/>) UND die Struktur-Neuformatierung
/// (<see cref="ParadoxModTool.Tools.ScriptPrettyPrinter"/>) auf Wunsch nacheinander auf das
/// aktive Dokument an, sofern der Dialog mit OK bestätigt wurde (<c>ShowDialog() == true</c>).
///
/// <paramref name="structuralFormattingAvailable"/> (Konstruktor) deaktiviert die
/// Struktur-Checkbox bereits im Dialog, wenn die aktive Datei anhand ihrer Endung erkennbar KEIN
/// Paradox-Skriptdialekt ist (z. B. .yml) - so erfährt der Nutzer schon vor dem Klick auf OK, statt
/// erst nach einer fehlgeschlagenen Formatierung, dass diese Option hier nicht greift.
/// </summary>
public partial class FormatDocumentWindow : Window
{
    public FormatDocumentWindow(bool structuralFormattingAvailable)
    {
        InitializeComponent();

        if (structuralFormattingAvailable)
        {
            PrettyPrintHintText.Text =
                "Baut den Aufbau der Datei (Schlüssel/Wert-Paare, Blöcke) komplett neu auf und " +
                "vereinheitlicht dabei die Einrückung. Schlägt fehl, ohne etwas zu ändern, falls " +
                "die Datei nicht als gültiges Paradox-Skript gelesen werden kann.";
        }
        else
        {
            PrettyPrintCheckBox.IsEnabled = false;
            PrettyPrintHintText.Text =
                "Für diesen Dateityp nicht verfügbar - die Struktur-Neuformatierung versteht nur " +
                "das Paradox-Skriptformat (.txt/.gui/.gfx/.vml), nicht z. B. YML-Lokalisierungs- " +
                "oder LUA-Dateien.";
        }
    }

    public bool CleanWhitespace { get; private set; }

    public bool PrettyPrintStructure { get; private set; }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        CleanWhitespace = CleanWhitespaceCheckBox.IsChecked == true;
        PrettyPrintStructure = PrettyPrintCheckBox.IsChecked == true;
        DialogResult = true;
    }
}
