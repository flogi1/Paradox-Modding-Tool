using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ParadoxModTool.Localization;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Über-Fenster: Version, Ersteller, Lizenz, KI-Hinweis (Nutzerwunsch "professioneller Look").</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppInfo.Version}";
        CreatorText.Text = MiscWindowTexts.AboutCreatedBy(AppInfo.Creator);
        LicenseBox.Text = ReadLicenseText();
        SponsorLinkText.Text = AppInfo.SponsorUrl;
        KofiLinkText.Text = AppInfo.KofiUrl;

        if (AppInfo.HasRepository)
        {
            RepositoryLinkText.Text = AppInfo.RepositoryUrl;
            RepositoryLine.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Die LICENSE-Datei des Repositories ist als Ressource eingebettet (eine einzige Quelle für den Lizenztext).</summary>
    private static string ReadLicenseText()
    {
        try
        {
            var stream = System.Windows.Application.GetResourceStream(new Uri("Assets/LICENSE.txt", UriKind.Relative))?.Stream;
            if (stream is null)
            {
                return "MIT License - Copyright (c) 2026 flogi";
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException)
        {
            return "MIT License - Copyright (c) 2026 flogi";
        }
    }

    private void RepositoryLink_Click(object sender, RoutedEventArgs e) => OpenInBrowser(AppInfo.RepositoryUrl);

    private void SponsorLink_Click(object sender, RoutedEventArgs e) => OpenInBrowser(AppInfo.SponsorUrl);

    private void KofiLink_Click(object sender, RoutedEventArgs e) => OpenInBrowser(AppInfo.KofiUrl);

    private void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, MiscWindowTexts.AboutBrowserFailed(ex.Message), CommonTexts.Error);
        }
    }
}
