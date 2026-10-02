using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using ParadoxModTool.VersionManager;

namespace ParadoxModTool.App;

/// <summary>
/// Einstellungsdialog für die Speicherorte von Snapshots ("[Modname] Snapshots") und
/// Vanilla-Historie ("EU V Vanilla History"): entweder ein frei wählbarer Oberordner außerhalb
/// des Mods (empfohlen) oder direkt im Mod-Ordner (mit deutlichem Warnhinweis, da Steam den
/// gesamten Ordnerinhalt hochlädt).
/// </summary>
public partial class StorageSettingsWindow : Window
{
    public StorageSettings Result { get; private set; }

    public StorageSettingsWindow(StorageSettings current)
    {
        InitializeComponent();
        Result = current;

        ParentTextBox.Text = string.IsNullOrWhiteSpace(current.ParentFolder)
            ? StorageLocations.GetDefaultParentFolder()
            : current.ParentFolder;
        (current.InsideModFolder ? InsideRadio : ExternalRadio).IsChecked = true;
        UpdateUi();
    }

    private void Mode_Changed(object sender, RoutedEventArgs e) => UpdateUi();

    private void ParentTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateUi();

    private void UpdateUi()
    {
        // Wird beim Laden des XAML schon vor der Initialisierung aller Elemente ausgelöst.
        if (PreviewText is null || InsideRadio is null || ExternalPanel is null)
        {
            return;
        }

        var inside = InsideRadio.IsChecked == true;
        ExternalPanel.IsEnabled = !inside;
        InsideWarning.Visibility = inside ? Visibility.Visible : Visibility.Collapsed;

        var parent = ParentTextBox.Text.Trim();
        PreviewText.Text = string.IsNullOrEmpty(parent)
            ? MiscWindowTexts.StorageParentRequired
            : MiscWindowTexts.StorageWillCreate(Path.Combine(parent, StorageLocations.VanillaHistoryFolderName), Path.Combine(parent, MiscWindowTexts.StorageModNamePlaceholder + StorageLocations.SnapshotsFolderSuffix));
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = MiscWindowTexts.StorageBrowseDescription,
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(ParentTextBox.Text) ? ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(ParentTextBox.Text) : string.Empty,
        };

        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("storage.folder");
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ParentTextBox.Text = dialog.SelectedPath;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (InsideRadio.IsChecked == true)
        {
            var confirm = MessageBox.Show(
                this,
                MiscWindowTexts.StorageInsideConfirm,
                MiscWindowTexts.StorageNotRecommended,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            Result = Result with { InsideModFolder = true };
        }
        else
        {
            var parent = ParentTextBox.Text.Trim();
            if (string.IsNullOrEmpty(parent) || !Path.IsPathRooted(parent))
            {
                MessageBox.Show(this, MiscWindowTexts.StorageInvalidFolder, MiscWindowTexts.StorageInvalidFolderTitle);
                return;
            }

            Result = Result with { ParentFolder = parent, InsideModFolder = false };
        }

        DialogResult = true;
    }
}
