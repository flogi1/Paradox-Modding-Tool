using System.Windows;
// Seit Abschnitt 5b (Suchen & Ersetzen) referenziert das Projekt zusätzlich Windows Forms
// (UseWindowsForms, nur für FolderBrowserDialog - siehe SearchReplaceWindow.xaml.cs). Dadurch
// ist "Application" zwischen System.Windows.Application und System.Windows.Forms.Application
// mehrdeutig (CS0104); die explizite using-Alias-Direktive löst das eindeutig auf, da Alias-
// Direktiven laut C#-Spezifikation Vorrang vor mehrdeutigen Namespace-Imports haben.
using Application = System.Windows.Application;

namespace ParadoxModTool.App;

public partial class App : Application
{
    /// <summary>
    /// Zeigt zuerst das Startbild und baut dann das Hauptfenster auf (statt <c>StartupUri</c>), damit der
    /// Nutzer während des Ladens Fortschritt sieht. Das Startbild schließt, sobald das Hauptfenster
    /// zum ersten Mal vollständig gezeichnet ist.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        FocusReturnOnClose.Register();

        // Start durch den Update-Modus? (--update-applied / --update-failed / --update-cancelled, siehe UpdateStartArguments)
        var updateStart = ParadoxModTool.Updates.UpdateStartArguments.Parse(e.Args);

        // Aufruf als externer Editor des Spiels (Plan 4): an das laufende PMT des Spiels weiterreichen, sonst hier öffnen. Die
        // Update-Schalter sieht diese Erkennung nicht: sie hielte den Versionswert hinter "--update-applied" für einen Dateipfad.
        var handOff = EditorHandOffStartup.Prepare(ParadoxModTool.Updates.UpdateStartArguments.WithoutUpdateArguments(e.Args));
        if (handOff.Finished)
        {
            Shutdown();
            return;
        }

        // Oberflächentexte schon vor den Startmeldungen laden (GameContext meldet z. B. "Spiel schon offen"); die Sprache ist
        // global (settings.json). Nach dem Öffnen des Spiels wird mit der geladenen Session noch einmal geladen.
        GlobalUiLanguage.Load(GameContext.Repository);

        // Spiel dieses Fensters wählen und sperren (siehe GameContext). Ohne freies Spiel gibt es kein Fenster.
        if (!GameContext.TryOpen(handOff.Args, out var startError))
        {
            if (EditorHandOffStartup.RetryHandOff(handOff))
            {
                Shutdown();
                return;
            }

            // Die neue Version läuft; das Spiel ist nur in einem anderen PMT offen. Kein Grund für einen Rückfall.
            if (updateStart.Applied is { } appliedWithoutGame)
            {
                global::ParadoxModTool.App.MainWindow.WriteStartMarker(appliedWithoutGame);
            }

            System.Windows.MessageBox.Show(startError, "Paradox Modding Tool", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Theme vor dem ersten Fenster anwenden, damit nichts kurz im falschen Theme aufblitzt. Die Session wird hier
        // (klein, nur JSON) und später im Hauptfenster gelesen.
        var sessionAtStart = GameContext.Session.State;
        ThemeManager.Initialize(sessionAtStart);

        // Oberflächentexte (locales/<sprache>.json aus User- und Programmordner, Rückfall Englisch, sonst die Texte im Code) vor dem ersten Fenster laden.
        ParadoxModTool.Localization.UiText.Load(ParadoxModTool.Localization.LocaleSources.Default(), sessionAtStart?.UiLanguage);

        // Dateikategorien (frei editierbar) müssen vor dem Hauptfenster feststehen, weil beim Wiederherstellen der Session
        // jedes Dokument sofort klassifiziert wird.
        ParadoxModTool.Editor.FileCategoryClassifier.SetCategories(sessionAtStart?.Categories);

        var splash = new SplashWindow();
        splash.Show();
        StartupProgress.Attach(splash);
        StartupProgress.Report(0.05, MiscWindowTexts.SplashBuildingWindow);

        var main = new MainWindow { PendingEditorRequest = handOff.Pending };
        if (updateStart.Applied is { } appliedVersion)
        {
            // "MainWindow" allein wäre hier die Eigenschaft Application.MainWindow, daher der volle Typname.
            global::ParadoxModTool.App.MainWindow.WriteStartMarker(appliedVersion);
        }

        // Das zuerst erzeugte Fenster (Startbild) wäre sonst das Hauptfenster der Anwendung.
        MainWindow = main;
        main.ContentRendered += (_, _) =>
        {
            StartupProgress.Report(1.0, ParadoxModTool.Localization.CommonTexts.Done);
            StartupProgress.Finish();
            main.OnFirstRenderedAfterStart(updateStart);
        };
        main.Show();
    }

    /// <summary>"PMT neu starten": nach dem Beenden dieses Prozesses dasselbe Spiel in einem neuen Prozess öffnen.</summary>
    internal static bool RestartAfterExit { get; set; }

    protected override void OnExit(ExitEventArgs e)
    {
        GameContext.Release();

        // Erst nach dem Freigeben der Spielsperre starten, sonst meldet der neue Prozess das Spiel als schon geöffnet.
        if (RestartAfterExit)
        {
            try
            {
                GameContext.LaunchWindow(GameContext.GameId);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Programmpfad unbekannt oder Start verweigert: PMT bleibt geschlossen, der Nutzer startet es selbst.
            }
        }

        base.OnExit(e);
    }
}
