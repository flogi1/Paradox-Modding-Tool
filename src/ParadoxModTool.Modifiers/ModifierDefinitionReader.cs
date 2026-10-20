using System.Globalization;

namespace ParadoxModTool.Modifiers;

/// <summary>Ein Eintrag einer Modifier-Definitionsdatei; nur die Felder, die die Anzeige des Werts bestimmen.</summary>
public sealed record ModifierDefinition(bool Percent, bool AlreadyPercent, int? Decimals, string? Format, bool NoDifferenceSign);

/// <summary>
/// Liest Modifier-Definitionsdateien (EU V <c>modifier_type_definitions</c>, CK3 <c>modifier_definition_formats</c>, HOI IV <c>modifier_definitions</c>
/// mit <c>value_type</c>/<c>precision</c>): Blöcke
/// <c>name = { ... }</c> auf oberster Ebene mit <c>percent</c>, <c>already_percent</c>, <c>decimals</c>, <c>no_difference_sign</c>
/// und <c>game_data = { format = ... }</c>. Boolean-Modifier haben keinen Zahlenwert und fehlen im Ergebnis; ein kaputter Eintrag
/// wird übersprungen.
/// </summary>
public static class ModifierDefinitionReader
{
    public static IReadOnlyDictionary<string, ModifierDefinition> Read(string text)
    {
        var tokens = ScriptLexer.Tokenize(text);
        var result = new Dictionary<string, ModifierDefinition>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while (i < tokens.Count)
        {
            if (i + 2 < tokens.Count && tokens[i].Kind == TokenKind.Word && tokens[i + 1].Kind == TokenKind.Equals && tokens[i + 2].Kind == TokenKind.Open)
            {
                var name = tokens[i].Text;
                i += 3;
                if (ReadBlock(tokens, ref i) is not { } block)
                {
                    break;
                }

                if (ToDefinition(block) is { } definition)
                {
                    result[name] = definition;
                }
                else
                {
                    result.Remove(name);
                }
            }
            else
            {
                i++;
            }
        }

        return result;
    }

    /// <summary>Eigenschaften aller Blöcke auf oberster Ebene, auch der Boolean-Modifier (siehe <see cref="ModifierTraits"/>).</summary>
    public static IReadOnlyDictionary<string, ModifierTraits> ReadTraits(string text)
    {
        var tokens = ScriptLexer.Tokenize(text);
        var result = new Dictionary<string, ModifierTraits>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while (i < tokens.Count)
        {
            if (i + 2 < tokens.Count && tokens[i].Kind == TokenKind.Word && tokens[i + 1].Kind == TokenKind.Equals && tokens[i + 2].Kind == TokenKind.Open)
            {
                var name = tokens[i].Text;
                i += 3;
                if (ReadBlock(tokens, ref i) is not { } block)
                {
                    break;
                }

                bool boolean = false, bad = false;
                var category = string.Empty;
                foreach (var field in block)
                {
                    switch (field.Key.ToLowerInvariant())
                    {
                        case "boolean":
                            boolean = IsYes(field.Value);
                            break;
                        case "value_type":
                            // Hearts of Iron IV: value_type = yes_no ist ein Ja/Nein-Modifier.
                            boolean = string.Equals(field.Value, "yes_no", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "color":
                        case "color_type":
                            bad = string.Equals(field.Value, "bad", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "category" when field.Value is not null:
                            // Hearts of Iron IV nennt mehrere Kategorien mit je einer Zeile category = x.
                            category = category.Length == 0 ? field.Value : category + ", " + field.Value;
                            break;
                        case "game_data" when field.Block is not null && category.Length == 0:
                            // EU V: category steht im Block game_data.
                            category = field.Block.LastOrDefault(f => f.Key.Equals("category", StringComparison.OrdinalIgnoreCase) && f.Value is not null)?.Value ?? category;
                            break;
                    }
                }

                result[name] = new ModifierTraits(boolean, bad, category);
            }
            else
            {
                i++;
            }
        }

        return result;
    }

    private static ModifierDefinition? ToDefinition(List<Field> fields)
    {
        bool percent = false, alreadyPercent = false, noDifferenceSign = false;
        int? decimals = null;
        string? format = null;
        foreach (var field in fields)
        {
            switch (field.Key.ToLowerInvariant())
            {
                case "boolean" when IsYes(field.Value):
                    return null;
                case "percent":
                    percent = IsYes(field.Value);
                    break;
                case "already_percent":
                    alreadyPercent = IsYes(field.Value);
                    break;
                case "no_difference_sign":
                    noDifferenceSign = IsYes(field.Value);
                    break;
                case "value_type":
                    // Hearts of Iron IV (common/modifier_definitions): number, percentage, percentage_in_hundred, yes_no.
                    switch (field.Value?.ToLowerInvariant())
                    {
                        case "yes_no":
                            return null;
                        case "percentage":
                            percent = true;
                            break;
                        case "percentage_in_hundred":
                            percent = true;
                            alreadyPercent = true;
                            break;
                    }

                    break;
                case "decimals":
                case "precision":
                    if (!int.TryParse(field.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return null;
                    }

                    decimals = parsed;
                    break;
                case "game_data" when field.Block is not null:
                    format = field.Block.LastOrDefault(f => f.Key.Equals("format", StringComparison.OrdinalIgnoreCase) && f.Value is not null)?.Value ?? format;
                    break;
            }
        }

        return new ModifierDefinition(percent, alreadyPercent, decimals, string.IsNullOrEmpty(format) ? null : format, noDifferenceSign);
    }

    private static bool IsYes(string? value) => string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>Liest einen Block nach der öffnenden Klammer bis zur passenden schließenden; <see langword="null"/>, wenn sie fehlt.</summary>
    private static List<Field>? ReadBlock(List<Token> tokens, ref int i)
    {
        var fields = new List<Field>();
        while (i < tokens.Count)
        {
            var token = tokens[i];
            if (token.Kind == TokenKind.Close)
            {
                i++;
                return fields;
            }

            if (token.Kind == TokenKind.Word && i + 2 < tokens.Count && tokens[i + 1].Kind == TokenKind.Equals)
            {
                var value = tokens[i + 2];
                i += 3;
                if (value.Kind == TokenKind.Open)
                {
                    if (ReadBlock(tokens, ref i) is not { } inner)
                    {
                        return null;
                    }

                    fields.Add(new Field(token.Text, null, inner));
                }
                else if (value.Kind == TokenKind.Word)
                {
                    fields.Add(new Field(token.Text, value.Text, null));
                }
                else
                {
                    i--;
                }

                continue;
            }

            if (token.Kind == TokenKind.Open)
            {
                i++;
                if (ReadBlock(tokens, ref i) is null)
                {
                    return null;
                }

                continue;
            }

            i++;
        }

        return null;
    }

    private sealed record Field(string Key, string? Value, List<Field>? Block);
}
