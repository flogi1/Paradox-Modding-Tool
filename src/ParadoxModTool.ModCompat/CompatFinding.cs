using ParadoxModTool.Mods;

namespace ParadoxModTool.ModCompat;

public enum CompatSeverity
{
    Info,
    Hint,
    Conflict,
}

public enum CompatKind
{
    FileOverride,
    FileIdentical,
    KeyIdentical,
    KeyCombinable,
    KeyLoss,
    KeyConflict,
    RedundantOverride,
    TooCoarse,
    AdditiveOnly,
    ValueChangeOnly,
    ReplaceNeeded,
    Unreadable,
    MissingFolder,
    ReplacePathNoEffect,
    ReplacePathGuiCrash,
    PrefixNoEffect,
    PrefixActsAsReplace,
}

public enum CompatSuggestion
{
    None,
    NeedsCompatch,
    ReviewBothSides,
    UseInject,
    UseReplace,
    ProvideOnlyChanged,
    RemoveRedundant,
}

/// <summary>Ein Ergebnis der Prüfung. Hält alle Beiträge mit Fundstelle, damit später ein Kompatch-Erzeuger (Stufe C) beide Seiten kennt.</summary>
public sealed class CompatFinding
{
    public required CompatSeverity Severity { get; init; }

    public required CompatKind Kind { get; init; }

    public required string RelativePath { get; init; }

    /// <summary>Der Schlüssel; leer bei Fund auf Dateiebene.</summary>
    public string Key { get; init; } = string.Empty;

    public IReadOnlyList<CompatContribution> Contributions { get; init; } = Array.Empty<CompatContribution>();

    public CompatParticipant? Winner { get; init; }

    public CompatSuggestion Suggestion { get; init; }

    public required string Message { get; init; }

    /// <summary>Die Regel oder Empfehlung ist nicht durch das Wiki gesichert.</summary>
    public bool Uncertain { get; init; }

    public string? Note { get; init; }

    internal CompatFinding With(CompatSeverity severity, string? note) => new()
    {
        Severity = severity,
        Kind = Kind,
        RelativePath = RelativePath,
        Key = Key,
        Contributions = Contributions,
        Winner = Winner,
        Suggestion = Suggestion,
        Message = Message,
        Uncertain = Uncertain,
        Note = note ?? Note,
    };
}

/// <summary>Eingabe einer Prüfung. Die Teilnehmer nach Rang, Vanilla mit Rang 0 (<see cref="CompatParticipant.IsVanilla"/>).</summary>
public sealed class CompatRequest
{
    public required IReadOnlyList<CompatParticipant> Participants { get; init; }

    public required OverrideRules Rules { get; init; }

    /// <summary>"Mod 1": jeder Fund muss sie enthalten. <see langword="null"/> = alle gegen alle.</summary>
    public string? FocusId { get; init; }

    /// <summary>Die Gegenseite (IDs); <see langword="null"/> = alle anderen Mods.</summary>
    public IReadOnlyCollection<string>? CounterpartIds { get; init; }

    public ParseCache Cache { get; init; } = new();

    /// <summary>
    /// Ordner, deren Vanilla-Dateien eine Mod des Sets mit <c>replace_path</c> ausblendet (siehe <see cref="ParadoxModTool.Mods.ReplacePaths"/>).
    /// Das Spiel lädt diese Dateien nicht; sie zählen nicht als Grundlage einer Mod-Datei.
    /// </summary>
    public IReadOnlyCollection<string> HiddenVanillaFolders { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Je Teilnehmer (Id) die Ordner, deren Dateien dieses Teilnehmers eine später ladende Mod mit <c>replace_path</c> ausblendet (Crusader Kings III
    /// und Europa Universalis V, gemessen 2026-09-30). Leer bei Spielen, in denen <c>replace_path</c> nur Vanilla betrifft.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> HiddenModFolders { get; init; } = new Dictionary<string, IReadOnlyCollection<string>>();

    /// <summary>Wurzelordner, die <c>replace_path</c> nicht mitnennt (siehe <c>GameProfile.ReplacePathRoots</c>).</summary>
    public IReadOnlyCollection<string> ReplacePathRoots { get; init; } = Array.Empty<string>();

    /// <summary>Je Mod (Id) ihre <c>replace_path</c>-Einträge, wie sie in den Metadaten stehen (auch die, die das Spiel nicht beachtet).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ReplacePathEntries { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Ordner, in deren Unterordnern ein <c>replace_path</c> mit .gui-Dateien das Spiel beim Start abstürzen lässt (siehe <c>GameProfile.ReplacePathGuiCrashRoots</c>).</summary>
    public IReadOnlyCollection<string> ReplacePathGuiCrashRoots { get; init; } = Array.Empty<string>();

    /// <summary>Lädt das Spiel diese Datei des Teilnehmers nicht, weil ein <c>replace_path</c> sie ausblendet?</summary>
    public bool IsHidden(CompatParticipant participant, string relativePath) =>
        participant.IsVanilla
            ? ParadoxModTool.Mods.ReplacePaths.Hides(HiddenVanillaFolders, relativePath, ReplacePathRoots)
            : HiddenModFolders.TryGetValue(participant.Id, out var folders) && ParadoxModTool.Mods.ReplacePaths.Hides(folders, relativePath, ReplacePathRoots);

    /// <summary>
    /// Für jede Mod die <c>replace_path</c>-Ordner aller Mods, die nach ihr laden (höherer Rang): Deren Ausblendung trifft die frühere Mod, die eigene
    /// und frühere nicht.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> HiddenFoldersOfEarlierMods(
        IEnumerable<(string Id, int Rank)> mods,
        IReadOnlyDictionary<string, IReadOnlyList<string>> replacePaths)
    {
        var list = mods.ToList();
        return list.ToDictionary(
            mod => mod.Id,
            mod => (IReadOnlyCollection<string>)list
                .Where(other => other.Rank > mod.Rank)
                .SelectMany(other => replacePaths.GetValueOrDefault(other.Id) ?? Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    /// <summary>Beziehung zweier Mods im Register (Dev-Branch, Submod); <see langword="null"/> = alle unabhängig.</summary>
    public Func<CompatParticipant, CompatParticipant, ModRelationKind>? Relation { get; init; }

    /// <summary>Ist der erste Teilnehmer ein Kompatch für den zweiten? <see langword="null"/> = keine Kompatchs bekannt.</summary>
    public Func<CompatParticipant, CompatParticipant, bool>? IsCompatchFor { get; init; }
}

public sealed class CompatResult
{
    public CompatResult(IReadOnlyList<CompatFinding> findings)
    {
        Findings = findings;
    }

    public IReadOnlyList<CompatFinding> Findings { get; }
}
