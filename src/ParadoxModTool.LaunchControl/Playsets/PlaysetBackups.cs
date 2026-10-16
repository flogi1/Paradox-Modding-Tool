using System.Globalization;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Sicherungen vor jeder Playset-Änderung: je Aufruf ein Satz (Ordner mit Zeitstempel) mit allen berührten Dateien zusammen
/// (Datenbank bzw. playsets.json und Lade-Datei). Es bleiben die neuesten <see cref="Keep"/> Sätze.
/// </summary>
public sealed class PlaysetBackups
{
    public const int Keep = 10;

    private readonly string _folder;
    private readonly Func<DateTime> _clock;

    public PlaysetBackups(string folder, Func<DateTime>? clock = null)
    {
        _folder = folder;
        _clock = clock ?? (() => DateTime.Now);
    }

    /// <summary>Kopiert die vorhandenen Dateien in einen neuen Satz und gibt dessen Namen zurück; fehlende Dateien entfallen.</summary>
    public string Create(IEnumerable<string> files)
    {
        var existing = files.Where(File.Exists).ToList();
        if (List().FirstOrDefault() is { } newest && SameContent(Path.Combine(_folder, newest), existing))
        {
            // Gleicher Stand wie der neueste Satz (z. B. derselbe Spielstart noch einmal): keinen weiteren Satz anlegen, damit ältere Sicherungen nicht verdrängt werden.
            return newest;
        }

        files = existing;
        var baseName = _clock().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var name = baseName;
        for (var counter = 2; Directory.Exists(Path.Combine(_folder, name)); counter++)
        {
            name = baseName + "-" + counter.ToString(CultureInfo.InvariantCulture);
        }

        var target = Path.Combine(_folder, name);
        Directory.CreateDirectory(target);
        foreach (var file in files.Where(File.Exists))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }

        Prune();
        return name;
    }

    /// <summary>Die Sätze, neueste zuerst.</summary>
    public IReadOnlyList<string> List() =>
        Directory.Exists(_folder)
            ? Directory.GetDirectories(_folder).Select(d => Path.GetFileName(d)).OrderByDescending(n => n, StringComparer.Ordinal).ToList()
            : Array.Empty<string>();

    /// <summary>Stellt die Dateien des Satzes in <paramref name="targetFolder"/> wieder her; den Stand davor sichert es als neuen Satz.</summary>
    public PlaysetResult Restore(string setName, string targetFolder)
    {
        var source = Path.Combine(_folder, setName);
        if (!Directory.Exists(source))
        {
            return new PlaysetResult(PlaysetStatus.BackupMissing, setName);
        }

        try
        {
            var files = Directory.GetFiles(source);
            var inSet = files.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Zu einer wiederhergestellten SQLite-Datei dürfen keine -wal/-shm vom neueren Stand liegen bleiben (SQLite würde sie auf die ältere Datei anwenden).
            var stale = files
                .Where(f => f.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase))
                .SelectMany(f => new[] { "-wal", "-shm" }.Select(suffix => Path.Combine(targetFolder, Path.GetFileName(f) + suffix)))
                .Where(f => !inSet.Contains(Path.GetFileName(f)) && File.Exists(f))
                .ToList();

            Create(files.Select(f => Path.Combine(targetFolder, Path.GetFileName(f))).Concat(stale));
            foreach (var file in files)
            {
                var target = Path.Combine(targetFolder, Path.GetFileName(file));
                var temp = target + ".pmt-tmp";
                File.Copy(file, temp, overwrite: true);
                File.Move(temp, target, overwrite: true);
            }

            foreach (var file in stale)
            {
                File.Delete(file);
            }

            return PlaysetResult.Ok;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PlaysetResult(PlaysetStatus.WriteFailed, ex.Message);
        }
    }

    /// <summary>Ob der Satz genau diese Dateien (nach Namen) mit demselben Inhalt enthält.</summary>
    private static bool SameContent(string setFolder, IReadOnlyList<string> files)
    {
        try
        {
            var inSet = Directory.GetFiles(setFolder);
            if (inSet.Length != files.Count)
            {
                return false;
            }

            return files.All(f =>
            {
                var saved = Path.Combine(setFolder, Path.GetFileName(f));
                return File.Exists(saved) && new FileInfo(saved).Length == new FileInfo(f).Length && File.ReadAllBytes(saved).AsSpan().SequenceEqual(File.ReadAllBytes(f));
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void Prune()
    {
        foreach (var old in List().Skip(Keep))
        {
            try
            {
                Directory.Delete(Path.Combine(_folder, old), recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ein nicht löschbarer alter Satz schadet nicht.
            }
        }
    }
}
