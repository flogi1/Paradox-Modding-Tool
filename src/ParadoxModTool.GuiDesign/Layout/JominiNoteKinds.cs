using System.Collections.Concurrent;
using ParadoxModTool.GuiDesign.Checks;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Nur Jomini: welche Hinweistexte echte Probleme nennen (Textur, Schrift, Text-Icon, Lokalisierungsschlüssel, Frame, Spielfehler). Die Erzeuger der
/// Hinweise reichen Listen von Texten weiter und kopieren sie; die Art wandert deshalb nicht mit. <see cref="Tag"/> merkt sie sich zum Text, die Engine
/// trägt sie nach dem Layout in <see cref="LayoutBox.NoteKinds"/> ein (<see cref="Apply"/>). Klassisch trägt seine Arten selbst ein und nutzt dies nicht.
/// </summary>
internal static class JominiNoteKinds
{
    private static readonly ConcurrentDictionary<string, GuiFindingKind> Kinds = new();

    /// <summary>Merkt <paramref name="kind"/> zum fertigen Hinweistext und gibt den Text unverändert zurück (zum Einsetzen in <c>notes.Add(...)</c>).</summary>
    public static string Tag(string text, GuiFindingKind kind)
    {
        Kinds[text] = kind;
        return text;
    }

    /// <summary>Trägt für jeden getaggten Text in <see cref="LayoutBox.Notes"/> die Art in <see cref="LayoutBox.NoteKinds"/> ein, im ganzen Kastenbaum.</summary>
    public static void Apply(LayoutBox box)
    {
        foreach (var note in box.Notes)
        {
            if (Kinds.TryGetValue(note, out var kind) && !box.NoteKinds.Contains(new LayoutNoteKind(kind, note)))
            {
                box.NoteKinds.Add(new LayoutNoteKind(kind, note));
            }
        }

        foreach (var child in box.Children)
        {
            Apply(child);
        }
    }
}
