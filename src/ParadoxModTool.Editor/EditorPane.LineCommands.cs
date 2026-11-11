using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;

namespace ParadoxModTool.Editor;

public partial class EditorPane
{
    /// <summary>Zeile oder Auswahl duplizieren (Strg+D, wie in Notepad++).</summary>
    private static readonly RoutedCommand DuplicateLineCommand = new("DuplicateLine", typeof(EditorPane));

    /// <summary>
    /// AvalonEdit löscht mit Strg+D die Zeile. Nutzerwunsch: Strg+D soll wie in Notepad++ duplizieren; "Zeile löschen" wandert auf Strg+Umschalt+K.
    /// </summary>
    private void RebindLineCommands()
    {
        var editing = Editor.TextArea.DefaultInputHandler.Editing;
        foreach (var binding in editing.InputBindings.OfType<KeyBinding>().Where(b => b.Command == AvalonEditCommands.DeleteLine).ToList())
        {
            editing.InputBindings.Remove(binding);
        }

        editing.InputBindings.Add(new KeyBinding(AvalonEditCommands.DeleteLine, Key.K, ModifierKeys.Control | ModifierKeys.Shift));
        editing.InputBindings.Add(new KeyBinding(DuplicateLineCommand, Key.D, ModifierKeys.Control));
        editing.CommandBindings.Add(new CommandBinding(DuplicateLineCommand, (_, _) => DuplicateLine(Editor)));
    }

    /// <summary>
    /// Ohne Auswahl wird die Zeile des Cursors unter sich kopiert (Cursor wandert in die neue Zeile, gleiche Spalte); mit Auswahl wird die
    /// Auswahl direkt dahinter noch einmal eingefügt und markiert. Ein Schritt im Text-Undo.
    /// </summary>
    internal static void DuplicateLine(TextEditor editor)
    {
        var document = editor.Document;
        using (document.RunUpdate())
        {
            if (editor.TextArea.Selection.IsEmpty)
            {
                var line = document.GetLineByOffset(editor.CaretOffset);
                var column = editor.CaretOffset - line.Offset;
                var text = document.GetText(line.Offset, line.Length);
                if (line.DelimiterLength == 0)
                {
                    // Letzte Zeile ohne Zeilenende: Umbruch davor einfügen.
                    document.Insert(line.EndOffset, "\r\n" + text);
                    editor.CaretOffset = line.EndOffset + 2 + column;
                }
                else
                {
                    var lineBreak = document.GetText(line.EndOffset, line.DelimiterLength);
                    var next = line.Offset + line.TotalLength;
                    document.Insert(next, text + lineBreak);
                    editor.CaretOffset = next + column;
                }
            }
            else
            {
                var segment = editor.TextArea.Selection.SurroundingSegment;
                var text = document.GetText(segment.Offset, segment.Length);
                document.Insert(segment.EndOffset, text);
                editor.Select(segment.EndOffset, segment.Length);
            }
        }
    }
}
