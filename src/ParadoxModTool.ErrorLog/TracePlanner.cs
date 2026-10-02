using ParadoxModTool.Scopes;

namespace ParadoxModTool.ErrorLog;

public enum TraceStatus
{
    Ok,

    /// <summary>Auf dem Weg zur Fehlerzeile gibt es keinen Befehl im Effekt-Kontext (z.B. <c>scripted_triggers</c>, <c>trigger = {}</c>).</summary>
    NoEffectContext,

    /// <summary>Die Zielzeile trägt schon eine Spur (Marker <c># PMT-TRACE</c>).</summary>
    AlreadyTraced,

    LineNotFound,
}

/// <summary>
/// Geplante Scope-Spur: Einschub <see cref="Prefix"/> am <see cref="Offset"/> (Start des Ziel-Befehls), <see cref="Suffix"/> am Ende der
/// Zeile <see cref="Line"/>. <see cref="FirstLine"/>..<see cref="LastLine"/> ist der Zeilenbereich des Ziel-Befehls (bei einer
/// <c>if</c>-Kette bis zum Ende des letzten Glieds); Fehler aus diesem Bereich gehören zur Spur.
/// </summary>
public sealed record TracePlan(TraceStatus Status, int Offset, int Line, int FirstLine, int LastLine, bool Guarded, string Prefix, string Suffix, string Id);

/// <summary>
/// Plant eine Scope-Spur an einer Fehlerstelle (Spec Scope-Verfolgung, Teil 2). Der Einschub kommt in dieselbe Zeile vor den Befehl, der
/// im Effekt-Kontext die Fehlerzeile enthält; liegt die Zeile in einem Trigger-Block, vor den Effekt-Befehl, der diesen Block enthält,
/// bei einer <c>if</c>-Kette vor deren erstes <c>if</c>. So verschieben sich keine Zeilennummern.
/// </summary>
public static class TracePlanner
{
    public const string MarkerText = "# PMT-TRACE ";

    private enum Context
    {
        None,
        Effect,
        Trigger,
    }

    public static TracePlan Plan(string text, string gamePath, int line, string? detail, IReadOnlyList<TraceGuard> guards, TraceRules rules, string id)
    {
        var starts = LineStarts(text);
        if (line < 1 || line > starts.Count)
        {
            return Failed(TraceStatus.LineNotFound, id);
        }

        var lineStart = starts[line - 1];
        var lineEnd = LineEnd(text, lineStart);
        var nodes = ScriptTree.Parse(text);
        var context = rules.IsEffectFile(gamePath) ? Context.Effect : Context.None;
        ScriptNode? target = null;
        List<ScriptNode>? targetSiblings = null;
        var list = nodes;
        while (ChildOnLine(list, lineStart, lineEnd) is { } child)
        {
            var opensTrigger = child.Key is { } key && rules.OpensTrigger(key);
            if (context == Context.Effect && !opensTrigger)
            {
                target = child;
                targetSiblings = list;
                if (child.Start >= lineStart)
                {
                    // Der erste Befehl, der auf der Zeile beginnt: Befehle darin (z.B. in "scope:x = { add_gold = 1 }") liefen im
                    // fehlenden Scope und zeigten nichts.
                    break;
                }
            }

            var next = child.Key is not { } childKey ? context
                : rules.OpensEffect(childKey) ? Context.Effect
                : opensTrigger ? Context.Trigger
                : context;
            if (next == Context.Trigger || child.Children is not { } children)
            {
                break;
            }

            list = children;
            context = next;
        }

        if (target is null || targetSiblings is null)
        {
            return Failed(TraceStatus.NoEffectContext, id);
        }

        var (first, last) = ChainOf(target, targetSiblings, rules);
        var targetLine = LineOf(starts, first.Start);
        var targetLineEnd = LineEnd(text, starts[targetLine - 1]);
        if (text.AsSpan(starts[targetLine - 1], targetLineEnd - starts[targetLine - 1]).Contains(MarkerText, StringComparison.Ordinal))
        {
            return Failed(TraceStatus.AlreadyTraced, id);
        }

        var limit = GuardFor(detail, guards);
        var trace = $"debug_log = \"PMT_TRACE_{id}\" debug_log_scopes = yes";
        var prefix = limit is null ? trace + " " : $"if = {{ limit = {{ {limit} }} {trace} }} ";
        return new TracePlan(
            TraceStatus.Ok,
            first.Start,
            targetLine,
            targetLine,
            LineOf(starts, Math.Max(first.Start, Math.Min(last.End, text.Length) - 1)),
            limit is not null,
            prefix,
            " " + MarkerText + id,
            id);
    }

    /// <summary>Schreibt den Einschub: Suffix vor das Zeilenende, Präfix an den Start des Ziel-Befehls. Andere Zeilen bleiben unverändert.</summary>
    public static string Apply(string text, TracePlan plan)
    {
        if (plan.Status != TraceStatus.Ok)
        {
            return text;
        }

        var lineEnd = LineEnd(text, plan.Offset);
        return text[..plan.Offset] + plan.Prefix + text[plan.Offset..lineEnd] + plan.Suffix + text[lineEnd..];
    }

    /// <summary>Erste passende Bedingungs-Vorlage mit eingesetztem Namen; <see langword="null"/>, wenn keine passt.</summary>
    public static string? GuardFor(string? detail, IReadOnlyList<TraceGuard> guards)
    {
        if (detail is null)
        {
            return null;
        }

        foreach (var guard in guards)
        {
            var match = guard.Detail.Match(detail);
            if (match.Success)
            {
                return guard.Limit.Replace("{name}", match.Groups["name"].Value, StringComparison.Ordinal);
            }
        }

        return null;
    }

    /// <summary>Vorzugsweise der Eintrag, der auf der Zeile beginnt (z.B. <c>else_if</c> hinter <c>}</c>), sonst der, der sie überdeckt.</summary>
    private static ScriptNode? ChildOnLine(List<ScriptNode> list, int lineStart, int lineEnd) =>
        list.FirstOrDefault(n => n.Start >= lineStart && n.Start < lineEnd)
        ?? list.FirstOrDefault(n => n.Start < lineEnd && n.End > lineStart);

    /// <summary>Glieder einer <c>if</c>-Kette: vom ersten <c>if</c> bis zum letzten folgenden <c>else_if</c>/<c>else</c>.</summary>
    private static (ScriptNode First, ScriptNode Last) ChainOf(ScriptNode target, List<ScriptNode> siblings, TraceRules rules)
    {
        if (rules.IfChain.Count == 0 || target.Key is not { } key || !rules.IfChain.Contains(key))
        {
            return (target, target);
        }

        var index = siblings.IndexOf(target);
        var first = index;
        while (first > 0 && siblings[first].Key != rules.IfChain[0] && siblings[first - 1].Key is { } previous && rules.IfChain.Contains(previous))
        {
            first--;
        }

        var last = index;
        while (last + 1 < siblings.Count && siblings[last + 1].Key is { } following && following != rules.IfChain[0] && rules.IfChain.Contains(following))
        {
            last++;
        }

        return (siblings[first], siblings[last]);
    }

    private static TracePlan Failed(TraceStatus status, string id) => new(status, 0, 0, 0, 0, false, string.Empty, string.Empty, id);

    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n' && i + 1 < text.Length)
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    /// <summary>Ende der Zeile ohne Zeilenende (vor <c>\r\n</c> bzw. <c>\n</c>).</summary>
    private static int LineEnd(string text, int offset)
    {
        var end = text.IndexOf('\n', offset);
        end = end < 0 ? text.Length : end;
        return end > offset && text[end - 1] == '\r' ? end - 1 : end;
    }

    private static int LineOf(List<int> starts, int offset)
    {
        var index = starts.BinarySearch(offset);
        return (index >= 0 ? index : ~index - 1) + 1;
    }
}
