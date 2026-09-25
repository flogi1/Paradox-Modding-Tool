using ParadoxModTool.Editor.Gui;

namespace ParadoxModTool.Editor;

/// <summary>Auswahl für "Alle speichern" (Arbeitsbereich-Leiste, Strg+Umschalt+S).</summary>
public static class WorkspaceSave
{
    /// <summary>
    /// Alle geänderten Dokumente der Arbeitsbereiche, in der Reihenfolge Arbeitsbereich, dann Tab. Auch ein unsichtbarer Arbeitsbereich
    /// (Split View aus) zählt. Vanilla-.gui-Dateien (auch GUI-Entwürfe) fehlen: Sie werden nie direkt gespeichert, ein Entwurf geht nur
    /// über "In Mod übernehmen".
    /// </summary>
    public static IReadOnlyList<(EditorPane Pane, OpenDocument Document)> DocumentsToSave(IEnumerable<EditorPane> panes) =>
        panes.SelectMany(pane => pane.Documents.Select(document => (Pane: pane, Document: document)))
            .Where(x => x.Document.IsModified && !GuiSettings.IsVanillaGui(x.Document))
            .ToList();
}
