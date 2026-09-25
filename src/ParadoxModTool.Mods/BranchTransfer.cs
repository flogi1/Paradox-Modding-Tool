namespace ParadoxModTool.Mods;

public enum TransferMode
{
    Copy,
    Move,
}

/// <summary>Ob eine Übertragung erlaubt ist; bei Nein steht der Grund als Text da.</summary>
public sealed record TransferCheck(bool Allowed, string? Reason)
{
    public static TransferCheck Yes { get; } = new(true, null);

    public static TransferCheck No(string reason) => new(false, reason);
}

public enum TransferOutcome
{
    Done,
    TargetExists,
    SourceMissing,

    /// <summary>Die Zieldatei ist gesperrt (Datei- oder Ordner-Eintrag in <see cref="ModInfo.LockedPaths"/>); nichts wurde geschrieben.</summary>
    TargetLocked,

    /// <summary>Die Quelldatei ist gesperrt (auch zum Kopieren); nichts wurde übertragen.</summary>
    SourceLocked,
}

/// <summary>Ergebnis einer Datei: <see cref="BackupPath"/> ist gesetzt, wenn eine vorhandene Zieldatei vorher gesichert wurde.</summary>
public sealed record TransferResult(TransferOutcome Outcome, string RelativePath, string? BackupPath);

/// <summary>
/// Kopiert oder verschiebt Dateien zwischen den Arbeitsordnern von Hauptmod und Dev-Branches eines Projekts. Geschrieben wird atomar (Temp-Datei, dann <see cref="File.Move(string, string, bool)"/>),
/// eine überschriebene Zieldatei wird vorher gesichert, und beim Verschieben wird die Quelle erst nach erfolgreichem Schreiben gelöscht.
/// </summary>
public static class BranchTransfer
{
    /// <summary>Der Arbeitsordner der Mod: ihr erster vorhandener Pfad (wie bei Release und GitHub-Veröffentlichung); <see langword="null"/>, wenn keiner existiert.</summary>
    public static string? WorkingFolder(ModInfo mod) => mod.Paths.FirstOrDefault(Directory.Exists);

    public static TransferCheck Check(ModRegistry registry, ModInfo from, ModInfo to, TransferMode mode)
    {
        if (from.Id == to.Id)
        {
            return TransferCheck.No("Quelle und Ziel sind dieselbe Mod.");
        }

        if (ModProjects.Root(registry, from).Id != ModProjects.Root(registry, to).Id)
        {
            return TransferCheck.No("Die Mods gehören nicht zum selben Projekt.");
        }

        if (to.IsLocked)
        {
            return TransferCheck.No($"\"{to.Name}\" ist gesperrt.");
        }

        if (from.IsLocked)
        {
            return TransferCheck.No($"\"{from.Name}\" ist gesperrt; daraus wird nichts übertragen.");
        }

        return TransferCheck.Yes;
    }

    /// <summary>Die Dateien unter einem Ordner der Mod (relativ zum Arbeitsordner, mit <c>/</c>), ohne ausgeschlossene Ordner und Dateien.</summary>
    public static IReadOnlyList<string> FilesUnder(ModInfo mod, string relativeFolder)
    {
        var folder = WorkingFolder(mod) ?? throw new DirectoryNotFoundException($"Die Mod \"{mod.Name}\" hat keinen vorhandenen Ordner.");
        var normalized = Normalize(relativeFolder);
        return ModStaging.EnumerateFiles(folder, ModStagingRules.Default.Extend(null, null, new[] { normalized }));
    }

    public static bool TargetExists(ModInfo to, string relativePath)
    {
        var folder = WorkingFolder(to);
        return folder is not null && File.Exists(ToFullPath(folder, Normalize(relativePath)));
    }

    /// <param name="backupStamp">Name des Backup-Unterordners; <see langword="null"/> = aktueller Zeitpunkt. Ein Aufrufer, der mehrere Dateien überträgt, gibt einen gemeinsamen Wert mit.</param>
    public static TransferResult Execute(ModRegistry registry, ModInfo from, ModInfo to, string relativePath, TransferMode mode, bool overwrite, string? backupStamp = null)
    {
        var check = Check(registry, from, to, mode);
        if (!check.Allowed)
        {
            throw new InvalidOperationException(check.Reason);
        }

        var relative = Normalize(relativePath);
        var fromFolder = WorkingFolder(from) ?? throw new DirectoryNotFoundException($"Die Mod \"{from.Name}\" hat keinen vorhandenen Ordner.");
        var toFolder = WorkingFolder(to) ?? throw new DirectoryNotFoundException($"Die Mod \"{to.Name}\" hat keinen vorhandenen Ordner.");
        var source = ToFullPath(fromFolder, relative);
        var target = ToFullPath(toFolder, relative);

        if (!File.Exists(source))
        {
            return new TransferResult(TransferOutcome.SourceMissing, relative, null);
        }

        if (ModLocks.LockedBy(from, relative) is not null)
        {
            return new TransferResult(TransferOutcome.SourceLocked, relative, null);
        }

        if (ModLocks.LockedBy(to, relative) is not null)
        {
            return new TransferResult(TransferOutcome.TargetLocked, relative, null);
        }

        string? backupPath = null;
        if (File.Exists(target))
        {
            if (!overwrite)
            {
                return new TransferResult(TransferOutcome.TargetExists, relative, null);
            }

            var stamp = backupStamp ?? DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            backupPath = ToFullPath(Path.Combine(registry.DataDirectory(to.Id), "backups", "branch-transfer", stamp), relative);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            File.Copy(target, backupPath, overwrite: true);
        }

        var targetDirectory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(targetDirectory);
        var temp = Path.Combine(targetDirectory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.Copy(source, temp, overwrite: true);
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        if (mode == TransferMode.Move)
        {
            if (new FileInfo(target).Length != new FileInfo(source).Length)
            {
                throw new IOException($"Die Zieldatei {relative} hat eine andere Größe als die Quelle; die Quelle bleibt erhalten.");
            }

            File.Delete(source);
            PruneEmptyDirectories(Path.GetDirectoryName(source)!, fromFolder);
        }

        return new TransferResult(TransferOutcome.Done, relative, backupPath);
    }

    /// <summary>Relativer Pfad mit <c>/</c>. Wirft <see cref="InvalidOperationException"/> bei leerem, absolutem oder aus dem Ordner herausführendem Pfad.</summary>
    private static string Normalize(string relativePath) => ModLocks.NormalizePath(relativePath);

    private static string ToFullPath(string folder, string relative) => Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));

    private static void PruneEmptyDirectories(string directory, string stopAt)
    {
        var stop = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stopAt));
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        while (current.Length > stop.Length
               && current.StartsWith(stop, StringComparison.OrdinalIgnoreCase)
               && Directory.Exists(current)
               && !Directory.EnumerateFileSystemEntries(current).Any())
        {
            Directory.Delete(current);
            current = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(current)!);
        }
    }
}
