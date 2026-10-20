using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>
/// Ein Eintrag mit seinem Schlüssel; <see cref="Wrappers"/> sind die Hüllen, in denen er steht, die äußere zuerst (leer ohne Hülle).
/// <see cref="Wrapper"/> ist die innerste. <see cref="WrapperHasValues"/>: In einer der Hüllen stehen Werte, die kein Schlüssel sind (HOI IV-Gesetze
/// <c>law = yes</c>, <c>@</c>-Konstanten in <c>technologies</c>); wer die Hülle neu schreibt, verlöre sie.
/// </summary>
public sealed record KeyedStatement(ScriptStatement Statement, string Key, IReadOnlyList<ScriptStatement> Wrappers, bool WrapperHasValues = false, string? Identity = null)
{
    public ScriptStatement? Wrapper => Wrappers.Count > 0 ? Wrappers[^1] : null;

    /// <summary>
    /// Woran das Spiel den Eintrag erkennt, für den Vergleich in der Mod-Kompatibilität: ohne die Kategorie bei <c>hülle/*</c> (HOI IV-Ideen:
    /// <c>ideas &gt; GER_x</c>) und <c>*</c> (HOI IV-Entscheidungen: <c>GER_decision</c>), denn HOI IV kennt Ideen und Entscheidungen über alle
    /// Kategorien hinweg nur nach dem Namen. <see cref="Key"/> behält die Kategorie, damit der Kompatch den Eintrag in seine Hüllen schreiben kann.
    /// </summary>
    public string IdentityKey => Identity ?? Key;
}

/// <summary>
/// Schlüssel nach den Angaben <see cref="RuleMatch.Unwrap"/> und <see cref="RuleMatch.KeyField"/>. Beispiel EU IV: Alle Events stehen in
/// <c>country_event = { id = … }</c>; ohne diese Angaben hätte jedes Event den Schlüssel <c>country_event</c>, und jede Event-Datei stünde
/// mit jeder anderen in Konflikt. Ein Eintrag ohne Namensfeld bekommt keinen Schlüssel (er zählt nur zu seiner Datei), nie den Blocknamen.
/// </summary>
/// <remarks>
/// Formen von <see cref="RuleMatch.Unwrap"/>: <c>hülle</c> (eine Ebene, EU IV <c>country_decisions</c>), <c>hülle/*</c> (zwei Ebenen, HOI IV
/// <c>ideas = { country = { GER_x = { … } } }</c>) und <c>*</c> (jeder Block der obersten Ebene ist eine Hülle, HOI IV-Entscheidungen
/// <c>GER_category = { GER_decision = { … } }</c>). In den beiden neuen Formen zählen Werte in einer Hülle nicht (HOI IV-Gesetze:
/// <c>economy = { law = yes use_list_view = yes … }</c>).
/// </remarks>
public static class RuleKeys
{
    /// <summary>Trennt Hülle und Name im Schlüssel (<c>country_decisions &gt; my_decision</c>). Skriptschlüssel enthalten keine Leerzeichen.</summary>
    public const string WrapSeparator = " > ";

    private const string AnyBlock = "*";
    private const string TwoLevelSuffix = "/*";

    public static bool Applies(RuleMatch rule) =>
        (rule.Unwrap.Count > 0 || rule.KeyField is not null)
        && rule.Behavior is not (OverrideBehavior.WholeFile or OverrideBehavior.PartialKeys or OverrideBehavior.LocalizationKeys);

    /// <summary>Die Hüllen eines Schlüssels aus <see cref="Read"/> (alles vor dem letzten Trenner, z. B. <c>ideas &gt; country</c>), sonst <see langword="null"/>.</summary>
    public static string? WrapperOf(string key)
    {
        var at = key.LastIndexOf(WrapSeparator, StringComparison.Ordinal);
        return at > 0 ? key[..at] : null;
    }

    /// <summary>Die einzelnen Hüllen eines Schlüssels, die äußere zuerst (leer ohne Hülle).</summary>
    public static IReadOnlyList<string> WrappersOf(string key) =>
        WrapperOf(key) is { } wrappers ? wrappers.Split(WrapSeparator) : Array.Empty<string>();

    /// <summary>
    /// Die Einträge der obersten Ebene mit Schlüssel. <c>@</c>-Konstanten, <c>namespace</c> und <c>inline_script</c> (eine Einbindung; ihre Definitionen vergleicht PMT nicht) haben keinen. Eine Hülle zählt nur, wenn jeder
    /// Eintrag darin einen Namen hat; sonst bleibt sie ganz ohne Schlüssel.
    /// </summary>
    public static IEnumerable<KeyedStatement> Read(ScriptBlock root, RuleMatch rule)
    {
        foreach (var statement in root.Entries.OfType<ScriptStatement>())
        {
            if (statement.Key.StartsWith('@') || statement.Key == "namespace" || statement.Key == "inline_script")
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
                    yield return new KeyedStatement(child, wrapper + WrapSeparator + name, new[] { statement });
                }

                continue;
            }

            // Zwei Ebenen (ideas/*): jede Kategorie darin ist eine Hülle für sich.
            if (rule.Unwrap.Any(w => string.Equals(w, statement.Key + TwoLevelSuffix, StringComparison.OrdinalIgnoreCase)))
            {
                if (statement.Value is ScriptBlock outer)
                {
                    var outerHasValues = outer.Entries.Any(e => e is not ScriptCommentEntry && (e is not ScriptStatement s || s.Value is not ScriptBlock));
                    foreach (var category in outer.Entries.OfType<ScriptStatement>().Where(s => s.Value is ScriptBlock))
                    {
                        foreach (var keyed in ReadWrapper(category, rule.KeyField, new[] { statement, category }))
                        {
                            var identity = statement.Key + WrapSeparator + keyed.Key[(keyed.Key.LastIndexOf(WrapSeparator, StringComparison.Ordinal) + WrapSeparator.Length)..];
                            yield return keyed with { WrapperHasValues = keyed.WrapperHasValues || outerHasValues, Identity = identity };
                        }
                    }
                }

                continue;
            }

            // Jeder Block der obersten Ebene ist eine Hülle (*), Werte haben keinen Schlüssel.
            if (rule.Unwrap.Contains(AnyBlock))
            {
                if (statement.Value is ScriptBlock)
                {
                    foreach (var keyed in ReadWrapper(statement, rule.KeyField, new[] { statement }))
                    {
                        yield return keyed with { Identity = keyed.Key[(keyed.Key.LastIndexOf(WrapSeparator, StringComparison.Ordinal) + WrapSeparator.Length)..] };
                    }
                }

                continue;
            }

            if (NameOf(statement, rule.KeyField) is { } key)
            {
                yield return new KeyedStatement(statement, key, Array.Empty<ScriptStatement>());
            }
        }
    }

    /// <summary>
    /// Die Blöcke einer Hülle als Schlüssel (neue Formen <c>hülle/*</c> und <c>*</c>): Werte darin zählen nicht; ein Block ohne Namen oder ein Eintrag,
    /// der weder Wert noch Block ist, lässt die ganze Hülle ohne Schlüssel.
    /// </summary>
    private static IEnumerable<KeyedStatement> ReadWrapper(ScriptStatement wrapper, string? keyField, IReadOnlyList<ScriptStatement> wrappers)
    {
        var inner = ((ScriptBlock)wrapper.Value).Entries.Where(e => e is not ScriptCommentEntry).ToList();
        if (inner.Any(e => e is not ScriptStatement))
        {
            return Array.Empty<KeyedStatement>();
        }

        var blocks = inner.OfType<ScriptStatement>().Where(s => s.Value is ScriptBlock).Select(s => (Statement: s, Name: NameOf(s, keyField))).ToList();
        if (blocks.Any(b => b.Name is null))
        {
            return Array.Empty<KeyedStatement>();
        }

        var prefix = string.Join(WrapSeparator, wrappers.Select(w => w.Key)) + WrapSeparator;
        var hasValues = inner.OfType<ScriptStatement>().Any(s => s.Value is not ScriptBlock);
        return blocks.Select(b => new KeyedStatement(b.Statement, prefix + b.Name, wrappers, hasValues)).ToList();
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
