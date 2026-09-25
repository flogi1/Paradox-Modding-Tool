namespace ParadoxModTool.GuiDesign.Syntax;

/// <summary>Halboffener Textbereich [Start, End) im Dokumenttext.</summary>
public readonly record struct TextSpan(int Start, int End)
{
    public int Length => End - Start;

    public bool Contains(TextSpan other) => other.Start >= Start && other.End <= End;

    public bool Contains(int offset) => offset >= Start && offset < End;
}

public enum GuiTokenKind
{
    Word,
    String,
    Operator,
    Open,
    Close,
    Comment,
    Eof,
}

/// <summary>Token mit Position. Bei <see cref="GuiTokenKind.String"/> ist <c>Text</c> der Inhalt ohne Anführungszeichen, die Spanne schließt sie ein.</summary>
public readonly record struct GuiToken(GuiTokenKind Kind, string Text, int Start, int End, int Line);

public enum GuiDiagnosticKind
{
    UnexpectedCloseBrace,
    MissingCloseBrace,
    UnterminatedString,
    MissingValue,
    UnexpectedToken,
}

public sealed record GuiDiagnostic(GuiDiagnosticKind Kind, int Start, int End, int Line, string Message);
