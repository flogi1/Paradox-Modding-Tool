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
        ScriptDocKind.Trigger => ParseMarkdown(text, ScriptDocKind.Trigger, "## "),
        ScriptDocKind.Effect => ParseMarkdown(text, ScriptDocKind.Effect, "## "),
        ScriptDocKind.EventTarget => ParseEventTargets(text),
        ScriptDocKind.OnAction => ParseOnActions(text),
        ScriptDocKind.CustomLocalization => ParseCustomLocalization(text),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Zeilen wie <c>Tag: name, Categories: Country, , All, </c>. "All" und "None" sind Markierungen, keine Scopes.</summary>
    public static IReadOnlyList<ScriptDocEntry> ParseModifiers(string text)
    {
        var result = new List<ScriptDocEntry>();
        foreach (var line in Lines(text))
        {
            if (!line.StartsWith("Tag:", StringComparison.Ordinal))
            {
                continue;
            }

            var rest = line["Tag:".Length..];
            var split = rest.IndexOf(", Categories:", StringComparison.Ordinal);
            if (split < 0)
            {
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
            if (!string.IsNullOrEmpty(scope) && !scope.Equals("none", StringComparison.OrdinalIgnoreCase))
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
            if (!string.IsNullOrEmpty(scope) && !scope.Equals("none", StringComparison.OrdinalIgnoreCase))
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

    private static List<string> SplitList(string text) =>
        text.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();

    private static string JoinBody(List<string> lines) =>
        string.Join("\n", lines.Select(l => l.TrimEnd())).Trim('\n', ' ');
}
