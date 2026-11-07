using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Checks;

/// <summary>Führt die klassischen Prüfungen aus. Nur Dateien der Mod bekommen Befunde (Vanilla und Abhängigkeiten nie, Spec-Antiziel "Lärm").</summary>
public static class ClassicCheckRunner
{
    /// <summary>Die Prüfungen, die der Designer laufen lässt; spätere Prüfungen kommen hier dazu.</summary>
    public static IReadOnlyList<IClassicCheck> DefaultChecks { get; } = new IClassicCheck[] { new LayoutFindingsCheck(), new LosesToOtherCheck(), new LocalisationCheck(), new ReplacePathCheck(), new ScriptedGuiChecks(), new CustomGuiChecks() };

    /// <summary>Alle Befunde, sortiert nach Schwere, dann Zeile; leer, wenn die Datei nicht zur Mod gehört.</summary>
    public static IReadOnlyList<GuiFinding> Run(ClassicCheckContext context, IEnumerable<IClassicCheck> checks, CancellationToken cancellation)
    {
        if (!context.IsModFile)
        {
            return Array.Empty<GuiFinding>();
        }

        var findings = new List<GuiFinding>();
        foreach (var check in checks)
        {
            cancellation.ThrowIfCancellationRequested();

            // Jede Prüfung für sich: eine, die wirft (auch beim Aufzählen, Prüfungen nutzen yield), fällt weg und meldet einen Hinweis; die anderen bleiben.
            try
            {
                findings.AddRange(check.Run(context, cancellation).ToList());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                findings.Add(new GuiFinding(
                    GuiFindingKind.CheckFailed, GuiFindingSeverity.Hint, GuiTexts.FindingCheckFailed(check.GetType().Name, ex.Message), null, context.DocumentPath, 0));
            }
        }

        cancellation.ThrowIfCancellationRequested();
        return findings.OrderBy(f => f.Severity).ThenBy(f => f.Line).ToList();
    }
}

/// <summary>
/// Befunde aus den Hinweisen der klassischen Layout-Engine (<see cref="LayoutBox.NoteKinds"/>): fehlendes Sprite, fehlende Textur und Schrift sind
/// Fehler, Frame außerhalb, unbekannter Anker und unbekanntes Format Warnungen. Überstand (<see cref="LayoutBox.OutsideParent"/>,
/// <see cref="LayoutBox.OutsideScreen"/>) ist ein Hinweis, nur bei "Überstand markieren". Reine Annäherungen sind keine Befunde. Nur Elemente, die in
/// der geöffneten Datei stehen; je Element, Art und Text ein Befund (Raster und Knopftexte teilen ihr Element).
/// </summary>
public sealed class LayoutFindingsCheck : IClassicCheck
{
    public IEnumerable<GuiFinding> Run(ClassicCheckContext context, CancellationToken cancellation)
    {
        if (context.Scene is not { } scene)
        {
            yield break;
        }

        var seen = new HashSet<(GuiNode, GuiFindingKind, string)>();
        foreach (var item in scene.Boxes)
        {
            var box = item.Box;
            var node = box.Element.Origin.Node;
            if (!ReferenceEquals(node.Root, context.Document.Root))
            {
                continue;
            }

            foreach (var note in box.NoteKinds)
            {
                if (seen.Add((node, note.Kind, note.Text)))
                {
                    yield return new GuiFinding(note.Kind, SeverityOf(note.Kind), note.Text, node, context.DocumentPath, node.Line);
                }
            }

            if (context.OverflowEnabled && (box.OutsideParent || box.OutsideScreen))
            {
                var text = box.OutsideParent ? GuiTexts.FindingOutsideParent : GuiTexts.FindingOutsideScreen;
                if (seen.Add((node, GuiFindingKind.Overflow, text)))
                {
                    yield return new GuiFinding(GuiFindingKind.Overflow, GuiFindingSeverity.Hint, text, node, context.DocumentPath, node.Line);
                }
            }
        }
    }

    private static GuiFindingSeverity SeverityOf(GuiFindingKind kind) => kind switch
    {
        GuiFindingKind.SpriteMissing or GuiFindingKind.TextureMissing or GuiFindingKind.FontMissing => GuiFindingSeverity.Error,
        GuiFindingKind.Overflow => GuiFindingSeverity.Hint,
        _ => GuiFindingSeverity.Warning,
    };
}
