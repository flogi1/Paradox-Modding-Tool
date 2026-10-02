using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>Ein Eintrag mit seinem Schlüssel; <see cref="Wrapper"/> ist die Hülle, in der er steht (sonst <see langword="null"/>).</summary>
public sealed record KeyedStatement(ScriptStatement Statement, string Key, ScriptStatement? Wrapper);

/// <summary>
/// Schlüssel nach den Angaben <see cref="RuleMatch.Unwrap"/> und <see cref="RuleMatch.KeyField"/>. Beispiel EU IV: Alle Events stehen in
/// <c>country_event = { id = … }</c>; ohne diese Angaben hätte jedes Event den Schlüssel <c>country_event</c>, und jede Event-Datei stünde
/// mit jeder anderen in Konflikt. Ein Eintrag ohne Namensfeld bekommt keinen Schlüssel (er zählt nur zu seiner Datei), nie den Blocknamen.
/// </summary>
public static class RuleKeys
{
    /// <summary>Trennt Hülle und Name im Schlüssel (<c>country_decisions &gt; my_decision</c>). Skriptschlüssel enthalten keine Leerzeichen.</summary>
    public const string WrapSeparator = " > ";

    public static bool Applies(RuleMatch rule) =>
        (rule.Unwrap.Count > 0 || rule.KeyField is not null)
        && rule.Behavior is not (OverrideBehavior.WholeFile or OverrideBehavior.PartialKeys or OverrideBehavior.LocalizationKeys);

    /// <summary>Die Hülle eines Schlüssels aus <see cref="Read"/>, sonst <see langword="null"/>.</summary>
    public static string? WrapperOf(string key)
    {
        var at = key.IndexOf(WrapSeparator, StringComparison.Ordinal);
        return at > 0 ? key[..at] : null;
    }

    /// <summary>
    /// Die Einträge der obersten Ebene mit Schlüssel. <c>@</c>-Konstanten und <c>namespace</c> haben keinen. Eine Hülle zählt nur, wenn jeder
    /// Eintrag darin einen Namen hat; sonst bleibt sie ganz ohne Schlüssel.
    /// </summary>
    public static IEnumerable<KeyedStatement> Read(ScriptBlock root, RuleMatch rule)
    {
        foreach (var statement in root.Entries.OfType<ScriptStatement>())
        {
            if (statement.Key.StartsWith('@') || statement.Key == "namespace")
            {
                continue;
            }

            var wrapper = rule.Unwrap.FirstOrDefault(w => string.Equals(w, statement.Key, StringComparison.OrdinalIgnoreCase));
            if (wrapper is not null)
            {
                if (statement.Value is not ScriptBlock block)
                {
                    continue;
                }

                var inner = block.Entries.Where(e => e is not ScriptCommentEntry).ToList();
                var named = inner.OfType<ScriptStatement>().Select(s => (Statement: s, Name: NameOf(s, rule.KeyField))).ToList();
                if (named.Count != inner.Count || named.Any(n => n.Name is null))
                {
                    continue;
                }

                foreach (var (child, name) in named)
                {
                    yield return new KeyedStatement(child, wrapper + WrapSeparator + name, statement);
                }

                continue;
            }

            if (NameOf(statement, rule.KeyField) is { } key)
            {
                yield return new KeyedStatement(statement, key, null);
            }
        }
    }

    private static string? NameOf(ScriptStatement statement, string? keyField)
    {
        if (keyField is null)
        {
            return statement.Key;
        }

        return statement.Value is ScriptBlock block
            && block.Entries.OfType<ScriptStatement>().FirstOrDefault(c => string.Equals(c.Key, keyField, StringComparison.OrdinalIgnoreCase)) is { Value: ScriptScalar { Text.Length: > 0 } name }
            ? name.Text
            : null;
    }
}
