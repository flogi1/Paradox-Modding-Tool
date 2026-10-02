namespace ParadoxModTool.Scopes;

/// <summary>Ein Glied einer bearbeitbaren Scope-Kette.</summary>
/// <param name="Step">Der Schritt (Event-Target, Iterator oder Verweis).</param>
/// <param name="Condition">Im Block dieses Glieds steht vor dem Rest der Kette ein <c>if = { limit = { } … }</c> (in Triggern <c>trigger_if</c>).</param>
/// <param name="Limit">Nur bei Iteratoren: der Iterator bekommt <c>limit = { }</c>.</param>
public sealed record ChainLink(PathStep Step, bool Condition, bool Limit);

/// <summary>
/// Eine Scope-Kette, wie sie der Scope Finder bearbeitet: Start (Scope und Präfix wie <c>root</c> oder <c>scope:actor</c>), Glieder mit Schaltern für Bedingung
/// und Limit, und optional ein Wert am Ende (<c>country_tax_base</c>, <c>modifier:KEY</c>). Unveränderlich; jede Operation liefert eine neue Kette.
/// </summary>
/// <param name="StartCondition">Ein <c>if</c> im Start-Scope, vor dem ersten Glied.</param>
/// <param name="Value">Wert am Ende; gilt nur für eine reine Kette (<see cref="CanBeChain"/>).</param>
public sealed record ScopeChain(string StartScope, string StartPrefix, bool StartCondition, IReadOnlyList<ChainLink> Links, string? Value)
{
    public static ScopeChain Empty(string startScope, string startPrefix) =>
        new(startScope, startPrefix, false, Array.Empty<ChainLink>(), null);

    /// <summary>Ein Weg aus der Suche als Kette, alle Schalter aus.</summary>
    public static ScopeChain From(ScopePath path) =>
        new(path.StartScope, path.StartPrefix, false, path.Steps.Select(s => new ChainLink(s, false, false)).ToArray(), null);

    /// <summary>Der Scope nach dem letzten Glied (ohne Glieder der Start-Scope).</summary>
    public string EndScope => Links.Count == 0 ? StartScope : Links[^1].Step.To;

    /// <summary>Lässt sich die Kette als Ausdruck schreiben (<c>owner.capital</c>)? Nur ohne Iteratoren, Bedingungen und Limits.</summary>
    public bool CanBeChain => !StartCondition && Links.All(l => l.Step.Kind != PathStepKind.Iterator && !l.Condition && !l.Limit);

    public ScopeChain Append(PathStep step) => this with { Links = Links.Append(new ChainLink(step, false, false)).ToArray() };

    /// <summary>Entfernt das Glied <paramref name="index"/> und alle dahinter.</summary>
    public ScopeChain TruncateAt(int index) => this with { Links = Links.Take(Math.Clamp(index, 0, Links.Count)).ToArray() };

    /// <summary>Schaltet die Bedingung am Glied <paramref name="index"/> um; −1 ist der Start.</summary>
    public ScopeChain WithCondition(int index, bool on) =>
        index < 0 ? this with { StartCondition = on } : Replace(index, Links[index] with { Condition = on });

    /// <summary>Schaltet das Limit am Glied <paramref name="index"/> um; bei anderen Gliedern als Iteratoren bleibt die Kette unverändert.</summary>
    public ScopeChain WithLimit(int index, bool on) =>
        Links[index].Step.Kind == PathStepKind.Iterator ? Replace(index, Links[index] with { Limit = on }) : this;

    public ScopeChain WithValue(string? value) => this with { Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim() };

    /// <summary>
    /// Die Kette als Ausdruck mit Präfix und Wert (<c>scope:recipient.leader_country.country_tax_base</c>); ein Wert darf auch direkt am Start stehen
    /// (<c>root.country_tax_base</c>, im aktuellen Scope nur <c>country_tax_base</c>). <see langword="null"/>, wenn sie keine reine Kette ist oder weder Glieder noch
    /// Wert hat.
    /// </summary>
    public string? ChainText()
    {
        if (!CanBeChain || (Links.Count == 0 && Value is null))
        {
            return null;
        }

        var parts = new List<string>();
        if (StartPrefix.Length > 0)
        {
            parts.Add(StartPrefix);
        }

        parts.AddRange(Links.Select(l => l.Step.Text("every")));
        if (Value is not null)
        {
            parts.Add(Value);
        }

        return string.Join('.', parts);
    }

    /// <summary>
    /// Die Kette als verschachtelte Blöcke zum Einfügen an der Cursorstelle (die Einrückung der ersten Zeile steht dort schon). Aufeinanderfolgende Glieder ohne
    /// Iterator und Bedingung bilden einen Kopf (<c>owner.capital</c>); eine Bedingung beendet den Kopf und öffnet darin <c>if = { limit = { } … }</c>, in der
    /// Form <c>any</c> <c>trigger_if</c> (dort gibt es kein Iterator-<c>limit</c>, der Inhalt ist schon die Bedingung). Der Cursor steht im ersten
    /// <c>limit</c> von außen, sonst in der leeren Zeile des innersten Blocks. Der Wert am Ende gehört nur zur Kette und wird hier nie geschrieben.
    /// </summary>
    public string ToBlock(string preferredForm, string indent, string newline, out int caretOffset, string tab = "\t")
    {
        var levels = Levels(preferredForm);
        var text = new System.Text.StringBuilder();
        caretOffset = -1;

        string Line(int level) => level == 0 ? string.Empty : indent + string.Concat(Enumerable.Repeat(tab, level));

        for (var level = 0; level < levels.Count; level++)
        {
            text.Append(Line(level)).Append(levels[level].Head).Append(" = {").Append(newline);
            if (!levels[level].HasLimit)
            {
                continue;
            }

            text.Append(Line(level + 1)).Append("limit = {").Append(newline);
            if (caretOffset < 0)
            {
                text.Append(Line(level + 2));
                caretOffset = text.Length;
                text.Append(newline);
            }

            text.Append(Line(level + 1)).Append('}').Append(newline);
        }

        if (caretOffset < 0)
        {
            text.Append(Line(levels.Count));
            caretOffset = text.Length;
            text.Append(newline);
        }

        for (var level = levels.Count - 1; level >= 0; level--)
        {
            text.Append(level == 0 ? indent : Line(level)).Append('}');
            if (level > 0)
            {
                text.Append(newline);
            }
        }

        return text.ToString();
    }

    /// <summary>Die Blockebenen von außen nach innen: Kopf und ob darin ein <c>limit</c> steht.</summary>
    private List<(string Head, bool HasLimit)> Levels(string preferredForm)
    {
        var condition = preferredForm == "any" ? "trigger_if" : "if";
        var levels = new List<(string Head, bool HasLimit)>();
        var chain = new List<string>();
        if (StartCondition)
        {
            levels.Add((condition, true));
        }

        if (StartPrefix.Length > 0)
        {
            chain.Add(StartPrefix);
        }

        void Flush()
        {
            if (chain.Count > 0)
            {
                levels.Add((string.Join('.', chain), false));
                chain.Clear();
            }
        }

        foreach (var link in Links)
        {
            if (link.Step.Kind == PathStepKind.Iterator)
            {
                Flush();
                levels.Add((link.Step.Text(preferredForm), link.Limit && preferredForm != "any"));
            }
            else
            {
                chain.Add(link.Step.Text(preferredForm));
                if (link.Condition)
                {
                    Flush();
                }
            }

            if (link.Condition)
            {
                levels.Add((condition, true));
            }
        }

        Flush();
        return levels;
    }

    private ScopeChain Replace(int index, ChainLink link)
    {
        var links = Links.ToArray();
        links[index] = link;
        return this with { Links = links };
    }
}
