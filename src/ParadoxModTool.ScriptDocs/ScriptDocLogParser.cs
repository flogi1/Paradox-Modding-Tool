using System.Text.RegularExpressions;

namespace ParadoxModTool.ScriptDocs;

/// <summary>
/// Liest die Doku-Logs, die das Spiel in seinen <c>docs</c>-Ordner schreibt. Die Formate wurden an den echten Dateien von
/// Europa Universalis V abgelesen; alles, was ein Parser nicht als eigene Angabe erkennt, bleibt als Beschreibung erhalten,
/// damit bei einem geänderten Format keine Information stillschweigend verloren geht.
/// </summary>
public static class ScriptDocLogParser
{
    private const string Separator = "--------------------";

    public static IReadOnlyList<ScriptDocEntry> Parse(ScriptDocKind kind, string text) => kind switch
    {
        ScriptDocKind.Trigger or ScriptDocKind.Effect when IsHoi4Documentation(text) => ParseHoi4Commands(text, kind),
        ScriptDocKind.Modifier when IsHoi4Documentation(text) => ParseHoi4Modifiers(text),
        ScriptDocKind.DynamicVariable => IsHoi4Documentation(text) ? ParseHoi4DynamicVariables(text) : Array.Empty<ScriptDocEntry>(),
        ScriptDocKind.Modifier when IsStellaris(text) => ParseStellarisModifiers(text),
        ScriptDocKind.Trigger or ScriptDocKind.Effect or ScriptDocKind.EventTarget when IsStellaris(text) => ParseStellaris(text, kind),
        ScriptDocKind.Modifier => ParseModifiers(text),
        ScriptDocKind.Trigger => IsClassic(text) ? ParseClassic(text, ScriptDocKind.Trigger) : ParseMarkdown(text, ScriptDocKind.Trigger, "## "),
        ScriptDocKind.Effect => IsClassic(text) ? ParseClassic(text, ScriptDocKind.Effect) : ParseMarkdown(text, ScriptDocKind.Effect, "## "),
        ScriptDocKind.EventTarget => IsClassic(text) ? ParseClassic(text, ScriptDocKind.EventTarget) : ParseEventTargets(text),
        ScriptDocKind.OnAction => ParseOnActions(text),
        ScriptDocKind.CustomLocalization => ParseCustomLocalization(text),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// Klassisches Format (Crusader Kings III): erste nicht leere Zeile wie <c>Effect Documentation:</c> statt der Markdown-Überschrift
    /// <c># Effect Documentation</c> von Europa Universalis V.
    /// </summary>
    public static bool IsClassic(string text)
    {
        var first = Lines(text).Select(l => l.Trim().TrimStart('﻿')).FirstOrDefault(l => l.Length > 0);
        return first is not null && !first.StartsWith('#') && first.EndsWith("Documentation:", StringComparison.Ordinal);
    }

    private static readonly string[] Hoi4Titles = { "# Effects", "# Triggers", "# Modifiers", "# Dynamic Variables" };

    private static readonly Regex Hoi4Span = new(@"<span[^>]*>\s*</span>", RegexOptions.CultureInvariant);

    private static readonly Regex Hoi4Placeholder = new(@"<[A-Za-z]+>", RegexOptions.CultureInvariant);

    /// <summary>
    /// Doku von Hearts of Iron IV (<c>documentation/*.md</c> im Installationsordner): erste Zeile <c># Effects</c>, <c># Triggers</c>,
    /// <c># Modifiers</c> oder <c># Dynamic Variables</c>, dazu ein Inhaltsverzeichnis <c>## Table of Content</c>.
    /// </summary>
    public static bool IsHoi4Documentation(string text)
    {
        var first = Lines(text).Select(l => l.Trim().TrimStart('﻿')).FirstOrDefault(l => l.Length > 0);
        return first is not null
            && Hoi4Titles.Contains(first, StringComparer.Ordinal)
            && Lines(text).Any(l => l.Trim() == "## Table of Content");
    }

    /// <summary>
    /// Teilt HOI-IV-Doku in Einträge: eine Überschrift mit <paramref name="entryPrefix"/> beginnt einen Eintrag; <c>## Table of Content</c> und
    /// <c>## … for scope X</c> sind Abschnitte (X kommt als Abschnitt mit). Überschriften in einem ```-Zaun (<c>### Example</c>) gehören zum Text.
    /// Jede Zeile kommt mit der Angabe, ob sie im Zaun steht; die Zaunzeilen selbst fallen weg.
    /// </summary>
    private static IEnumerable<(string Name, List<(string Line, bool Fenced)> Body, string? Section)> Hoi4Sections(string text, string entryPrefix)
    {
        const string scopeMarker = " for scope ";
        string? section = null;
        string? name = null;
        var body = new List<(string, bool)>();
        var fenced = false;
        foreach (var raw in Lines(text))
        {
            var line = raw.TrimEnd();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            if (!fenced && line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (name is not null)
                {
                    yield return (name, body, section);
                }

                name = null;
                body = new List<(string, bool)>();
                var heading = line[3..].Trim();
                var scopeAt = heading.IndexOf(scopeMarker, StringComparison.Ordinal);
                if (heading == "Table of Content")
                {
                    section = null;
                }
                else if (scopeAt > 0)
                {
                    section = heading[(scopeAt + scopeMarker.Length)..].Trim();
                }
                else if (entryPrefix == "## ")
                {
                    name = Hoi4Span.Replace(heading, string.Empty).Trim();
                }

                continue;
            }

            if (!fenced && entryPrefix != "## " && line.StartsWith(entryPrefix, StringComparison.Ordinal))
            {
                if (name is not null)
                {
                    yield return (name, body, section);
                }

                name = line[entryPrefix.Length..].Trim();
                body = new List<(string, bool)>();
                continue;
            }

            if (name is not null)
            {
                body.Add((line, fenced));
            }
        }

        if (name is not null)
        {
            yield return (name, body, section);
        }
    }

    /// <summary>
    /// Effects und Trigger von HOI IV: <c>* Supported Scopes: COUNTRY, CHARACTER</c>, <c>* Supported Targets: …</c>, dann die Beschreibung in einem
    /// ```-Zaun. <c>any</c> und <c>none</c> heißen: kein bestimmter Scope.
    /// </summary>
    private static IReadOnlyList<ScriptDocEntry> ParseHoi4Commands(string text, ScriptDocKind kind)
    {
        var result = new List<ScriptDocEntry>();
        foreach (var (name, body, _) in Hoi4Sections(text, "## "))
        {
            var entry = new ScriptDocEntry { Kind = kind, Name = name };
            var description = new List<string>();
            foreach (var (line, fenced) in body)
            {
                if (!fenced && TryLabel(line.Trim(), "* Supported Scopes:", out var scopes))
                {
                    entry.Scopes.AddRange(SplitList(scopes).Where(s => !IsHoi4NoScope(s)));
                }
                else if (!fenced && TryLabel(line.Trim(), "* Supported Targets:", out var targets))
                {
                    entry.Targets.AddRange(SplitList(targets).Where(s => !IsHoi4NoScope(s)));
                }
                else if (fenced || line.Trim().Length > 0)
                {
                    description.Add(fenced ? line : line.Trim());
                }
            }

            entry.Description = JoinBody(description);
            result.Add(entry);
        }

        return result;
    }

    /// <summary>
    /// Modifier von HOI IV: <c>* Number with N decimal places</c> (Anzeigegenauigkeit, nach "Decimals"), <c>* **Categories**: a, b</c> (keine Scopes,
    /// nach "Category"), <c>* **Description**: …</c>. Vorlagen wie <c>&lt;Building&gt;_max_level_terrain_limit</c> werden mit
    /// <c>* **Modified types**: a, b</c> zu echten Namen; ohne diese Liste fallen sie weg.
    /// </summary>
    private static IReadOnlyList<ScriptDocEntry> ParseHoi4Modifiers(string text)
    {
        const string decimalsPrefix = "* Number with ";
        const string decimalsSuffix = " decimal places";
        var result = new List<ScriptDocEntry>();
        foreach (var (name, body, _) in Hoi4Sections(text, "## "))
        {
            var extra = new Dictionary<string, string>();
            var description = new List<string>();
            var types = new List<string>();
            foreach (var (raw, _) in body)
            {
                var line = raw.Trim();
                if (line.StartsWith(decimalsPrefix, StringComparison.Ordinal) && line.EndsWith(decimalsSuffix, StringComparison.Ordinal))
                {
                    extra["Decimals"] = line[decimalsPrefix.Length..^decimalsSuffix.Length].Trim();
                }
                else if (TryLabel(line, "* **Categories**:", out var categories))
                {
                    extra["Category"] = string.Join(", ", SplitList(categories));
                }
                else if (TryLabel(line, "* **Description**:", out var said))
                {
                    description.Add(said.Trim());
                }
                else if (TryLabel(line, "* **Modified types**:", out var modified))
                {
                    types.AddRange(SplitList(modified));
                }
                else if (line.Length > 0)
                {
                    description.Add(line);
                }
            }

            var names = name.Contains('<') ? types.Select(t => Hoi4Placeholder.Replace(name, t)) : new[] { name };
            foreach (var expanded in names.Where(n => n.Length > 0 && !n.Contains('<')))
            {
                result.Add(new ScriptDocEntry
                {
                    Kind = ScriptDocKind.Modifier,
                    Name = expanded,
                    Description = JoinBody(description),
                    Extra = new Dictionary<string, string>(extra),
                });
            }
        }

        return result;
    }

    /// <summary>Dynamic Variables von HOI IV: <c>## Dynamic variables for scope country</c>, darunter <c>### name</c> und <c>* description: …</c>.</summary>
    private static IReadOnlyList<ScriptDocEntry> ParseHoi4DynamicVariables(string text)
    {
        var result = new List<ScriptDocEntry>();
        foreach (var (name, body, section) in Hoi4Sections(text, "### "))
        {
            if (section is null)
            {
                continue;
            }

            var description = new List<string>();
            foreach (var (raw, _) in body)
            {
                var line = raw.Trim();
                if (TryLabel(line, "* description:", out var value))
                {
                    description.Add(value.Trim());
                }
                else if (line.Length > 0)
                {
                    description.Add(line);
                }
            }

            var entry = new ScriptDocEntry { Kind = ScriptDocKind.DynamicVariable, Name = name, Description = JoinBody(description) };
            if (!IsHoi4NoScope(section))
            {
                entry.Scopes.Add(section);
            }

            result.Add(entry);
        }

        return result;
    }

    private static bool IsHoi4NoScope(string scope) => IsNoScope(scope) || scope.Equals("any", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Format von Stellaris (<c>logs/script_documentation</c>): eine Zeitstempel-Zeile wie <c>[14:52:31][game_application.cpp:1422]:</c>, dann
    /// <c>== EFFECT DOCUMENTATION ==</c> (auch TRIGGER, SCOPE, MODIFIER).
    /// </summary>
    public static bool IsStellaris(string text)
    {
        foreach (var raw in Lines(text).Take(5))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0 || (line.StartsWith('[') && line.EndsWith("]:", StringComparison.Ordinal)))
            {
                continue;
            }

            return line.StartsWith("== ", StringComparison.Ordinal) && line.EndsWith(" DOCUMENTATION ==", StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// Trigger, Effects und Scope-Wechsel (als Event-Targets) von Stellaris. Es gibt keine Trennlinien, und Leerzeilen trennen nicht sicher, weil
    /// Anwendungsbeispiele über mehrere Zeilen gehen. Ein Eintrag beginnt mit <c>name - Beschreibung</c> (oder nur dem Namen) und endet mit der Zeile
    /// <c>Supported Scopes: a b c</c> (durch Leerzeichen getrennt, <c>all</c> = kein bestimmter Scope); bei Scope-Wechseln folgt <c>Output Scope: x</c>.
    /// Freitext vor dem ersten Eintrag (Einleitung von scopes.log) fällt weg.
    /// </summary>
    private static IReadOnlyList<ScriptDocEntry> ParseStellaris(string text, ScriptDocKind kind)
    {
        var result = new List<ScriptDocEntry>();
        var pending = new List<string>();
        ScriptDocEntry? last = null;
        foreach (var raw in Lines(text).SkipWhile(l => !l.Contains(" DOCUMENTATION ==", StringComparison.Ordinal)).Skip(1))
        {
            var line = raw.TrimEnd();
            if (TryLabel(line, "Supported Scopes:", out var scopes))
            {
                last = StellarisEntry(pending, kind);
                pending.Clear();
                if (last is not null)
                {
                    last.Scopes = scopes.Split(' ', '\t').Select(s => s.Trim()).Where(s => s.Length > 0 && s != "all").ToList();
                    result.Add(last);
                }

                continue;
            }

            if (last is not null && pending.Count == 0 && (TryLabel(line, "Output Scope:", out var output) || TryLabel(line, "Output Scopes:", out output)))
            {
                last.Targets = output.Split(' ', '\t').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                continue;
            }

            if (pending.Count > 0 || line.Length > 0)
            {
                pending.Add(line);
            }
        }

        return result;
    }

    /// <summary>Kopf (erste Zeile, deren Name kein Leerzeichen hat) und Rumpf eines Stellaris-Eintrags; Zeilen davor sind Einleitungstext.</summary>
    private static ScriptDocEntry? StellarisEntry(List<string> lines, ScriptDocKind kind)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var header = lines[i].Trim();
            var dash = header.IndexOf(" - ", StringComparison.Ordinal);
            var name = (dash < 0 ? header : header[..dash]).Trim();
            if (name.Length == 0 || name.Any(char.IsWhiteSpace) || name.EndsWith(':'))
            {
                continue;
            }

            var body = new List<string>();
            if (dash >= 0 && header[(dash + 3)..].Trim() is { Length: > 0 } first)
            {
                body.Add(first);
            }

            body.AddRange(lines.Skip(i + 1));
            return new ScriptDocEntry { Kind = kind, Name = name, Description = JoinBody(body) };
        }

        return null;
    }

    /// <summary>Modifier von Stellaris: Zeilen <c>- name, Category: A, B</c>. Die Kategorien sind keine Scope-Namen ("Ship Design Stats") und stehen unter "Category".</summary>
    private static IReadOnlyList<ScriptDocEntry> ParseStellarisModifiers(string text)
    {
        const string marker = ", Category: ";
        var result = new List<ScriptDocEntry>();
        foreach (var raw in Lines(text))
        {
            var line = raw.Trim();
            if (!line.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            var at = line.IndexOf(marker, StringComparison.Ordinal);
            var name = (at < 0 ? line[2..] : line[2..at]).Trim();
            if (name.Length == 0 || name.Any(char.IsWhiteSpace))
            {
                continue;
            }

            var entry = new ScriptDocEntry { Kind = ScriptDocKind.Modifier, Name = name };
            if (at >= 0 && line[(at + marker.Length)..].Trim() is { Length: > 0 } category)
            {
                entry.Extra["Category"] = category;
            }

            result.Add(entry);
        }

        return result;
    }

    /// <summary>
    /// Trigger, Effects und Event-Targets im klassischen Format: Blöcke zwischen Trennlinien, erste Zeile <c>name - Beschreibung</c>
    /// (getrennt am ersten <c> - </c>), danach weitere Beschreibung und die Angaben wie <c>Supported Scopes: a, b</c>.
    /// </summary>
    private static IReadOnlyList<ScriptDocEntry> ParseClassic(string text, ScriptDocKind kind)
    {
        var result = new List<ScriptDocEntry>();
        foreach (var block in Blocks(text))
        {
            var header = block[0];
            if (block.Count == 1 && header.EndsWith("Documentation:", StringComparison.Ordinal))
            {
                continue;
            }

            var dash = header.IndexOf(" - ", StringComparison.Ordinal);
            var name = (dash < 0 ? header : header[..dash]).Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var entry = new ScriptDocEntry { Kind = kind, Name = name };
            var body = new List<string>();
            if (dash >= 0 && header[(dash + 3)..].Trim() is { Length: > 0 } first)
            {
                body.Add(first);
            }

            foreach (var line in block.Skip(1))
            {
                if (!ApplyClassicLabel(entry, line))
                {
                    body.Add(line);
                }
            }

            entry.Description = JoinBody(body);
            result.Add(entry);
        }

        return result;
    }

    /// <summary>Übernimmt eine Angabe des klassischen Formats in den Eintrag; <see langword="false"/>, wenn die Zeile zur Beschreibung gehört.</summary>
    private static bool ApplyClassicLabel(ScriptDocEntry entry, string line)
    {
        if (entry.Kind == ScriptDocKind.EventTarget)
        {
            if (TryLabel(line, "Input Scopes:", out var input))
            {
                entry.Scopes = SplitList(input);
            }
            else if (TryLabel(line, "Output Scopes:", out var output))
            {
                entry.Targets = SplitList(output);
            }
            else if (TryLabel(line, "Requires Data:", out var requires))
            {
                entry.Extra["Requires Data"] = requires.Trim();
            }
            else if (TryLabel(line, "Global Link:", out var global))
            {
                entry.Extra["Global Link"] = global.Trim();
            }
            else if (TryLabel(line, "Wild Card:", out var wild))
            {
                entry.Extra["Wild Card"] = wild.Trim();
            }
            else
            {
                return false;
            }

            return true;
        }

        if (TryLabel(line, "Supported Scopes:", out var scopes))
        {
            entry.Scopes = SplitList(scopes);
            return true;
        }

        if (TryLabel(line, "Supported Targets:", out var targets))
        {
            entry.Targets = SplitList(targets);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Zeilen wie <c>Tag: name, Categories: Country, , All, </c> (EU V; "All" und "None" sind Markierungen, keine Scopes) oder, im Format von
    /// Crusader Kings III, <c>Tag: name</c> mit einer folgenden Zeile <c>Use areas: character, province, and county</c>.
    /// </summary>
    public static IReadOnlyList<ScriptDocEntry> ParseModifiers(string text)
    {
        if (!Lines(text).Any(l => l.StartsWith("Tag:", StringComparison.Ordinal)))
        {
            return ParseModifierBlocks(text);
        }

        var result = new List<ScriptDocEntry>();
        ScriptDocEntry? pending = null;
        foreach (var line in Lines(text))
        {
            if (pending is not null && TryLabel(line, "Use areas:", out var areas))
            {
                pending.Scopes = SplitList(areas.Replace(", and ", ",", StringComparison.Ordinal).Replace(" and ", ",", StringComparison.Ordinal));
                pending = null;
                continue;
            }

            if (!line.StartsWith("Tag:", StringComparison.Ordinal))
            {
                continue;
            }

            pending = null;
            var rest = line["Tag:".Length..];
            var split = rest.IndexOf(", Categories:", StringComparison.Ordinal);
            if (split < 0)
            {
                if (rest.Trim() is { Length: > 0 } tag)
                {
                    pending = new ScriptDocEntry { Kind = ScriptDocKind.Modifier, Name = tag };
                    result.Add(pending);
                }

                continue;
            }

            var name = rest[..split].Trim();
            if (name.Length == 0)
            {
                continue;
            }

            var entry = new ScriptDocEntry { Kind = ScriptDocKind.Modifier, Name = name };
            foreach (var token in SplitList(rest[(split + ", Categories:".Length)..]))
            {
                if (!token.Equals("All", StringComparison.OrdinalIgnoreCase) && !token.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    entry.Scopes.Add(token);
                }
            }

            result.Add(entry);
        }

        return result;
    }

    /// <summary>
    /// Format von Victoria 3: Abschnitte <c>--- Static modifier types ---</c> (auch Dynamic, Potential dynamic), darin je Modifier eine Zeile
    /// <c>name:</c> und eingerückt <c>Mask:</c>, <c>Name:</c> und <c>Description:</c> (Name und Beschreibung in der Sprache der Spieloberfläche).
    /// Eine Beschreibung kann ohne Einrückung auf weiteren Zeilen weitergehen. Die Maske wird zum Scope, der Abschnitt steht in
    /// <see cref="ScriptDocEntry.Extra"/> unter "Section", der Anzeigename unter "Name".
    /// </summary>
    private static IReadOnlyList<ScriptDocEntry> ParseModifierBlocks(string text)
    {
        var result = new List<ScriptDocEntry>();
        ScriptDocEntry? current = null;
        var section = string.Empty;
        var inDescription = false;
        var blankInDescription = false;
        foreach (var raw in Lines(text))
        {
            // Eine Leerzeile trennt Absätze einer Beschreibung; erst ein Abschnitt, ein neuer Modifier oder eine Angabe beendet sie.
            if (raw.Trim().Length == 0)
            {
                blankInDescription = inDescription;
                continue;
            }

            if (raw.StartsWith("---", StringComparison.Ordinal))
            {
                section = raw.Trim('-', ' ').Replace(" modifier types", string.Empty, StringComparison.Ordinal);
                current = null;
                inDescription = false;
                blankInDescription = false;
                continue;
            }

            if (raw.StartsWith(' ') && current is not null)
            {
                var line = raw.Trim();
                inDescription = false;
                blankInDescription = false;
                if (TryLabel(line, "Mask:", out var mask))
                {
                    current.Scopes = mask.Trim() is { Length: > 0 } scope ? new List<string> { scope } : new List<string>();
                }
                else if (TryLabel(line, "Name:", out var name))
                {
                    current.Extra["Name"] = name.Trim();
                }
                else if (TryLabel(line, "Description:", out var description))
                {
                    current.Description = description.Trim();
                    inDescription = true;
                    blankInDescription = false;
                }

                continue;
            }

            if (raw.EndsWith(':') && !raw.Contains(' '))
            {
                current = new ScriptDocEntry { Kind = ScriptDocKind.Modifier, Name = raw[..^1] };
                if (section.Length > 0)
                {
                    current.Extra["Section"] = section;
                }

                result.Add(current);
                inDescription = false;
                blankInDescription = false;
                continue;
            }

            if (inDescription && current is not null)
            {
                current.Description += (blankInDescription ? "\n\n" : "\n") + raw.Trim();
                blankInDescription = false;
            }
        }

        return result;
    }

    /// <summary>Trigger und Effects: <c>## name</c>, dann Beschreibung und <c>**Supported Scopes**: a, b</c> / <c>**Supported Targets**: c</c>.</summary>
    private static IReadOnlyList<ScriptDocEntry> ParseMarkdown(string text, ScriptDocKind kind, string headingPrefix)
    {
        var result = new List<ScriptDocEntry>();
        ScriptDocEntry? current = null;
        var body = new List<string>();

        void Flush()
        {
            if (current is not null)
            {
                current.Description = JoinBody(body);
                result.Add(current);
            }

            current = null;
            body.Clear();
        }

        foreach (var line in Lines(text))
        {
            if (line.StartsWith(headingPrefix, StringComparison.Ordinal))
            {
                Flush();
                var name = line[headingPrefix.Length..].Trim();
                current = name.Length == 0 ? null : new ScriptDocEntry { Kind = kind, Name = name };
                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (TryLabel(line, "**Supported Scopes**:", out var scopes))
            {
                current.Scopes = SplitList(scopes);
            }
            else if (TryLabel(line, "**Supported Targets**:", out var targets))
            {
                current.Targets = SplitList(targets);
            }
            else
            {
                body.Add(line);
            }
        }

        Flush();
        return result;
    }

    /// <summary>Event-Targets: <c>### name</c>, dann Beschreibung und die Angaben Input/Output Scopes, Requires Data, Global Link, Wild Card.</summary>
    public static IReadOnlyList<ScriptDocEntry> ParseEventTargets(string text)
    {
        var result = new List<ScriptDocEntry>();
        ScriptDocEntry? current = null;
        var body = new List<string>();

        void Flush()
        {
            if (current is not null)
            {
                current.Description = JoinBody(body);
                result.Add(current);
            }

            current = null;
            body.Clear();
        }

        foreach (var line in Lines(text))
        {
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                Flush();
                var name = line["### ".Length..].Trim();
                current = name.Length == 0 ? null : new ScriptDocEntry { Kind = ScriptDocKind.EventTarget, Name = name };
                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (TryLabel(line, "Input Scopes:", out var input))
            {
                current.Scopes = SplitList(input);
            }
            else if (TryLabel(line, "Output Scopes:", out var output))
            {
                current.Targets = SplitList(output);
            }
            else if (TryLabel(line, "Requires Data:", out var requires))
            {
                current.Extra["Requires Data"] = requires.Trim();
            }
            else if (TryLabel(line, "Global Link:", out var global))
            {
                current.Extra["Global Link"] = global.Trim();
            }
            else if (TryLabel(line, "Wild Card:", out var wild))
            {
                current.Extra["Wild Card"] = wild.Trim();
            }
            else
            {
                body.Add(line);
            }
        }

        Flush();
        return result;
    }

    /// <summary>On-Actions: durch Trennlinien getrennte Blöcke mit <c>name:</c>, <c>From Code: Yes</c> und <c>Expected Scope: country</c>.</summary>
    public static IReadOnlyList<ScriptDocEntry> ParseOnActions(string text)
    {
        var result = new List<ScriptDocEntry>();
        foreach (var block in Blocks(text))
        {
            string? name = null;
            string? fromCode = null;
            string? scope = null;
            foreach (var line in block)
            {
                if (TryLabel(line, "From Code:", out var code))
                {
                    fromCode = code.Trim();
                }
                else if (TryLabel(line, "Expected Scope:", out var expected))
                {
                    scope = expected.Trim();
                }
                else if (name is null && line.Length > 1 && line.EndsWith(':'))
                {
                    name = line[..^1].Trim();
                }
            }

            if (name is null || fromCode is null)
            {
                continue;
            }

            var entry = new ScriptDocEntry { Kind = ScriptDocKind.OnAction, Name = name };
            entry.Extra["From Code"] = fromCode;
            if (!string.IsNullOrEmpty(scope) && !IsNoScope(scope))
            {
                entry.Scopes.Add(scope);
            }

            result.Add(entry);
        }

        return result;
    }

    /// <summary>Custom-Localization: Blöcke mit <c>name:</c>, <c>Scope:</c>, <c>Random Valid:</c>, optional <c>Parent:</c>/<c>Suffix:</c>, dann <c>Entries:</c> und die Einträge.</summary>
    public static IReadOnlyList<ScriptDocEntry> ParseCustomLocalization(string text)
    {
        var result = new List<ScriptDocEntry>();
        foreach (var block in Blocks(text))
        {
            string? name = null;
            string? scope = null;
            var extra = new Dictionary<string, string>();
            var entries = new List<string>();
            var inEntries = false;

            foreach (var line in block)
            {
                if (inEntries)
                {
                    entries.Add(line);
                }
                else if (line == "Entries:")
                {
                    inEntries = true;
                }
                else if (TryLabel(line, "Scope:", out var s))
                {
                    scope = s.Trim();
                }
                else if (TryLabel(line, "Random Valid:", out var random))
                {
                    extra["Random Valid"] = random.Trim();
                }
                else if (TryLabel(line, "Parent:", out var parent))
                {
                    extra["Parent"] = parent.Trim();
                }
                else if (TryLabel(line, "Suffix:", out var suffix))
                {
                    extra["Suffix"] = suffix.Trim();
                }
                else if (name is null && line.Length > 1 && line.EndsWith(':'))
                {
                    name = line[..^1].Trim();
                }
            }

            // Der Kopfblock ("Custom Localization Documentation:") hat weder Scope noch Entries und wird so übersprungen.
            if (name is null || (scope is null && !inEntries))
            {
                continue;
            }

            var entry = new ScriptDocEntry
            {
                Kind = ScriptDocKind.CustomLocalization,
                Name = name,
                Description = JoinBody(entries),
                Extra = extra,
            };
            if (!string.IsNullOrEmpty(scope) && !IsNoScope(scope))
            {
                entry.Scopes.Add(scope);
            }

            result.Add(entry);
        }

        return result;
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n').Select(l => l.TrimEnd('\r'));

    /// <summary>Teilt den Text an den Trennlinien in Blöcke aus nicht leeren, getrimmten Zeilen.</summary>
    private static IEnumerable<List<string>> Blocks(string text)
    {
        var block = new List<string>();
        foreach (var raw in Lines(text))
        {
            var line = raw.Trim();
            if (line == Separator)
            {
                if (block.Count > 0)
                {
                    yield return block;
                }

                block = new List<string>();
            }
            else if (line.Length > 0)
            {
                block.Add(line);
            }
        }

        if (block.Count > 0)
        {
            yield return block;
        }
    }

    private static bool TryLabel(string line, string label, out string value)
    {
        if (line.StartsWith(label, StringComparison.Ordinal))
        {
            value = line[label.Length..];
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <summary>"none" (ältere Spiel-Builds) und "none/any" (neuere) heißen: kein bestimmter Scope.</summary>
    private static bool IsNoScope(string scope) =>
        scope.Equals("none", StringComparison.OrdinalIgnoreCase) || scope.Equals("none/any", StringComparison.OrdinalIgnoreCase);

    private static List<string> SplitList(string text) =>
        text.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

    private static string JoinBody(List<string> lines) =>
        string.Join("\n", lines.Select(l => l.TrimEnd())).Trim('\n', ' ');
}
