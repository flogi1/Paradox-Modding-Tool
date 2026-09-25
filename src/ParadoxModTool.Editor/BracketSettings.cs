namespace ParadoxModTool.Editor;

/// <summary>
/// Schalter für die Klammer-Markierung im Editor. Gilt wie <see cref="ScopeSettings"/> für alle Arbeitsbereiche eines Fensters:
/// das Hauptfenster setzt den Schalter, die Editoren lesen ihn bei Bedarf.
/// </summary>
public static class BracketSettings
{
    /// <summary>Färbt die Klammer am Cursor und ihr Gegenstück ein. Standard: an.</summary>
    public static bool HighlightEnabled { get; private set; } = true;

    public static event EventHandler? Changed;

    public static void SetHighlightEnabled(bool enabled)
    {
        if (HighlightEnabled != enabled)
        {
            HighlightEnabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
