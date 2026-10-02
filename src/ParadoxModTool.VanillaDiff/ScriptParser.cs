namespace ParadoxModTool.VanillaDiff;

/// <summary>Einstiegspunkt zum Parsen einer kompletten Paradox-Skriptdatei in einen AST.</summary>
public static class ScriptDocument
{
    /// <summary>
    /// Parst den Inhalt einer .txt/.gui/.gfx-Datei (Jomini-Skriptdialekt) in einen
    /// <see cref="ScriptBlock"/>, der die implizite Wurzelebene der Datei repräsentiert.
    /// Wirft <see cref="ScriptParseException"/> bei Syntaxfehlern (nicht geschlossene
    /// Klammern, nicht terminierte Strings, unerwartete Tokens).
    /// </summary>
    public static ScriptBlock Parse(string source)
    {
        var tokens = ScriptTokenizer.Tokenize(source);
        var parser = new ScriptParser(tokens, source);
        return parser.ParseRoot();
    }
}

/// <summary>
/// Rekursiver Abstiegsparser für den Jomini-Skriptdialekt. Baut aus der Token-Liste des
/// <see cref="ScriptTokenizer"/> einen strukturierten AST (<see cref="ScriptBlock"/> /
/// <see cref="ScriptStatement"/> / <see cref="ScriptListItem"/>) — die Grundlage für
/// <see cref="ScriptDiffer"/>, <see cref="MergeEngine"/> und <see cref="ScriptWriter"/>,
/// die alle auf der Struktur statt auf dem Rohtext arbeiten (Abschnitt 7 der Architektur-
/// Spezifikation: "Der Diff-Parser arbeitet auf dem strukturierten AST ... nicht auf Rohtext").
/// </summary>
internal sealed class ScriptParser
{
    private readonly List<ScriptToken> _tokens;
    private readonly string _source;
    private int _pos;

    /// <summary>Zeile des zuletzt konsumierten Nicht-Kommentar-Tokens, für die Trailing-Kommentar-Erkennung.</summary>
    private int _lastConsumedLine = -1;

    public ScriptParser(List<ScriptToken> tokens, string source)
    {
        _tokens = tokens;
        _source = source;
    }

    public ScriptBlock ParseRoot()
    {
        var entries = ParseEntries(isRoot: true);
        return new ScriptBlock { Entries = entries };
    }

    private ScriptToken Peek() => _tokens[_pos];

    private ScriptToken Advance()
    {
        var token = _tokens[_pos];
        if (token.Kind != ScriptTokenKind.Eof)
        {
            _pos++;
        }

        if (token.Kind != ScriptTokenKind.Comment)
        {
            _lastConsumedLine = token.Line;
        }

        return token;
    }

    private List<ScriptEntry> ParseEntries(bool isRoot)
    {
        var entries = new List<ScriptEntry>();
        var pendingComments = new List<string>();

        while (true)
        {
            var token = Peek();

            if (token.Kind == ScriptTokenKind.Eof)
            {
                if (!isRoot)
                {
                    throw new ScriptParseException(VanillaDiffTexts.UnclosedBrace, token.Line);
                }

                FlushPendingComments(entries, pendingComments);
                break;
            }

            if (token.Kind == ScriptTokenKind.CloseBrace)
            {
                if (isRoot)
                {
                    throw new ScriptParseException(VanillaDiffTexts.UnexpectedCloseBrace, token.Line);
                }

                FlushPendingComments(entries, pendingComments);
                break;
            }

            if (token.Kind == ScriptTokenKind.Comment)
            {
                Advance();

                if (entries.Count > 0 && token.Line == _lastConsumedLine)
                {
                    entries[^1] = WithTrailingComment(entries[^1], token.Text);
                }
                else
                {
                    pendingComments.Add(token.Text);
                }

                continue;
            }

            var entry = ParseEntry(pendingComments);
            pendingComments = new List<string>();
            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// Übernimmt am Ende eines Blocks (bzw. der Wurzelebene) noch übrig gebliebene, keinem
    /// weiteren Eintrag mehr zugeordnete Kommentarzeilen als eigenen <see cref="ScriptCommentEntry"/>
    /// in die Ergebnisliste, statt sie stillschweigend zu verwerfen (siehe Klassenkommentar dort).
    /// Ohne Wirkung, falls keine Kommentare mehr offen sind (der Normalfall).
    /// </summary>
    private static void FlushPendingComments(List<ScriptEntry> entries, List<string> pendingComments)
    {
        if (pendingComments.Count > 0)
        {
            entries.Add(new ScriptCommentEntry { LeadingComments = pendingComments });
        }
    }

    private ScriptEntry ParseEntry(List<string> leadingComments)
    {
        var entryStart = Peek().Start;
        var first = ParseValue();

        if (first is ScriptScalar scalar && Peek().Kind == ScriptTokenKind.Operator)
        {
            var op = ParseOperator();
            var value = ParseValue();
            var entryEnd = _tokens[_pos - 1].End;

            return new ScriptStatement
            {
                Key = scalar.Text,
                Operator = op,
                Value = value,
                LeadingComments = leadingComments,
                SourceText = _source,
                SourceStart = entryStart,
                SourceEnd = entryEnd
            };
        }

        var listEntryEnd = _tokens[_pos - 1].End;
        return new ScriptListItem
        {
            Value = first,
            LeadingComments = leadingComments,
            SourceText = _source,
            SourceStart = entryStart,
            SourceEnd = listEntryEnd
        };
    }

    private ScriptValue ParseValue()
    {
        var token = Peek();

        if (token.Kind == ScriptTokenKind.OpenBrace)
        {
            Advance();
            var entries = ParseEntries(isRoot: false);

            if (Peek().Kind != ScriptTokenKind.CloseBrace)
            {
                throw new ScriptParseException(VanillaDiffTexts.UnclosedBrace, token.Line);
            }

            Advance(); // "}"
            return new ScriptBlock { Entries = entries };
        }

        if (token.Kind == ScriptTokenKind.Identifier)
        {
            Advance();
            return new ScriptScalar { Text = token.Text, IsQuoted = false };
        }

        if (token.Kind == ScriptTokenKind.String)
        {
            Advance();
            return new ScriptScalar { Text = token.Text, IsQuoted = true };
        }

        throw new ScriptParseException(VanillaDiffTexts.UnexpectedToken(token.Text, token.Kind), token.Line);
    }

    private ScriptOperator ParseOperator()
    {
        var token = Advance();

        return token.Text switch
        {
            "=" => ScriptOperator.Equals,
            "<" => ScriptOperator.LessThan,
            "<=" => ScriptOperator.LessOrEqual,
            ">" => ScriptOperator.GreaterThan,
            ">=" => ScriptOperator.GreaterOrEqual,
            "?=" => ScriptOperator.EqualsIfExists,
            "!=" => ScriptOperator.NotEquals,
            _ => throw new ScriptParseException(VanillaDiffTexts.UnknownOperator(token.Text), token.Line)
        };
    }

    private static ScriptEntry WithTrailingComment(ScriptEntry entry, string comment) => entry switch
    {
        ScriptStatement stmt => new ScriptStatement
        {
            Key = stmt.Key,
            Operator = stmt.Operator,
            Value = stmt.Value,
            LeadingComments = stmt.LeadingComments,
            TrailingComment = Combine(stmt.TrailingComment, comment),
            SourceText = stmt.SourceText,
            SourceStart = stmt.SourceStart,
            SourceEnd = stmt.SourceEnd
        },
        ScriptListItem item => new ScriptListItem
        {
            Value = item.Value,
            LeadingComments = item.LeadingComments,
            TrailingComment = Combine(item.TrailingComment, comment),
            SourceText = item.SourceText,
            SourceStart = item.SourceStart,
            SourceEnd = item.SourceEnd
        },
        _ => entry
    };

    private static string Combine(string? existing, string addition) =>
        existing is null ? addition : $"{existing} {addition}";
}
