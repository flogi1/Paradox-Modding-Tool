namespace ParadoxModTool.Generators.Editing;

/// <summary>Vorschlag eines Bilds zum Schlüssel des Arbeitsstands (Wappen zum Tag), wie im Python-Generator.</summary>
public static class ImageSuggest
{
    /// <summary>
    /// Erst ein Eintrag, dessen Dateiname (ohne Ordner und Endung) genau der Schlüssel ist, dann einer, der mit <c>SCHLÜSSEL_</c> beginnt, dann einer, der
    /// ihn enthält; Groß-/Kleinschreibung egal, in der Reihenfolge der Bibliothek. <see langword="null"/> ohne Treffer oder ohne Schlüssel.
    /// </summary>
    public static string? ForKey(IReadOnlyList<string> entries, string? key)
    {
        var wanted = key?.Trim();
        if (string.IsNullOrEmpty(wanted))
        {
            return null;
        }

        string Name(string entry) => Path.GetFileNameWithoutExtension(entry.Replace('\\', '/').Split('/')[^1]);

        return entries.FirstOrDefault(e => Name(e).Equals(wanted, StringComparison.OrdinalIgnoreCase))
            ?? entries.FirstOrDefault(e => Name(e).StartsWith(wanted + "_", StringComparison.OrdinalIgnoreCase))
            ?? entries.FirstOrDefault(e => Name(e).Contains(wanted, StringComparison.OrdinalIgnoreCase));
    }
}
