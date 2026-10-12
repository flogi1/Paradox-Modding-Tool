namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>
/// Tippen in mehrsprachige Zellen eines Gitters (Konstanten-Tabelle): je Zelle steht die bearbeitete Sprache bei der ersten Änderung fest, wie bei
/// <see cref="LocalizedTextEditor"/>. Sonst fiele nach dem Leeren einer Zelle die Sprache weg und die nächste Eingabe änderte Englisch.
/// </summary>
public sealed class LocalizedCellLanguages
{
    private readonly Dictionary<(object Row, string Column), string> _languages = new();

    /// <summary>Neuer Wert der Zelle <paramref name="row"/>/<paramref name="column"/> (Zeile über ihre Referenz) nach dem Tippen von <paramref name="text"/>.</summary>
    public LocalizedText Type(object row, string column, LocalizedText current, string text, string uiLanguage)
    {
        if (!_languages.TryGetValue((row, column), out var language))
        {
            language = current.ResolvedLanguage(uiLanguage);
            _languages[(row, column)] = language;
        }

        return current.With(language, text);
    }

    /// <summary>Die Zelle wurde schon mehrsprachig bearbeitet; auch nach dem Leeren (Wert dann ohne Sprache) bleibt sie mehrsprachig.</summary>
    public bool IsMultilingual(object row, string column) => _languages.ContainsKey((row, column));

    /// <summary>Vergisst alle Sprachen (nach dem Sprachen-Dialog einer Zelle oder dem Umbenennen einer Spalte).</summary>
    public void Forget() => _languages.Clear();
}
