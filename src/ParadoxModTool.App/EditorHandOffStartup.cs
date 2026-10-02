using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.Session;

namespace ParadoxModTool.App;

/// <summary>
/// Ergebnis des Starts als Editor: <c>Finished</c> = dieser Prozess endet (an ein laufendes PMT weitergegeben, oder keine Datei erkannt und gemeldet);
/// sonst die Argumente für <see cref="GameContext"/> und die zu öffnende Datei.
/// </summary>
internal sealed record EditorHandOffStart(bool Finished, string[] Args, EditorRequest? Pending, string? GameId = null);

/// <summary>
/// Start von PMT als externer Editor des Spiels (Spec "Weitergabe an ein laufendes PMT", Plan 4 Ruling 1 bis 3): rohe Kommandozeile protokollieren, Datei und Zeile erkennen, das Spiel
/// bestimmen (nur lesend), an das laufende PMT dieses Spiels weitergeben oder selbst mit <c>--game</c> starten und die Datei öffnen.
/// </summary>
internal static class EditorHandOffStartup
{
    private const int AsfwAny = -1;

    public static string LogPath(SessionRepository repository) => Path.Combine(repository.Root, "logs", "editor-handoff.log");

    public static EditorHandOffStart Prepare(string[] args)
    {
        // Rohe Kommandozeile vor der Erkennung (Plan 4 Ruling 1): auch ein unbekanntes Format landet im Protokoll (Spec-Testpunkt 3).
        if (!EditorArguments.LooksLikeEditorCall(args))
        {
            return new EditorHandOffStart(false, args, null);
        }

        var repository = SessionRepository.CreateDefault();
        var log = LogPath(repository);
        HandOffLog.Append(log, "command line: " + Environment.CommandLine, DateTime.Now);
        var request = EditorArguments.Parse(args, File.Exists);
        if (request is null)
        {
            // Aufruf ohne erkennbare Datei (Review Plan 4): nicht still das nächste freie Spiel öffnen, sondern melden und enden.
            HandOffLog.Append(log, "no file recognised", DateTime.Now);
            GlobalUiLanguage.Load(repository);
            System.Windows.MessageBox.Show(GuiTexts.HandOffNotRecognised(log), "Paradox Modding Tool", MessageBoxButton.OK, MessageBoxImage.Information);
            return new EditorHandOffStart(true, args, null);
        }

        var profiles = GameProfileCatalog.Discover(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles"));
        var games = GameFolderReader.Read(
            Path.Combine(repository.Root, "games"),
            profiles.Select(e => new GameProfileFolders(
                e.Profile.Id,
                string.IsNullOrWhiteSpace(e.Profile.DefaultModRootTemplate) ? null : Environment.ExpandEnvironmentVariables(e.Profile.DefaultModRootTemplate),
                e.Profile.SteamAppId)));
        var gameId = EditorGameMatcher.GameFor(request.Path, games);
        HandOffLog.Append(log, $"file: {request.Path}, line: {request.Line?.ToString() ?? "-"}, game: {gameId ?? "-"}", DateTime.Now);
        if (gameId is null)
        {
            // Unbekannter Ort: normaler Start, die Datei öffnet sich im Fenster, das sich öffnet (Plan 4 Ruling 2).
            return new EditorHandOffStart(false, Array.Empty<string>(), request);
        }

        if (repository.IsGameOpen(gameId))
        {
            // Das PMT des Spiels läuft oder startet gerade: es darf nach vorn (Plan 4 Ruling 3).
            AllowSetForegroundWindow(AsfwAny);
            var sent = EditorHandOffPipe.SendAsync(gameId, request, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            HandOffLog.Append(log, sent ? "handed over" : "hand-over failed", DateTime.Now);
            if (sent)
            {
                return new EditorHandOffStart(true, args, null);
            }
        }

        return new EditorHandOffStart(false, new[] { "--game", gameId }, request, gameId);
    }

    /// <summary>
    /// Zweiter Versuch, wenn das Spiel sich nicht öffnen ließ, weil es schon in einem anderen Fenster offen ist (Plan 4 Ruling 3: langsamer Start des
    /// anderen PMT oder Doppelklick im Spiel): bis zu 20 Sekunden weitergeben. True, wenn angenommen; dann endet dieser Prozess still.
    /// </summary>
    public static bool RetryHandOff(EditorHandOffStart start)
    {
        if (start.Pending is not { } request || start.GameId is not { } gameId)
        {
            return false;
        }

        AllowSetForegroundWindow(AsfwAny);
        var sent = EditorHandOffPipe.SendAsync(gameId, request, TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
        HandOffLog.Append(LogPath(SessionRepository.CreateDefault()), sent ? "handed over on retry" : "hand-over failed on retry", DateTime.Now);
        return sent;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
