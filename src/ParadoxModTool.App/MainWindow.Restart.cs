using ParadoxModTool.SettingsUi;

namespace ParadoxModTool.App;

/// <summary>
/// "PMT neu starten" (Einstellungen → Sprache): Das Fenster schließt sich auf dem normalen Weg (Sicherung ungespeicherter Änderungen wie beim
/// Schließen). Den neuen Prozess startet <see cref="App"/> erst nach dem Freigeben der Spielsperre, sonst fände er das Spiel noch gesperrt.
/// </summary>
public partial class MainWindow
{
    void ISettingsHost.RestartApp()
    {
        App.RestartAfterExit = true;
        Close();
        if (!_mainWindowClosed)
        {
            // Schließen abgebrochen: kein Neustart.
            App.RestartAfterExit = false;
        }
    }
}
