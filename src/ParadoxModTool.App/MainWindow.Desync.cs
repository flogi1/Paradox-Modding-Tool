using System.IO;
using System.Text.Json;
using System.Windows;
using ParadoxModTool.Desync;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Desync-Analyse im Hauptfenster (Spec, App): Menüpunkt nur für Spiele mit <c>desyncRules</c>, Dienst und Fenster entstehen beim ersten Öffnen
/// und leben bis zum Beenden von PMT. Ein Spielwechsel öffnet ein eigenes PMT-Fenster; Schließen und Freigeben laufen daher nur beim Beenden.
/// </summary>
public partial class MainWindow
{
    private DesyncService? _desync;
    private DesyncWindow? _desyncWindow;

    /// <summary>Beim Start: Menüpunkt nur zeigen, wenn das Spielprofil Desync-Regeln hat.</summary>
    private void InitializeDesync() =>
        DesyncMenuItem.Visibility = (GameContext.Profile ?? _activeProfile)?.DesyncRules is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Beim Schließen von PMT, vor dem Fehlerprotokoll (das Fenster hängt an dessen Sitzung): Fenster schließen, Vergleich abbrechen.</summary>
    private void CloseDesync()
    {
        _desyncWindow?.Close();
        _desync?.Dispose();
    }

    private void OpenDesync_Click(object sender, RoutedEventArgs e) => OpenDesyncWindow();

    /// <summary>Öffnet das Fenster "Desync-Analyse" (nur eines); eigenständig oder über PMT wie das Fehlerprotokoll.</summary>
    private void OpenDesyncWindow()
    {
        if (DesyncServiceFor() is not { } service)
        {
            return;
        }

        // Mod-Spalte und Sprung zur Datei brauchen Mod-Liste und Index des Fehlerprotokolls.
        var errorLog = ErrorLogServiceFor();
        errorLog.WantIndex();
        if (_desyncWindow is null)
        {
            var owner = _settings.ErrorLogStandalone ? null : this;
            _desyncWindow = new DesyncWindow(service, errorLog, _settings, owner, new DesyncWindowHost(TryChangeBugs, id => OpenBugTracker(id)));
            _desyncWindow.Closed += (_, _) => _desyncWindow = null;
        }

        if (!_desyncWindow.IsVisible)
        {
            _desyncWindow.Show();
            return;
        }

        if (_desyncWindow.WindowState == WindowState.Minimized)
        {
            _desyncWindow.WindowState = WindowState.Normal;
        }

        _desyncWindow.Activate();
    }

    /// <summary>Legt den Dienst beim ersten Bedarf an; null (mit Meldung), wenn die Regeln fehlen oder nicht lesbar sind.</summary>
    private DesyncService? DesyncServiceFor()
    {
        if (_desync is not null)
        {
            return _desync;
        }

        var profile = GameContext.Profile ?? _activeProfile;
        var rulesPath = DesyncProfile.PathFor(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles"), profile?.DesyncRules);
        if (rulesPath is null)
        {
            return null;
        }

        DesyncProfile rules;
        try
        {
            rules = DesyncProfile.Load(rulesPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            MessageBox.Show(this, DesyncTexts.ProfileLoadFailed(rulesPath, ex.Message), DesyncTexts.Menu, MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        _desync = new DesyncService(
            rules,
            ErrorLogServiceFor().Session.Catalog,
            OosLocations.FolderFor(profile?.DefaultModRootTemplate) ?? string.Empty,
            GameContext.Repository.GameDirectory(GameContext.GameId));
        return _desync;
    }
}
