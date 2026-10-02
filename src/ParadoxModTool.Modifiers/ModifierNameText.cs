using System.Text.RegularExpressions;

namespace ParadoxModTool.Modifiers;

/// <summary>
/// Macht aus einem Lokalisierungstext des Spiels einen lesbaren Namen: Farb- und Formatcodes fallen weg, Begriffe wie <c>[levy|E]</c> und Verweise auf andere
/// Schlüssel wie <c>$MOD_X_ADD$</c> werden durch ihren Text aus der Lokalisierung ersetzt (auch verschachtelt). Ein Begriff ohne Treffer wird zu seinem Namen mit
/// großem Anfangsbuchstaben ("Levy"), ein Verweis ohne Treffer ist ein Schlüsselname und ergibt keinen Namen.
/// <c>[Concept('levy', 'Aufgebots')]</c> zeigt den zweiten Text. Bleibt ein Rest, den nur das Spiel auflösen kann (<c>[GetScheme('x').GetName]</c>),
/// gibt es keinen Namen.
/// </summary>
public static partial class ModifierNameText
{
    private const int MaxNesting = 4;

    /// <param name="raw">Text aus der Lokalisierung.</param>
    /// <param name="concept">Text zu einem Begriff (<c>[levy|E]</c>, <c>Concept('levy')</c>); <see langword="null"/> oder ohne Treffer: der Name selbst.</param>
    /// <param name="reference">Text zu einem Verweis <c>$schlüssel$</c>; ohne Treffer ergibt der Verweis (außer in Kleinbuchstaben wie <c>$nobles$</c>) keinen Namen.</param>
    public static string? Clean(string raw, Func<string, string?>? concept = null, Func<string, string?>? reference = null) => Clean(raw, concept, reference, 0);

    private static string? Clean(string raw, Func<string, string?>? concept, Func<string, string?>? reference, int depth)
    {
        if (depth > MaxNesting)
        {
            return null;
        }

        string Concept(string id) => ResolveText(concept, id, depth) ?? Humanize(id);

        // Ein Verweis ohne Treffer in Großbuchstaben ist ein Schlüsselname (MOD_X_ADD), kein Wort: die Klammer lässt den ganzen Namen entfallen.
        string Reference(string id) => ResolveText(reference, id, depth) ?? (id.Any(char.IsUpper) ? "[" : Humanize(id));

        string? ResolveText(Func<string, string?>? resolve, string id, int level) =>
            resolve?.Invoke(id) is { } resolved ? Clean(resolved, concept, reference, level + 1) : null;

        var text = raw.Replace("\\n", " ");
        text = ColorCode().Replace(text, string.Empty);
        text = RichTextCode().Replace(text, string.Empty);
        text = IconCode().Replace(text, string.Empty);
        text = ConceptCall().Replace(text, match => match.Groups["text"].Success ? match.Groups["text"].Value : Concept(match.Groups["id"].Value));
        text = ConceptWithFormat().Replace(text, match => Concept(match.Groups["id"].Value));
        text = KeyReference().Replace(text, match => Reference(match.Groups["id"].Value));
        if (text.Contains('[') || text.Contains(']') || text.Contains('$'))
        {
            return null;
        }

        text = Spaces().Replace(text, " ").Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>Die Begriffe im Text (<c>[levy|E]</c>), die <see cref="Clean"/> über <c>concept</c> nachschlägt.</summary>
    public static IEnumerable<string> ReferencedConcepts(string raw)
    {
        foreach (Match match in ConceptCall().Matches(raw))
        {
            if (!match.Groups["text"].Success)
            {
                yield return match.Groups["id"].Value;
            }
        }

        foreach (Match match in ConceptWithFormat().Matches(raw))
        {
            yield return match.Groups["id"].Value;
        }
    }

    /// <summary>Die Schlüssel, auf die der Text mit <c>$schlüssel$</c> verweist; <see cref="Clean"/> schlägt sie über <c>reference</c> nach.</summary>
    public static IEnumerable<string> ReferencedKeys(string raw) => KeyReference().Matches(raw).Select(match => match.Groups["id"].Value);

    private static string Humanize(string id) =>
        string.Join(' ', id.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    [GeneratedRegex(@"§[A-Za-z!]", RegexOptions.CultureInvariant)]
    private static partial Regex ColorCode();

    [GeneratedRegex(@"#!|#[A-Za-z0-9_;]+ ?", RegexOptions.CultureInvariant)]
    private static partial Regex RichTextCode();

    /// <summary>Symbol vor dem Text (<c>@prestige_icon!Auswirkung</c>).</summary>
    [GeneratedRegex(@"@[A-Za-z0-9_]+!", RegexOptions.CultureInvariant)]
    private static partial Regex IconCode();

    [GeneratedRegex(@"\[(?<id>[A-Za-z0-9_]+)\|[^\]]*\]", RegexOptions.CultureInvariant)]
    private static partial Regex ConceptWithFormat();

    [GeneratedRegex(@"\[Concept\(\s*'(?<id>[A-Za-z0-9_]+)'\s*(?:,\s*'(?<text>[^']*)'\s*)?\)(?:\|[^\]]*)?\]", RegexOptions.CultureInvariant)]
    private static partial Regex ConceptCall();

    [GeneratedRegex(@"\$(?<id>[A-Za-z0-9_]+)(?:\|[^$]*)?\$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyReference();

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Spaces();
}
