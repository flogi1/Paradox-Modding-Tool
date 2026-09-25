namespace ParadoxModTool.Mods;

/// <summary>
/// Sperren einzelner Dateien und Ordner einer Mod (<see cref="ModInfo.LockedPaths"/>). Ein Eintrag sperrt eine Datei oder einen Ordner samt allem darunter.
/// Die Sperre gilt für Übertragen und Abgleich (PMT überträgt weder in eine gesperrte Datei noch aus ihr), nicht für den Editor.
/// </summary>
public static class ModLocks
{
    /// <summary>Relativer Pfad mit <c>/</c>, ohne abschließende <c>/</c>, ohne <c>.</c>-Segmente. Wirft <see cref="InvalidOperationException"/> bei leerem, absolutem (auch mit führendem <c>/</c>) oder aus dem Ordner herausführendem Pfad.</summary>
    public static string NormalizePath(string relativePath)
    {
        var text = (relativePath ?? string.Empty).Trim().Replace('\\', '/');
        var segments = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (text.Length == 0 || text.StartsWith('/') || Path.IsPathRooted(text) || text.Contains(':') || segments.Length == 0 || segments.Any(s => s == ".."))
        {
            throw new InvalidOperationException($"Ungültiger Pfad innerhalb der Mod: \"{relativePath}\".");
        }

        return string.Join('/', segments.Where(s => s != "."));
    }

    /// <summary>
    /// Bereinigt eine geladene Liste: ungültige Einträge entfallen, Pfade werden normalisiert, Doppelte (ohne Beachtung der Schreibweise) entfallen. Der Pfad mit der Schreibweise des ersten Vorkommens bleibt.
    /// </summary>
    public static List<string> NormalizeEntries(IEnumerable<string>? entries)
    {
        var result = new List<string>();
        foreach (var entry in entries ?? Array.Empty<string>())
        {
            string normalized;
            try
            {
                normalized = NormalizePath(entry);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (!result.Any(e => string.Equals(e, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    /// <summary>Der Eintrag in <see cref="ModInfo.LockedPaths"/>, der diesen Pfad sperrt (Datei-Eintrag oder Ordner darüber); <see langword="null"/>, wenn keiner sperrt. Die Sperre der ganzen Mod fließt hier nicht ein.</summary>
    public static string? LockedBy(ModInfo mod, string relativePath)
    {
        var path = NormalizePath(relativePath);
        foreach (var entry in mod.LockedPaths)
        {
            if (string.Equals(path, entry, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(entry + "/", StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Warum ein Pfad gesperrt ist (für Symbole und Tooltips); <see langword="null"/>, wenn er nicht gesperrt ist. Ein leerer Pfad meint den Wurzelordner der Mod und ist nur durch die Sperre der ganzen Mod gesperrt.</summary>
    public static LockReason? Reason(ModInfo mod, string relativePath)
    {
        if (mod.IsLocked)
        {
            return new LockReason(LockReasonKind.WholeMod, null);
        }

        if (string.IsNullOrWhiteSpace(relativePath) || LockedBy(mod, relativePath) is not { } by)
        {
            return null;
        }

        return string.Equals(by, NormalizePath(relativePath), StringComparison.OrdinalIgnoreCase)
            ? new LockReason(LockReasonKind.ThisPath, by)
            : new LockReason(LockReasonKind.ParentFolder, by);
    }

    /// <summary>Erklärt für Tooltips, was gesperrt ist und was die Sperre bewirkt.</summary>
    public static string Describe(LockReason reason, bool isDirectory)
    {
        var what = reason.Kind switch
        {
            LockReasonKind.WholeMod => "Die ganze Mod ist gesperrt.",
            LockReasonKind.ThisPath => isDirectory ? "Dieser Ordner ist gesperrt." : "Diese Datei ist gesperrt.",
            _ => $"Der Ordner {reason.ByPath} darüber ist gesperrt.",
        };
        return what + " Übertragen und Abgleich lassen sie unangetastet: nichts wird hineingeschrieben, nichts herauskopiert oder verschoben. Der Editor bleibt unberührt.";
    }

    /// <summary>Anzahl der Sperr-Einträge in <see cref="ModInfo.LockedPaths"/> im Ordner (auch der Ordner selbst und alles darunter); ein leerer Pfad meint die ganze Mod. Die Sperre der ganzen Mod zählt nicht.</summary>
    public static int CountUnder(ModInfo mod, string relativeFolder) => mod.LockedPaths.Count(e => IsUnder(e, relativeFolder));

    internal static bool IsUnder(string entry, string relativeFolder)
    {
        if (string.IsNullOrWhiteSpace(relativeFolder))
        {
            return true;
        }

        var folder = NormalizePath(relativeFolder);
        return string.Equals(entry, folder, StringComparison.OrdinalIgnoreCase)
            || entry.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Die ganze Mod ist gesperrt oder der Pfad liegt unter einem Sperr-Eintrag.</summary>
    public static bool IsLocked(ModInfo mod, string relativePath) => mod.IsLocked || LockedBy(mod, relativePath) is not null;
}

public enum LockReasonKind
{
    /// <summary>Die ganze Mod ist gesperrt.</summary>
    WholeMod,

    /// <summary>Der Pfad selbst hat einen Sperr-Eintrag.</summary>
    ThisPath,

    /// <summary>Ein Ordner darüber hat einen Sperr-Eintrag.</summary>
    ParentFolder,
}

/// <summary>Grund einer Sperre; <paramref name="ByPath"/> ist der Sperr-Eintrag (bei <see cref="LockReasonKind.WholeMod"/> leer).</summary>
public sealed record LockReason(LockReasonKind Kind, string? ByPath);
