using ParadoxModTool.Patches;
using ParadoxModTool.UndoRedo;

namespace ParadoxModTool.Generators;

/// <summary>
/// Macht aus einem Plan einen Rückgängig-Schritt (neue und geänderte Texte und Binärdateien) und prüft, ob sich Dateien seit der Vorschau geändert haben.
/// Vor dem Überschreiben einer externen Datei legt der Schritt eine Sicherung im Ordner <c>backups</c> neben ihr an (<see cref="ExternalBackupAction"/>).
/// </summary>
public static class GeneratorPlanWriter
{
    /// <param name="stamp">Zeitstempel im Namen der Sicherungen; ohne Angabe jetzt.</param>
    public static CompositeUndoableAction CreateAction(GeneratorPlan plan, string description, DateTime? stamp = null)
    {
        var time = stamp ?? DateTime.Now;
        var actions = new List<IUndoableAction>();
        foreach (var file in ToWrite(plan))
        {
            var encoding = TextFile.EncodingFor(file.HasBom, file.EncodingName);
            if (file.ExternalPath is { } external && file.OriginalText is not null)
            {
                // Die Sicherung zuerst: bricht das Schreiben ab, bleibt der alte Stand erhalten (Namensschema wie beim Tag-Editor)
                actions.Add(new ExternalBackupAction(description, external, time));
            }

            actions.Add(file.Bytes is { } bytes
                ? new BinaryWriteUndoableAction(description, FullPath(plan, file), bytes)
                : new FileWriteUndoableAction(description, FullPath(plan, file), file.NewText ?? string.Empty, encoding));
        }

        return new CompositeUndoableAction(description, actions);
    }

    /// <summary>Dateien, deren Stand am Schreibort nicht mehr dem der Vorschau entspricht; dann darf der Plan nicht geschrieben werden.</summary>
    public static IReadOnlyList<string> StaleFiles(GeneratorPlan plan)
    {
        var stale = new List<string>();
        foreach (var file in ToWrite(plan))
        {
            var path = FullPath(plan, file);
            var exists = File.Exists(path);
            var changed = file.Bytes is not null
                ? exists != (file.OriginalBytes is not null) || (exists && !File.ReadAllBytes(path).AsSpan().SequenceEqual(file.OriginalBytes))
                : (exists ? TextFile.Read(path).Text : null) != file.OriginalText;
            if (changed)
            {
                stale.Add(file.RelativePath);
            }
        }

        return stale;
    }

    /// <summary>Volle Pfade aller Dateien, die der Plan schreibt (für die Prüfung auf ungespeicherte offene Dokumente und das Neuladen).</summary>
    public static IReadOnlyList<string> WrittenPaths(GeneratorPlan plan) => ToWrite(plan).Select(f => FullPath(plan, f)).ToList();

    private static IEnumerable<GeneratorPlannedFile> ToWrite(GeneratorPlan plan) =>
        plan.Files.Where(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed);

    private static string FullPath(GeneratorPlan plan, GeneratorPlannedFile file) =>
        file.ExternalPath ?? Path.Combine(plan.WriteRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
}
