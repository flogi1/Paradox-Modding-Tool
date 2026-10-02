using System.Text.RegularExpressions;
using ParadoxModTool.GuiDesign.DataTypes;

namespace ParadoxModTool.GuiDesign.Conditions;

public enum ConditionHintKind
{
    UnknownName,
    TypeMismatch,
    NotBool,
    ArgumentCount,
    EmptyGroup,
    NoVariant,
    RawText,
    MissingValue,
    QuoteInText,
}

/// <summary>Hinweis zu einem Knoten; <see cref="Detail"/> ist der betroffene Name bzw. <c>int32 ≠ CFixedPoint</c>, den Satz baut die Oberfläche.</summary>
public sealed record ConditionHint(CondPath Path, ConditionHintKind Kind, string Detail);

/// <summary>
/// Prüft einen Bedingungsbaum: Namen und Typen gegen den Datentyp-Dump (entfällt ohne Dump), dazu Struktur (Argumentzahl, leere Gruppe, fehlende Variante,
/// Rohtext).
/// </summary>
public static partial class ConditionChecker
{
    public static IReadOnlyList<ConditionHint> Check(CondNode? root, DataTypeDatabase db, string? start)
    {
        var hints = new List<ConditionHint>();
        if (root is not null)
        {
            Visit(root, CondPath.Root, db.Entries.Count > 0 ? db : null, start, hints);
        }

        return hints;
    }

    private static void Visit(CondNode node, CondPath path, DataTypeDatabase? db, string? start, List<ConditionHint> hints)
    {
        switch (node)
        {
            case LogicNode logic:
                if (logic.Items.Count == 0)
                {
                    hints.Add(new ConditionHint(path, ConditionHintKind.EmptyGroup, string.Empty));
                }

                if (logic.Source is CallNode call && ExpectedItems(call.Name) is { } expected && expected != logic.Items.Count)
                {
                    hints.Add(new ConditionHint(path, ConditionHintKind.ArgumentCount, call.Name));
                }

                for (var i = 0; i < logic.Items.Count; i++)
                {
                    Visit(logic.Items[i], path.Child(i), db, start, hints);
                }

                break;
            case NotNode not:
                Visit(not.Inner, path.Child(0), db, start, hints);
                break;
            case CompareNode compare:
                if (string.IsNullOrEmpty(compare.Variant))
                {
                    hints.Add(new ConditionHint(path, ConditionHintKind.NoVariant, compare.Kind.ToString()));
                }

                var sides = new[] { compare.Left, compare.Right, compare.Right2 }.OfType<ExprNode>().ToList();
                sides.ForEach(side => Names(side, path, db, start, hints));
                if (db is not null)
                {
                    var types = sides.Select(s => Normalize(ExpressionTypes.TypeOf(s, db, start))).ToList();
                    var known = types.OfType<string>().Distinct().ToList();
                    if (types.All(t => t is not null) && known.Count > 1)
                    {
                        hints.Add(new ConditionHint(path, ConditionHintKind.TypeMismatch, string.Join(" ≠ ", known)));
                    }
                }

                break;
            case CheckNode check:
                if (ComparisonCatalog.TryKind(check.Name, out var kind) && ComparisonCatalog.Arity(kind) != check.Args.Count)
                {
                    hints.Add(new ConditionHint(path, ConditionHintKind.ArgumentCount, check.Name));
                }
                else if (db is not null)
                {
                    if (ExpressionTypes.Global(db, check.Name) is not { } entry)
                    {
                        hints.Add(new ConditionHint(path, ConditionHintKind.UnknownName, check.Name));
                    }
                    else if (entry.Arguments.Count != check.Args.Count)
                    {
                        hints.Add(new ConditionHint(path, ConditionHintKind.ArgumentCount, check.Name));
                    }
                    else if (entry.HasRegisteredReturn && entry.ReturnType != "bool")
                    {
                        hints.Add(new ConditionHint(path, ConditionHintKind.NotBool, entry.ReturnType));
                    }
                }

                foreach (var arg in check.Args)
                {
                    Names(arg, path, db, start, hints);
                }

                break;
            case ValueNode value:
                var before = hints.Count;
                Names(value.Expr, path, db, start, hints);
                if (db is not null && hints.Count == before && ExpressionTypes.TypeOf(value.Expr, db, start) is { } type && type != "bool")
                {
                    hints.Add(new ConditionHint(path, ConditionHintKind.NotBool, type));
                }

                break;
            case RawCondNode raw:
                hints.Add(new ConditionHint(path, ConditionHintKind.RawText, raw.Text));
                break;
        }
    }

    /// <summary>Unbekannte Namen in Ketten, auch in Argumenten.</summary>
    private static void Names(ExprNode expr, CondPath path, DataTypeDatabase? db, string? start, List<ConditionHint> hints)
    {
        switch (expr)
        {
            case ChainNode chain:
                if (db is not null && ExpressionTypes.Resolve(chain, db, start).Unknown is { } unknown)
                {
                    hints.Add(new ConditionHint(path, ConditionHintKind.UnknownName, unknown));
                }

                foreach (var arg in chain.Links.Where(l => l.Args is not null).SelectMany(l => l.Args!))
                {
                    Names(arg, path, db, start, hints);
                }

                break;
            case CallNode call:
                foreach (var arg in call.Args)
                {
                    Names(arg, path, db, start, hints);
                }

                break;
            case LiteralNode literal when literal.Value.Contains('\''):
                // Das Spiel kennt kein Maskieren: ein Hochkomma im Text beendet die Konstante.
                hints.Add(new ConditionHint(path, ConditionHintKind.QuoteInText, literal.Value));
                break;
            case RawNode raw:
                // Leer: der Ausdruck hätte hier ein leeres Argument (GreaterThan_int32(X, )); sonst ungeprüfter Rohtext.
                hints.Add(raw.Text.Trim().Length == 0
                    ? new ConditionHint(path, ConditionHintKind.MissingValue, string.Empty)
                    : new ConditionHint(path, ConditionHintKind.RawText, raw.Text));
                break;
        }
    }

    private static int? ExpectedItems(string name)
    {
        var match = LogicName().Match(name);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups["n"].Success ? int.Parse(match.Groups["n"].Value) : 2;
    }

    /// <summary>Text heißt im Dump <c>CString</c>, eine Textkonstante <c>string</c>: für den Typvergleich gleich.</summary>
    private static string? Normalize(string? type) => type == "CString" ? "string" : type;

    [GeneratedRegex("^(And|Or)(?<n>[2-8])?$")]
    private static partial Regex LogicName();
}
