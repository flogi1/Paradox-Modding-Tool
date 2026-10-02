namespace ParadoxModTool.GuiDesign.Index;

/// <summary>
/// Wo ein Spiel seine .gui-Dateien hat und welche Definition gilt (Spielprofil, Abschnitt <c>gui</c>). <see cref="Contexts"/>: Kontextordner wie bei EU V
/// (<c>in_game</c>, <c>main_menu</c>, <c>loading_screen</c>); leer = flach, die Dateien liegen direkt in <c>gui/</c> (Crusader Kings III).
/// <see cref="FirstWins"/>: Bei Typen und Templates gewinnt innerhalb einer Ebene die zuerst geladene Datei (CK III, gemessen 2026-09-30), sonst die zuletzt
/// geladene (EU V).
/// </summary>
public sealed record GuiLayout(IReadOnlyList<string> Contexts, bool FirstWins)
{
    /// <summary>EU V: drei Kontextordner, zuletzt geladene Definition gewinnt. Gilt, wenn das Profil nichts angibt.</summary>
    public static GuiLayout Default { get; } = new(new[] { "in_game", "main_menu", "loading_screen" }, false);

    public bool IsFlat => Contexts.Count == 0;
}
