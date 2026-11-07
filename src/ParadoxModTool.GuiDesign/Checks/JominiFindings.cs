using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Checks;

/// <summary>
/// Befunde der Jomini-Vorschau (CK III, Victoria 3, EU V) aus den Kästen der Szene: Hinweise mit Art (<see cref="LayoutBox.NoteKinds"/>) sind Fehler,
/// alle anderen Hinweise (<see cref="LayoutBox.Notes"/>, Annäherungen) <see cref="GuiFindingKind.PreviewNote"/> als Hinweis. Überstand ist ein Hinweis, nur bei
/// "Überstand markieren". Je Element, Art und Text ein Befund (Listenmuster wiederholen Kästen). Reihenfolge: Schwere, dann Dateireihenfolge.
/// </summary>
public static class JominiFindings
{
    public static IReadOnlyList<GuiFinding> Collect(GuiSchematicScene scene, bool overflowEnabled, CancellationToken cancellation)
    {
        var findings = new List<GuiFinding>();
        // Datei je Syntaxknoten: der wählbare Knoten ist die Herkunft eines Kastens der Szene.
        var files = new Dictionary<GuiNode, string?>();
        foreach (var item in scene.Boxes)
        {
            files.TryAdd(item.Box.Element.Origin.Node, item.Box.Element.Origin.FilePath);
        }

        var seen = new HashSet<(GuiNode?, GuiFindingKind, string)>();
        foreach (var item in scene.Boxes)
        {
            cancellation.ThrowIfCancellationRequested();
            var box = item.Box;
            var node = scene.SelectableNode(box);
            var path = node is not null && files.TryGetValue(node, out var file) ? file : null;

            void Add(GuiFindingKind kind, GuiFindingSeverity severity, string text)
            {
                if (seen.Add((node, kind, text)))
                {
                    findings.Add(new GuiFinding(kind, severity, text, node, path, node?.Line ?? 0));
                }
            }

            foreach (var note in box.NoteKinds)
            {
                Add(note.Kind, GuiFindingSeverity.Error, note.Text);
            }

            foreach (var note in box.Notes)
            {
                if (!box.NoteKinds.Any(tagged => tagged.Text == note))
                {
                    Add(GuiFindingKind.PreviewNote, GuiFindingSeverity.Hint, note);
                }
            }

            if (overflowEnabled && (box.OutsideParent || box.OutsideScreen))
            {
                Add(GuiFindingKind.Overflow, GuiFindingSeverity.Hint, box.OutsideParent ? GuiTexts.FindingOutsideParent : GuiTexts.FindingOutsideScreen);
            }
        }

        cancellation.ThrowIfCancellationRequested();
        return findings.OrderBy(f => f.Severity).ToList();
    }
}
