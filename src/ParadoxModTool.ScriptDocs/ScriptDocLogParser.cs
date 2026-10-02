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
