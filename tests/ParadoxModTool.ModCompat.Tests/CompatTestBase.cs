namespace ParadoxModTool.ModCompat.Tests;

/// <summary>Gemeinsame Bausteine für Analyse-Tests: Mods als Ordner im Temp-Verzeichnis, Standardregeln.</summary>
public abstract class CompatTestBase : TempDirTests
{
    protected static OverrideRules Rules() => new(new[]
    {
        new OverrideRule("*/common", OverrideBehavior.PerKey),
        new OverrideRule("*/common/defines", OverrideBehavior.PartialKeys),
        new OverrideRule("*/common/on_action", OverrideBehavior.AppendOnly),
        new OverrideRule("*/events", OverrideBehavior.FirstWins),
        new OverrideRule("*/gui", OverrideBehavior.LastWins),
        new OverrideRule("*/localization", OverrideBehavior.LocalizationKeys),
        new OverrideRule("*/gfx", OverrideBehavior.WholeFile),
    });

    /// <summary>Legt den Mod-Ordner an (leer) und liefert den Teilnehmer.</summary>
    protected CompatParticipant Mod(string name, int rank, string? modId = null) =>
        new(name, name, Make(name), rank, false, modId);

    /// <summary>Schreibt eine Datei in den Ordner der Mod.</summary>
    protected string Put(CompatParticipant mod, string relativePath, string content) =>
        Write(Path.GetFileName(mod.Folder) + "/" + relativePath, content);

    protected CompatParticipant Vanilla() => new("vanilla", "Vanilla", Make("vanilla"), 0, true);
}
