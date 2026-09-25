using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.Session;

namespace ParadoxModTool.App;

/// <summary>
/// Das Spiel, für das dieser Programmprozess läuft: Jedes Fenster gehört zu genau einem Spiel (Spielprofil) und hält dessen Sperre, damit
/// ein Spiel nur einmal geöffnet sein kann. Bündelt außerdem den beim Start geladenen Zustand (globale Einstellungen plus Zustand dieses
/// Spiels) und den Ablageort der Sitzungsdateien. Wird in <see cref="App.OnStartup"/> vor dem ersten Fenster eingerichtet.
/// </summary>
internal static class GameContext
{
    private const string FallbackGameId = "eu5";

    private static readonly Regex IdPattern = new("^[A-Za-z0-9_-]{1,40}$", RegexOptions.Compiled);

    private static GameLock? _lock;

    public static SessionRepository Repository { get; private set; } = SessionRepository.CreateDefault();

    public static IReadOnlyList<GameProfileEntry> Profiles { get; private set; } = Array.Empty<GameProfileEntry>();

    public static string GameId { get; private set; } = FallbackGameId;

    public static LoadedSession Session { get; private set; } = null!;

    public static GameProfile? Profile => Profiles.FirstOrDefault(e => e.Profile.Id == GameId)?.Profile;

    /// <summary>
    /// Wählt das Spiel dieses Fensters und sperrt es. Mit <c>--game &lt;id&gt;</c> genau dieses Spiel; ohne Angabe das zuletzt geöffnete, und
    /// ist es schon in einem anderen Fenster offen, das nächste freie. Gibt <see langword="false"/> mit einer Meldung zurück, wenn kein
    /// Spiel geöffnet werden kann.
    /// </summary>
    public static bool TryOpen(string[] args, out string? error)
    {
        error = null;
        Repository = SessionRepository.CreateDefault();
        Profiles = GameProfileCatalog.Discover(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles"));

        var requested = ParseGameArgument(args);
        var candidates = new List<string>();
        if (requested is not null)
        {
            if (!IdPattern.IsMatch(requested) || (Profiles.Count > 0 && Profiles.All(e => e.Profile.Id != requested)))
            {
                error = $"Für '{requested}' gibt es kein Spielprofil (Ordner 'profiles' neben der Programmdatei).";
                return false;
            }

            candidates.Add(requested);
        }
        else
        {
            if (Repository.ReadLastGameId() is { } last && (Profiles.Count == 0 || Profiles.Any(e => e.Profile.Id == last)))
            {
                candidates.Add(last);
            }

            candidates.AddRange(Profiles.Select(e => e.Profile.Id).Where(id => !candidates.Contains(id)));
            if (candidates.Count == 0)
            {
                candidates.Add(FallbackGameId);
            }
        }

        foreach (var id in candidates)
        {
            var gameLock = Repository.TryLockGame(id);
            if (gameLock is null)
            {
                continue;
            }

            _lock = gameLock;
            GameId = id;
            Repository.RecordLastGame(id);
            Session = Repository.Load(id);
            return true;
        }

        error = requested is not null
            ? $"{DisplayNameOf(requested)} ist schon in einem anderen Fenster geöffnet."
            : "Alle Spiele sind schon in einem anderen Fenster geöffnet.";
        return false;
    }

    public static void Release()
    {
        _lock?.Dispose();
        _lock = null;
    }

    public static string DisplayNameOf(string gameId) =>
        Profiles.FirstOrDefault(e => e.Profile.Id == gameId)?.Profile.DisplayName ?? gameId;

    /// <summary>Startet das Programm neu für ein anderes Spiel (eigener Prozess, eigenes Fenster).</summary>
    public static void LaunchWindow(string gameId)
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Programmpfad unbekannt.");
        var arguments = $"--game {gameId}";

        // Beim Start über "dotnet ParadoxModTool.dll" ist der Prozess dotnet.exe - dann muss die Programm-DLL mitgegeben werden.
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase)
            && System.Reflection.Assembly.GetEntryAssembly()?.Location is { Length: > 0 } entry)
        {
            arguments = $"\"{entry}\" {arguments}";
        }

        Process.Start(new ProcessStartInfo(processPath, arguments) { UseShellExecute = false });
    }

    private static string? ParseGameArgument(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--game" && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            if (args[i].StartsWith("--game=", StringComparison.Ordinal))
            {
                return args[i]["--game=".Length..];
            }
        }

        return null;
    }
}
