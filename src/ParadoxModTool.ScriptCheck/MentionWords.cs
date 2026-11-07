namespace ParadoxModTool.ScriptCheck;

/// <summary>
/// Zerlegt Skript-Token in Wörter, an denen erkannt wird, ob ein Name erwähnt wird (Skriptwerte in Verbundwerten wie
/// <c>value:name|P|1|</c>, <c>script_value:name</c>, <c>@[ name * 2 ]</c>, <c>scope:name</c>, Event-Ids wie <c>ns.12</c>).
/// </summary>
public static class MentionWords
{
    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.';

    /// <summary>
    /// Das Token selbst, dann seine Teile, getrennt an jedem Zeichen, das kein Buchstabe, keine Ziffer, kein <c>_</c> und kein <c>.</c> ist;
    /// enthält ein Teil einen Punkt, kommen auch dessen Stücke dazu. Keine leeren Teile, Reihenfolge frei, Doppelte möglich.
    /// </summary>
    public static IEnumerable<string> Of(string token)
    {
        if (string.IsNullOrEmpty(token)) yield break;
        yield return token;
        int start = -1;
        for (int i = 0; i <= token.Length; i++)
        {
            bool word = i < token.Length && IsWordChar(token[i]);
            if (word)
            {
                if (start < 0) start = i;
                continue;
            }
            if (start < 0) continue;
            foreach (var w in Parts(token.AsSpan(start, i - start).ToString())) yield return w;
            start = -1;
        }
    }

    private static IEnumerable<string> Parts(string part)
    {
        yield return part;
        if (part.IndexOf('.') < 0) yield break;
        foreach (var piece in part.Split('.', StringSplitOptions.RemoveEmptyEntries)) yield return piece;
    }

    /// <summary>
    /// Schneller Vorfilter über den Rohtext ohne Parsen: <see langword="true"/>, wenn ein Wort aus dem Text einem der Namen entsprechen könnte.
    /// Falsch-Positive sind erlaubt, Falsch-Negative nicht.
    /// </summary>
    public static bool MayMention(string text, IReadOnlySet<string> names)
    {
        if (names.Count == 0 || string.IsNullOrEmpty(text)) return false;
        // Namen mit Sonderzeichen können nur als ganzes Token passen, das sich nicht über Wortläufe finden lässt.
        foreach (var name in names)
            if (name.Any(c => !IsWordChar(c)) && text.Contains(name, StringComparison.Ordinal)) return true;

        int start = -1;
        for (int i = 0; i <= text.Length; i++)
        {
            bool word = i < text.Length && IsWordChar(text[i]);
            if (word)
            {
                if (start < 0) start = i;
                continue;
            }
            if (start < 0) continue;
            var run = text.Substring(start, i - start);
            start = -1;
            foreach (var w in Parts(run))
                if (names.Contains(w)) return true;
        }
        return false;
    }
}
