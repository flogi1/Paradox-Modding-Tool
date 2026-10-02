namespace ParadoxModTool.GuiDesign.Blocks;

/// <summary>Herkunft eines Bausteins: mitgeliefert (Programmordner), eigener für alle Mods des Spiels oder nur für eine Mod.</summary>
public enum GuiBlockLayer
{
    Shipped,
    Game,
    Mod,
}

public enum GuiBlockParameterKind
{
    Text,
    Modifier,
}

public enum GuiBlockErrorKind
{
    MissingBlock,
    DuplicateBlock,
    BlockNotFirst,
    InvalidHeader,
    InvalidKey,
    UndeclaredParent,
    DuplicateKey,
    UnknownPlaceholder,
    OutsideRepeat,
    MissingEnd,
    MismatchedEnd,
    LoneDollar,
    DuplicateName,
    Unreadable,
}

/// <summary>Fehler eines Bausteins. <see cref="Line"/> ist 1-basiert über die ganze Datei, 0 heißt "ganze Datei".</summary>
public sealed record GuiBlockError(int Line, GuiBlockErrorKind Kind, string Detail)
{
    public string Message => GuiTexts.BlocksError(Kind, Detail);
}

internal enum GuiBlockTokenKind
{
    Text,
    Placeholder,
    RepeatStart,
    RepeatEnd,
    InvalidKey,
    LoneDollar,
}

/// <summary>Stück einer Rumpfzeile aus <see cref="GuiBlockParser.Tokenize"/>; <see cref="Value"/> ist Text, Schlüssel bzw. Rest der Zeile.</summary>
internal sealed record GuiBlockToken(GuiBlockTokenKind Kind, string Value);

/// <summary>Stück eines Rumpfs oder einer Vorgabe: fester Text, Platzhalter oder Wiederholungsabschnitt.</summary>
public abstract record GuiBlockPart;

public sealed record GuiBlockText(string Text) : GuiBlockPart;

public sealed record GuiBlockPlaceholder(string Key, int Line) : GuiBlockPart;

public sealed record GuiBlockSection(string RepeatKey, IReadOnlyList<GuiBlockPart> Parts, int Line) : GuiBlockPart;

/// <summary>Parameter mit vollem Schlüssel (<c>tabs.id</c>); <see cref="DefaultParts"/> ist die zerlegte Vorgabe.</summary>
public sealed record GuiBlockParameter(string Key, string Label, string Default, GuiBlockParameterKind Kind, int Line)
{
    public IReadOnlyList<GuiBlockPart> DefaultParts { get; init; } = Array.Empty<GuiBlockPart>();
}

public sealed record GuiBlockRepeat(
    string Key,
    string Label,
    int DefaultCount,
    IReadOnlyList<GuiBlockParameter> Parameters,
    IReadOnlyList<GuiBlockRepeat> Repeats,
    int Line);

/// <summary>
/// Ein GUI-Baustein aus einer <c>*.guiblock</c>-Datei (siehe docs/superpowers/specs/2026-09-27-gui-building-blocks-design.md). Unveränderlich;
/// <see cref="Parameters"/> und <see cref="Repeats"/> enthalten nur die oberste Ebene.
/// </summary>
public sealed class GuiBlock
{
    public GuiBlock(
        string name,
        string group,
        string description,
        IReadOnlyList<GuiBlockParameter> parameters,
        IReadOnlyList<GuiBlockRepeat> repeats,
        IReadOnlyList<string> requires,
        IReadOnlyList<GuiBlockPart> body,
        GuiBlockLayer layer,
        string? sourcePath,
        IReadOnlyList<GuiBlockError> errors)
    {
        Name = name;
        Group = group;
        Description = description;
        Parameters = parameters;
        Repeats = repeats;
        Requires = requires;
        Body = body;
        Layer = layer;
        SourcePath = sourcePath;
        Errors = errors;
    }

    public string Name { get; }

    public string Group { get; }

    public string Description { get; }

    public IReadOnlyList<GuiBlockParameter> Parameters { get; }

    public IReadOnlyList<GuiBlockRepeat> Repeats { get; }

    public IReadOnlyList<string> Requires { get; }

    public IReadOnlyList<GuiBlockPart> Body { get; }

    public GuiBlockLayer Layer { get; }

    public string? SourcePath { get; }

    public IReadOnlyList<GuiBlockError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    /// <summary>Eingebaut (ohne Datei), z.B. "Leere Datei".</summary>
    public bool IsBuiltIn => SourcePath is null;

    public GuiBlockRepeat? FindRepeat(string key) => Find(Repeats, key);

    /// <summary>Kopie mit zusätzlichen Fehlern (für Fehler, die erst die Bibliothek erkennt).</summary>
    public GuiBlock WithErrors(IEnumerable<GuiBlockError> more) =>
        new(Name, Group, Description, Parameters, Repeats, Requires, Body, Layer, SourcePath, Errors.Concat(more).ToList());

    private static GuiBlockRepeat? Find(IReadOnlyList<GuiBlockRepeat> repeats, string key)
    {
        foreach (var repeat in repeats)
        {
            if (repeat.Key == key)
            {
                return repeat;
            }

            if (key.StartsWith(repeat.Key + ".", StringComparison.Ordinal) && Find(repeat.Repeats, key) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }
}
