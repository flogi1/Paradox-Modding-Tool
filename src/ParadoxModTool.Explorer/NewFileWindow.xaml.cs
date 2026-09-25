using System.Windows;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Minimaler Dialog zum Anlegen einer neuen Datei in einem Ordner der Verzeichnis-Übersicht
/// (siehe XAML-Kommentar). Liefert bei Bestätigung den gewählten Dateinamen INKLUSIVE Endung
/// über <see cref="ResultFileName"/>; die eigentliche Datei wird NICHT hier, sondern von
/// <c>MainWindow</c> über den programmweiten Aktionsverlauf angelegt (siehe
/// <c>ExplorerPanel.NewFileRequested</c>), damit das Anlegen wie jede andere Programmaktion
/// rückgängig gemacht werden kann (Konzept: "alle Prozesse ... mit einer Funktion rückgängig
/// gemacht werden können").
/// </summary>
public partial class NewFileWindow : Window
{
    private static readonly string[] FallbackExtensions = { ".txt", ".gui", ".gfx", ".vml", ".yml", ".lua" };

    /// <summary>
    /// <paramref name="preferredExtension"/> (Nutzer-Feedback: "Im gui Ordner sollte
    /// standardmäßig die GUI Datei ausgewählt sein. In den localitzation Ordner standardmäßig
    /// yml; für die anderen Ordner ist .txt die richtige Option") - von ExplorerPanel anhand des
    /// Zielordnernamens vorbelegt (siehe ExplorerPanel.GuessPreferredExtension). Ist die Endung
    /// nicht in <paramref name="knownExtensions"/>/<see cref="FallbackExtensions"/> enthalten
    /// (z. B. weil das aktive Spielprofil sie gar nicht kennt) oder <see langword="null"/> (kein
    /// erkannter Ordnername), fällt die Auswahl weiterhin auf Index 0 zurück - unverändertes
    /// bisheriges Verhalten.
    /// </summary>
    public NewFileWindow(string targetFolderPath, IReadOnlyList<string>? knownExtensions, string? preferredExtension = null)
    {
        InitializeComponent();
        TargetFolderText.Text = targetFolderPath;

        var extensions = knownExtensions is { Count: > 0 } ? knownExtensions : FallbackExtensions;
        ExtensionCombo.ItemsSource = extensions;

        var preferredIndex = preferredExtension is null
            ? -1
            : extensions.ToList().FindIndex(ext => string.Equals(ext, preferredExtension, StringComparison.OrdinalIgnoreCase));
        ExtensionCombo.SelectedIndex = preferredIndex >= 0 ? preferredIndex : 0;

        Loaded += (_, _) =>
        {
            FileNameBox.Focus();
        };
    }

    /// <summary>Dateiname INKLUSIVE Endung (z. B. "neue_datei.txt"), sofern mit "Anlegen"
    /// bestätigt (<c>ShowDialog() == true</c>) und ein nicht-leerer Name eingegeben wurde.</summary>
    public string ResultFileName { get; private set; } = string.Empty;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var name = FileNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            FileNameBox.Focus();
            return;
        }

        var extension = ExtensionCombo.SelectedItem as string ?? FallbackExtensions[0];

        // Hat der Nutzer bereits selbst eine (ggf. andere) Endung eingetippt, wird diese
        // respektiert statt die im Dropdown gewählte zusätzlich anzuhängen.
        ResultFileName = System.IO.Path.HasExtension(name) ? name : name + extension;
        DialogResult = true;
    }
}
