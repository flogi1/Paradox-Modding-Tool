namespace ParadoxModTool.VanillaDiff;

/// <summary>Art eines Tokens der Paradox-Skriptsprache (siehe <see cref="ScriptTokenizer"/>).</summary>
public enum ScriptTokenKind
{
    /// <summary>Unquotiertes Wort: Bezeichner, Zahl, Datum, Bool-Literal (yes/no) usw.</summary>
    Identifier,

    /// <summary>Ein in doppelte Anführungszeichen gefasster String.</summary>
    String,

    /// <summary>Eines der Vergleichs-/Zuweisungs-Operatoren: = &lt; &gt; &lt;= &gt;=.</summary>
    Operator,

    OpenBrace,
    CloseBrace,

    /// <summary>Ein Kommentar (# bis Zeilenende), ohne die führende Raute.</summary>
    Comment,

    Eof
}

/// <summary>
/// Ein einzelnes lexikalisches Token samt Zeilennummer (1-basiert). Die Zeilennummer
/// wird beim Parsen genutzt, um zu erkennen, ob ein Kommentar auf derselben Zeile wie
/// die vorherige Anweisung steht (-> Trailing-Kommentar) oder in einer eigenen Zeile
/// davor (-> führender Kommentar der nächsten Anweisung).
/// </summary>
public readonly struct ScriptToken
{
    public ScriptTokenKind Kind { get; init; }

    /// <summary>
    /// Der Roh-Text des Tokens. Bei <see cref="ScriptTokenKind.String"/> bereits ohne die
    /// umschließenden Anführungszeichen. Bei <see cref="ScriptTokenKind.Comment"/> ohne die
    /// führende Raute, aber mit ggf. vorhandenem Leerzeichen danach getrimmt.
    /// </summary>
    public string Text { get; init; }

    public int Line { get; init; }

    public int Start { get; init; }

    /// <summary>Exklusives Ende (Zeichen-Offset direkt nach dem letzten Zeichen des Tokens).</summary>
    public int End { get; init; }

    public override string ToString() => $"{Kind}:'{Text}'@{Line}";
}
