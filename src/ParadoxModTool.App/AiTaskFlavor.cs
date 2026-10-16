using System.Windows;
using ParadoxModTool.Patches.Ai;

namespace ParadoxModTool.App;

/// <summary>
/// Was das KI-Fenster vom Aufrufer braucht: Spiel, bekannte Mods (Name und Ordner), die aktive Mod und den Spielordner.
/// <see cref="GameRoot"/> darf nachfragen (Spiel-Exe wählen); das Fenster ruft es nur bei Bedarf auf.
/// </summary>
internal sealed record AiTaskHost(
    string GameId,
    Func<IReadOnlyList<(string Name, string Path)>> KnownMods,
    Func<string?> ActiveModRoot,
    Func<string?> GameRoot)
{
    /// <summary>
    /// false: <see cref="GameRoot"/> fragt nie nach und darf jederzeit aufgerufen werden; ohne Spielordner ist dann der Umschalter "Spieldateien"
    /// gleich gesperrt (mit Tooltip), statt erst beim Anklicken etwas zu melden.
    /// </summary>
    public bool GameRootMayAsk { get; init; } = true;
}

/// <summary>Eingaben aus dem Reiter "Auftrag"; Mod- und Spieldateien getrennt, je relativ zu ihrem Ordner und sortiert.</summary>
internal sealed record AiTaskInput(
    string Description,
    string? ModRoot,
    string? GameRoot,
    IReadOnlyList<string> ModFiles,
    IReadOnlyList<string> GameFiles,
    IReadOnlyList<AiTaskSnippet> Snippets,
    bool WithOpenItem);

/// <summary>
/// Ergebnis von "Übernehmen" oder "Aus Ordner importieren". <see cref="Messages"/> füllt die Meldungsliste; <see cref="Problem"/> färbt alle Zeilen rot;
/// <see cref="Feedback"/> geht in "Fehler für die KI kopieren" (ohne Angabe: bei einem Problem alle Meldungen, sonst nichts).
/// <see cref="Status"/> ist eine kurze Zeile unter der Meldungsliste; leer zeigt nichts.
/// </summary>
internal sealed record AiTaskOutcome(bool Done, IReadOnlyList<string> Messages, string Status)
{
    public bool Problem { get; init; }

    public IReadOnlyList<string>? Feedback { get; init; }
}

/// <summary>
/// Die Art des KI-Fensters: Texte, Hilfe, Auftrag bauen und Antwort übernehmen. Das Fenster selbst (<see cref="AiTaskWindow"/>) ist für alle Arten gleich.
/// </summary>
internal interface IAiTaskFlavor
{
    string Title { get; }

    string TaskIntro { get; }

    string ResultIntro { get; }

    string DescriptionTip { get; }

    string WithOpenText { get; }

    string WithOpenTip { get; }

    string SaveFileName { get; }

    // Tooltips und Meldungen, die von der Art abhängen (Paket oder Rezept); alles andere im Fenster ist für alle Arten gleich.
    string ModTip { get; }

    string ChooseModMessage { get; }

    string FilesTip { get; }

    string CopyTextTip { get; }

    string SaveFolderTip { get; }

    string AnswerTip { get; }

    string ApplyTip { get; }

    string ImportFolderTip { get; }

    string SnippetsIntro { get; }

    string SnippetWholeFileTip { get; }

    string SnippetExcerptTip { get; }

    string SnippetNoteTip { get; }

    /// <summary>Meldung (und Tooltip am gesperrten Umschalter), wenn kein Spielordner bekannt ist.</summary>
    string NoGameFolder { get; }

    /// <summary>Die Dateiliste kann zwischen Mod- und Spieldateien umschalten.</summary>
    bool OffersGameFiles { get; }

    /// <summary>Ob "Offenes ... mitgeben" für diese Mod möglich ist.</summary>
    bool CanHandOver(string? modRoot);

    void ShowHelp(Window owner);

    string BuildText(AiTaskInput input);

    void BuildFolder(AiTaskInput input, string targetFolder);

    /// <summary>Antwort übernehmen; <paramref name="withOpen"/> ist der Stand von "Offenes ... mitgeben" beim Klick (zählt beim Import, nicht beim Bauen des Auftrags).</summary>
    Task<AiTaskOutcome> ApplyAnswerAsync(Window owner, string answer, string? modRoot, bool withOpen);

    /// <summary>Ordner importieren; <paramref name="withOpen"/> wie bei <see cref="ApplyAnswerAsync"/>.</summary>
    Task<AiTaskOutcome> ImportFolderAsync(Window owner, string folder, string? modRoot, bool withOpen);

    string Feedback(IEnumerable<string> messages);
}
