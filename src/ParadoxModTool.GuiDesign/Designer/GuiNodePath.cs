using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// Weg von der Dateiwurzel zu einem Knoten (Art, Name und wievielter gleichartiger Geschwisterknoten). Damit bleibt die Auswahl über ein
/// Neu-Parsen hinweg erhalten, obwohl dabei alle Knoten neu entstehen.
/// </summary>
public sealed class GuiNodePath
{
    private GuiNodePath(IReadOnlyList<Step> steps) => Steps = steps;

    public readonly record struct Step(GuiNodeKind Kind, string Name, int Occurrence);

    public IReadOnlyList<Step> Steps { get; }

    public static GuiNodePath Of(GuiNode node)
    {
        var steps = new List<Step>();
        for (var current = node; current.Parent is not null; current = current.Parent)
        {
            var self = current;
            var occurrence = self.Parent!.Children
                .TakeWhile(c => !ReferenceEquals(c, self))
                .Count(c => c.Kind == self.Kind && c.Name == self.Name);
            steps.Add(new Step(self.Kind, self.Name, occurrence));
        }

        steps.Reverse();
        return new GuiNodePath(steps);
    }

    /// <summary>Knoten im neuen Dokument; fehlt ein Schritt, der tiefste noch gefundene Vorfahr; null, wenn schon der erste fehlt.</summary>
    public GuiNode? Find(GuiDocument document)
    {
        GuiNode? found = null;
        var current = document.Root;
        foreach (var step in Steps)
        {
            var next = current.Children
                .Where(c => c.Kind == step.Kind && c.Name == step.Name)
                .Skip(step.Occurrence)
                .FirstOrDefault();
            if (next is null)
            {
                return found;
            }

            found = next;
            current = next;
        }

        return found;
    }

    /// <summary>Knoten im neuen Dokument nur, wenn jeder Schritt gefunden wird (für Designer-Aktionen, die genau diesen Knoten brauchen); sonst null.</summary>
    public GuiNode? FindExact(GuiDocument document)
    {
        var current = document.Root;
        foreach (var step in Steps)
        {
            var next = current.Children
                .Where(c => c.Kind == step.Kind && c.Name == step.Name)
                .Skip(step.Occurrence)
                .FirstOrDefault();
            if (next is null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    public bool SameAs(GuiNodePath? other) => other is not null && Steps.SequenceEqual(other.Steps);
}
