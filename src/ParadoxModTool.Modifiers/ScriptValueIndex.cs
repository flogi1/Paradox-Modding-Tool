using System.Globalization;

namespace ParadoxModTool.Modifiers;

/// <summary>
/// Name → Zahl für Script-Values (<c>common/script_values</c>) und Konstanten (<c>common/scripted_variables</c>, <c>@name</c>) von Spiel und Mod, damit der
/// Hover über <c>county_fertility_growth_mult = small_county_fertility_growth_gain</c> den Wert kennt. Aufgelöst werden nur Konstanten:
/// <c>name = 0.1</c>, <c>name = { value = 0.1 }</c>, Verweise auf andere Konstanten (<c>name = other</c>, <c>name = @konstante</c>) und Inline-Formeln
/// (<c>@konstante = @[ basis + 0.05 ]</c>, siehe <see cref="InlineMath"/>). Alles mit Formel im Block (<c>add</c>, <c>multiply</c>, <c>if</c> …) bleibt unaufgelöst. Die Mod überschreibt gleiche Namen des Spiels.
/// </summary>
public sealed class ScriptValueIndex
{
    private const int MaxReferenceDepth = 8;

    private static readonly string[] Folders =
    {
        "common/script_values",
        "common/scripted_variables",
        "in_game/common/script_values",
        "in_game/common/scripted_variables",
    };

    private readonly Dictionary<string, string?> _raw;

    private ScriptValueIndex(Dictionary<string, string?> raw) => _raw = raw;

    public static ScriptValueIndex Empty { get; } = new(new Dictionary<string, string?>(StringComparer.Ordinal));

    public int Count => _raw.Count;

    /// <summary>Die Zahl hinter dem Namen; <see langword="null"/>, wenn der Name unbekannt ist, eine Formel hat oder sich im Kreis verweist.</summary>
    public decimal? Find(string name) => Find(name, null);

    /// <summary>
    /// Wie <see cref="Find(string)"/>, aber mit den Einträgen der gerade offenen Datei zuerst (<see cref="Read"/>): Konstanten wie <c>@preis = 0.5</c> am Dateianfang
    /// gelten nur in ihrer Datei und stehen deshalb nicht im Index.
    /// </summary>
    public decimal? Find(string name, IReadOnlyDictionary<string, string?>? local) => Resolve(name, local, 0);

    private decimal? Resolve(string name, IReadOnlyDictionary<string, string?>? local, int depth)
    {
        var current = name;
        for (; depth <= MaxReferenceDepth; depth++)
        {
            string? raw = null;
            if (!(local is not null && local.TryGetValue(current, out raw)) && !_raw.TryGetValue(current, out raw))
            {
                return null;
            }

            if (raw is null)
            {
                return null;
            }

            if (decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            if (raw.StartsWith("@[", StringComparison.Ordinal) && raw.EndsWith(']'))
            {
                // Namen in der Formel stehen ohne "@", können aber Konstanten (@name) oder Script-Values meinen.
                var nextDepth = depth + 1;
                return InlineMath.Evaluate(raw[2..^1], id => Resolve("@" + id, local, nextDepth) ?? Resolve(id, local, nextDepth));
            }

            current = raw;
        }

        return null;
    }

    /// <summary>Liest erst die Spiel-, dann die Mod-Ordner; Dateien je Ordner in alphabetischer Reihenfolge, der spätere Eintrag gilt.</summary>
    public static ScriptValueIndex Build(IEnumerable<string> gameRoots, IEnumerable<string> modRoots)
    {
        var raw = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var root in gameRoots.Concat(modRoots))
        {
            foreach (var folder in Folders)
            {
                ReadFolder(Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar)), raw);
            }
        }

        return new ScriptValueIndex(raw);
    }

    /// <summary>Die Einträge der obersten Ebene einer Datei: Name → Zahl oder Verweis; <see langword="null"/> für Formeln und Blöcke mit mehr als einem <c>value</c>, damit sie einen früheren Eintrag überschreiben.</summary>
    public static IReadOnlyDictionary<string, string?> Read(string text)
    {
        var tokens = ScriptLexer.Tokenize(text);
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        var i = 0;
        while (i + 2 < tokens.Count)
        {
            if (tokens[i].Kind != TokenKind.Word || tokens[i + 1].Kind != TokenKind.Equals)
            {
                i++;
                continue;
            }

            var name = tokens[i].Text;
            var value = tokens[i + 2];
            if (value.Kind == TokenKind.Word)
            {
                result[name] = value.Text;
                i += 3;
            }
            else if (value.Kind == TokenKind.Open)
            {
                var close = MatchingClose(tokens, i + 2);
                if (close == i + 6
                    && tokens[i + 3] is { Kind: TokenKind.Word } key && key.Text.Equals("value", StringComparison.OrdinalIgnoreCase)
                    && tokens[i + 4].Kind == TokenKind.Equals
                    && tokens[i + 5].Kind == TokenKind.Word)
                {
                    result[name] = tokens[i + 5].Text;
                }
                else
                {
                    result[name] = null;
                }

                i = close < 0 ? tokens.Count : close + 1;
            }
            else
            {
                i++;
            }
        }

        return result;
    }

    /// <summary>Index der <c>}</c>, die die <c>{</c> an <paramref name="open"/> schließt; -1, wenn sie fehlt.</summary>
    private static int MatchingClose(List<Token> tokens, int open)
    {
        var depth = 0;
        for (var i = open; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == TokenKind.Open)
            {
                depth++;
            }
            else if (tokens[i].Kind == TokenKind.Close && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static void ReadFolder(string folder, Dictionary<string, string?> raw)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var file in Directory.EnumerateFiles(folder, "*.txt", options).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var (name, value) in Read(text))
            {
                raw[name] = value;
            }
        }
    }
}
