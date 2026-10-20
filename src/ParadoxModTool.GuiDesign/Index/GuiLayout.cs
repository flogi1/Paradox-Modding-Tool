namespace ParadoxModTool.GuiDesign.Index;

/// <summary>Format der .gui-Dateien: Jomini (CK III, EU V, Victoria 3) oder das klassische Format (HOI IV, EU IV: <c>guiTypes</c>, <c>containerWindowType</c>).</summary>
public enum GuiDialect
{
    Jomini,
    Classic,
}

/// <summary>
/// Wo ein Spiel seine .gui-Dateien hat und welche Definition gilt (Spielprofil, Abschnitt <c>gui</c>). <see cref="Contexts"/>: Kontextordner wie bei EU V
/// (<c>in_game</c>, <c>main_menu</c>, <c>loading_screen</c>); leer = flach, die Dateien liegen direkt in <c>gui/</c> (Crusader Kings III).
/// <see cref="FirstWins"/>: Bei Typen und Templates gewinnt innerhalb einer Ebene die zuerst geladene Datei (CK III, gemessen 2026-09-30), sonst die zuletzt
/// geladene (EU V). <see cref="ModsBeforeVanilla"/>: Bei FirstWins gehen die Mods vor Vanilla (CK III); <see langword="false"/> = Vanilla und Mods zählen
/// gemeinsam in Ladereihenfolge, Unterordner nach den Dateien des Ordners darüber (Victoria 3, gemessen 2026-10-11).
/// Nur klassisch: <see cref="SpriteFirstWins"/> gilt für Sprites (<c>spriteType</c>), <see cref="FirstWins"/> dort für Fenster; <see cref="CaseSensitive"/>
/// vergleicht Dateinamen in der Ladereihenfolge nach Groß-/Kleinschreibung (HOI IV); <see cref="VanillaRoots"/> nennt die Ordner der Installation, deren
/// <c>interface/</c> Vanilla ist (<c>""</c> = die Installation selbst, ein <c>/*</c> am Ende = jeder direkte Unterordner).
/// </summary>
public sealed record GuiLayout(
    IReadOnlyList<string> Contexts,
    bool FirstWins,
    bool ModsBeforeVanilla = true,
    GuiDialect Dialect = GuiDialect.Jomini,
    bool SpriteFirstWins = false,
    bool CaseSensitive = false,
    IReadOnlyList<string>? VanillaRoots = null)
{
    /// <summary>EU V: drei Kontextordner, zuletzt geladene Definition gewinnt. Gilt, wenn das Profil nichts angibt.</summary>
    public static GuiLayout Default { get; } = new(new[] { "in_game", "main_menu", "loading_screen" }, false);

    public bool IsFlat => Contexts.Count == 0;

    public bool IsClassic => Dialect == GuiDialect.Classic;
}
