using System.IO;
using System.Windows;
using ParadoxModTool.Settings;
using ParadoxModTool.Templates;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Lokalisierungs-Profile: Dienst, Neu-Lernen (Einstellungsfenster) und der Ordner der zuletzt aktiven Datei für das Template-Fenster.</summary>
public partial class MainWindow
{
    private LocalizationProfileService? _locProfileService;

    private LocalizationProfileService LocProfileService => _locProfileService ??= new LocalizationProfileService(
        Path.Combine(GameContext.Repository.GameDirectory(GameContext.GameId), "localization", "profiles.json"),
        () => CollectOriginRoots().Vanilla);

    /// <summary>Gleicht die Profile mit den Spieldateien ab und lernt im Hintergrund neu, falls nötig. Fehler stören den Start nicht (dann gilt das Rückfallprofil).</summary>
    private async Task<LocalizationProfileUpdate?> UpdateLocProfilesAsync(bool force, CancellationToken cancellation = default, IProgress<(int Done, int Total)>? progress = null)
    {
        try
        {
            return await LocProfileService.UpdateAsync(force, cancellation, progress);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                   || (ex is OperationCanceledException && !cancellation.IsCancellationRequested))
        {
            return null;
        }
    }

    /// <summary>Lernt die Profile mit Fortschrittsfenster neu; nach einem Abbruch bleibt der bisherige Stand, ohne Meldung.</summary>
    private async Task RelearnLocProfilesAsync()
    {
        LocalizationProfileUpdate? result = null;
        if (!await RunWithProgressAsync(SettingsTexts.RelearnLocProfiles, async (cancellation, progress) => result = await UpdateLocProfilesAsync(force: true, cancellation, progress)))
        {
            return;
        }

        MessageBox.Show(
            this,
            result is null ? TemplateWindowTexts.LocProfilesLearnFailed : result.Summary + (result.Learned ? TemplateWindowTexts.LocProfilesDuration(result.Duration.TotalSeconds) : string.Empty),
            TemplateWindowTexts.LocProfilesTitle,
            MessageBoxButton.OK,
            result is null ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    /// <summary>Der Ordner der zuletzt aktiven Datei relativ zum Spielordner, z. B. <c>common/building_types</c>; <see langword="null"/>, wenn keine Datei offen oder der Ordner unbekannt ist.</summary>
    private string? LocSourceRelativeFolder()
    {
        var (vanilla, mod) = CollectOriginRoots();
        return LocalizationProfileResolver.RelativeFolder((_modSourcePane ?? PanelPane).CurrentFilePath, vanilla.Concat(mod));
    }
}
