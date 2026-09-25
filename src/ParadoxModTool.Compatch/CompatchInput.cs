using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Eingaben eines Kompatch-Laufs. Ordner sind Wurzelordner der Spielinhalte (Vanilla: <c>&lt;Spiel&gt;/game</c>).</summary>
public sealed class CompatchInput
{
    public required string VanillaFolder { get; init; }

    public required string ModAFolder { get; init; }

    public required string ModBFolder { get; init; }

    /// <summary><see langword="true"/> = B lädt nach A und gewinnt bei gleichem Pfad (Standard).</summary>
    public bool BLoadsAfterA { get; init; } = true;

    public required OverrideRules Rules { get; init; }

    /// <summary>Präfixe, die das Spiel kennt (siehe <c>GameProfile.KeyOperators</c>).</summary>
    public IReadOnlyCollection<string> KeyOperators { get; init; } = Array.Empty<string>();

    public CompatchFileFilter Filter { get; init; } = CompatchFileFilter.Default;

    /// <summary>Beim Ziel "Basis-Mod": PMT-eigene Patch-Dateien dieser Seite nicht lesen (sie sind der frühere Patch, nicht Teil der Mod).</summary>
    public MergeSide? ExcludeOwnPatchFilesOf { get; init; }

    /// <summary>Beim Ziel "Basis-Mod": die Hauptmod, also die Seite, die nicht das Ziel ist. <see langword="null"/> = keine Hauptmod-Optionen.</summary>
    public MergeSide? MainSide { get; init; }

    /// <summary>Option 1: nur gegen die Hauptmod vergleichen (Basis = Hauptmod statt Vanilla). Wirkt nur mit <see cref="MainSide"/>.</summary>
    public bool MainAsBase { get; init; }

    /// <summary>Option 2: Blöcke der Hauptmod übernehmen, die im Ziel ganz fehlen. Wirkt nur mit <see cref="MainSide"/>.</summary>
    public bool FillMissingFromMain { get; init; }

    /// <summary>Felder, deren Inhalt Trigger oder Effekte sind. Automatische Übernahmen darin bekommen die Stufe "prüfen".</summary>
    public IReadOnlySet<string> LogicFieldKeys { get; init; } = LogicFields.Default;

    public ParseCache Cache { get; init; } = new();
}

/// <summary>Standardliste der Logikfelder. <c>on_*</c> steht für alle Felder, die mit <c>on_</c> beginnen.</summary>
public static class LogicFields
{
    public static readonly IReadOnlySet<string> Default = new HashSet<string>(StringComparer.Ordinal)
    {
        "trigger", "potential", "allow", "is_shown", "is_valid", "is_possible", "can_start", "can_end",
        "effect", "immediate", "after", "limit", "ai_will_do", "ai_chance", "on_*",
    };

    public static bool IsLogic(string key, IReadOnlySet<string> keys) =>
        keys.Contains(key) || (key.StartsWith("on_", StringComparison.Ordinal) && keys.Contains("on_*"));

    private static readonly string[] LogicFolders = { "scripted_effects", "scripted_triggers", "on_action", "on_actions", "events" };

    /// <summary>Ordner, deren Einträge ganz aus Logik bestehen (Skript-Effekte/-Trigger, on_actions, Events), egal wie die Felder heißen.</summary>
    public static bool IsLogicScope(string scope) =>
        scope.Split('/').Any(segment => LogicFolders.Contains(segment, StringComparer.OrdinalIgnoreCase));
}

/// <summary>Die Vergleichsbasis des 3-Wege-Merges: Vanilla oder (Option 1) die Fassung der Hauptmod. Anlegen und Anwenden fragen hier nach.</summary>
public static class MergeBasis
{
    public static MergeSide BaseSide(CompatchInput input) =>
        input.MainAsBase && input.MainSide is { } main ? main : MergeSide.Vanilla;

    public static IReadOnlyList<ScriptEntry> Entries(AddressInfo info, CompatchInput input) => Of(info, input).Entries;

    public static string? FileHash(AddressInfo info, CompatchInput input) => Of(info, input).FileHash;

    private static Effective Of(AddressInfo info, CompatchInput input) => BaseSide(input) switch
    {
        MergeSide.A => info.A,
        MergeSide.B => info.B,
        _ => info.Vanilla,
    };
}

/// <summary>Die Eingaben passen nicht (z. B. Mod-Ordner fehlt). Die Meldung ist für den Nutzer gedacht.</summary>
public sealed class CompatchInputException : Exception
{
    public CompatchInputException(string message) : base(message)
    {
    }
}
