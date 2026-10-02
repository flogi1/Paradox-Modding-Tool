using System.Text.RegularExpressions;
using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Desync;

/// <summary>Eine Mod, die die MP-Prüfung untersucht; <paramref name="IsOwnMod"/> trennt eigene Mods von fremden.</summary>
public sealed record SafetyMod(string Name, string Directory, bool IsOwnMod);

/// <summary>Eine untersuchte Datei; <paramref name="RelativePath"/> gilt ab dem Modordner und trennt mit <c>/</c>.</summary>
public sealed record ScannedFile(string ModName, bool IsOwnMod, string RelativePath, string FullPath);

/// <summary>
/// Ein Fund der MP-Prüfung. <paramref name="Line"/> zählt ab 1. <paramref name="Token"/> ist klein geschrieben (bei Skriptschlüsseln der Wert, sonst der Schlüssel;
/// bei Konsolenbefehlen der Befehlsname; bei einem zur Laufzeit gebauten Befehl der normalisierte Argumenttext, ersatzweise <c>dynamic</c>). <paramref name="ProfileSeverity"/> ist die Strenge laut Profil,
/// <paramref name="Severity"/> die nach dem Urteil des Nutzers. <paramref name="Parsed"/> ist <c>false</c>, wenn die Datei sich nicht parsen ließ und der Fund
/// aus der Zeilensuche stammt (Kontext dann <see cref="SafetyContext.Unknown"/>); <paramref name="ParseErrorLine"/> ist dann die Zeile, die der Parser meldete.
/// </summary>
public sealed record SafetyFinding(
    string ModName,
    bool IsOwnMod,
    string RelativePath,
    string FullPath,
    int Line,
    string RuleId,
    string Token,
    SafetyContext Context,
    SafetySeverity Severity,
    SafetySeverity ProfileSeverity,
    bool Parsed,
    int? ParseErrorLine = null);

/// <summary>Art eines Hinweises der Prüfung.</summary>
public enum SafetyNoticeKind
{
    /// <summary>Der Modordner existiert nicht.</summary>
    MissingFolder,

    /// <summary>Eine Datei oder ein Ordner ließ sich nicht lesen.</summary>
    Unreadable,
}

/// <summary>Ein Hinweis zur Prüfung (kein Fund); <paramref name="Path"/> ist der Modordner bzw. die Datei, <paramref name="Message"/> die Fehlermeldung, falls es eine gibt.</summary>
public sealed record SafetyNotice(SafetyNoticeKind Kind, string ModName, string Path, string? Message);

/// <summary>Ergebnis einer Prüfung: Funde (nach Mod, Pfad, Zeile sortiert), Hinweise und alle untersuchten Dateien.</summary>
public sealed record SafetyScanResult(
    IReadOnlyList<SafetyFinding> Findings,
    IReadOnlyList<SafetyNotice> Notices,
    IReadOnlyList<ScannedFile> Files)
{
    /// <summary>Dasselbe Ergebnis mit den Urteilen des Nutzers neu angewendet: die Strenge <see cref="SafetyFinding.Severity"/> wird aus <see cref="SafetyFinding.ProfileSeverity"/> neu bestimmt.</summary>
    public SafetyScanResult WithVerdicts(SafetyVerdicts verdicts) =>
        this with
        {
            Findings = Findings.Select(f => f with { Severity = verdicts.Apply(f.RuleId, f.Token, f.ProfileSeverity) }).ToList(),
        };
}

/// <summary>
/// Prüft Mods auf Dinge, die im Mehrspielerbetrieb auseinanderlaufen können (siehe <see cref="SafetyRules"/>). Liest nur <c>.txt</c> (geparst, bei Fehlern zeilenweise)
/// und <c>.gui</c> (zeilenweise, <c>ExecuteConsoleCommand</c>); Ordner mit einem Punkt am Anfang werden übersprungen, alles nach <c>#</c> außerhalb von <c>"…"</c> und <c>'…'</c> zählt nicht.
/// </summary>
public static class SafetyScanner
{
    private const string DynamicToken = "dynamic";

    /// <summary>Höchstlänge des Tokens eines zur Laufzeit gebauten Befehls (der normalisierte Argumenttext).</summary>
    private const int MaxDynamicTokenLength = 120;

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    private static readonly Regex ConsoleCall = new(@"ExecuteConsoleCommand\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ConsoleLiteral = new(@"\GExecuteConsoleCommand\(\s*'([^']*)'\s*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// <c>ExecuteConsoleCommand( Select_CString( Bedingung, 'a', 'b' ) )</c>: Die letzten zwei Argumente sind Texte, also zwei bekannte Befehle. Die Bedingung
    /// (auch mit Klammern und eigenen Texten) zählt nicht.
    /// </summary>
    private static readonly Regex ConsoleSelect = new(
        @"\GExecuteConsoleCommand\(\s*Select_CString\(\s*(?:[^()']|'[^']*'|\((?:[^()']|'[^']*'|\((?:[^()']|'[^']*')*\))*\))*?,\s*'([^']*)'\s*,\s*'([^']*)'\s*\)\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Das Wort hinter einem Vergleichsoperator, falls eines folgt (für die Zeilensuche in nicht parsbaren Dateien).</summary>
    private static readonly Regex ValueAfterOperator = new(@"\G\s*(?:=|\?=|!=|<=|>=|<|>)\s*""?([\w.:\-]+)", RegexOptions.CultureInvariant);

    /// <summary>Untersucht alle Mods. Wirft <see cref="OperationCanceledException"/>, wenn <paramref name="cancellationToken"/> abgebrochen wird.</summary>
    public static SafetyScanResult Scan(
        IReadOnlyList<SafetyMod> mods,
        SafetyRules rules,
        SafetyVerdicts verdicts,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var notices = new List<SafetyNotice>();
        var files = new List<ScannedFile>();
        foreach (var mod in mods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!System.IO.Directory.Exists(mod.Directory) && FolderProblem(mod) is { } problem)
            {
                notices.Add(problem);
                continue;
            }

            CollectFiles(mod, files, notices, cancellationToken);
        }

        var fallback = FallbackPatterns(rules);
        var keyPatterns = rules.Rules
            .Where(r => r.Kind == SafetyRuleKind.ScriptKey)
            .SelectMany(r => r.Patterns)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var findings = new List<SafetyFinding>();
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            var text = Read(file.FullPath, out var error);
            if (text is null)
            {
                notices.Add(new SafetyNotice(SafetyNoticeKind.Unreadable, file.ModName, file.FullPath, error));
            }
            else if (file.RelativePath.EndsWith(".gui", StringComparison.OrdinalIgnoreCase))
            {
                ScanGui(file, text, rules, verdicts, findings);
            }
            else
            {
                ScanScript(file, text, rules, verdicts, keyPatterns, fallback, findings);
            }

            progress?.Report((double)(i + 1) / files.Count);
        }

        if (files.Count == 0)
        {
            progress?.Report(1.0);
        }

        var sorted = findings
            .OrderBy(f => f.ModName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Line)
            .ToList();
        return new SafetyScanResult(sorted, notices, files);
    }

    /// <summary>Der Text der Datei (UTF-8 mit oder ohne BOM, sonst Windows-1252), oder <c>null</c>, wenn sie sich nicht lesen lässt.</summary>
    public static string? ReadText(string fullPath) => Read(fullPath, out _);

    private static string? Read(string fullPath, out string? error)
    {
        try
        {
            error = null;
            return ParsedScript.Decode(File.ReadAllBytes(fullPath)).Text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Warum <see cref="System.IO.Directory.Exists"/> den Modordner nicht fand: Gibt es ihn nicht (oder ist der Pfad ungültig), ist es
    /// <see cref="SafetyNoticeKind.MissingFolder"/>; lässt er sich nur nicht öffnen, <see cref="SafetyNoticeKind.Unreadable"/> mit der Meldung.
    /// <c>null</c>, wenn er sich inzwischen doch lesen lässt.
    /// </summary>
    private static SafetyNotice? FolderProblem(SafetyMod mod)
    {
        try
        {
            using var entries = System.IO.Directory.EnumerateFileSystemEntries(mod.Directory).GetEnumerator();
            entries.MoveNext();
            return null;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or ArgumentException)
        {
            return new SafetyNotice(SafetyNoticeKind.MissingFolder, mod.Name, mod.Directory, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SafetyNotice(SafetyNoticeKind.Unreadable, mod.Name, mod.Directory, ex.Message);
        }
    }

    /// <summary>Sammelt die <c>.txt</c>- und <c>.gui</c>-Dateien eines Modordners, ohne in Ordner mit einem Punkt am Anfang hinabzusteigen.</summary>
    private static void CollectFiles(SafetyMod mod, List<ScannedFile> files, List<SafetyNotice> notices, CancellationToken cancellationToken)
    {
        var pending = new Stack<(string Full, string Relative)>();
        pending.Push((mod.Directory, string.Empty));
        var found = new List<ScannedFile>();
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, relative) = pending.Pop();
            try
            {
                foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
                {
                    if (entry.Name.StartsWith('.'))
                    {
                        continue;
                    }

                    var entryRelative = relative.Length == 0 ? entry.Name : relative + "/" + entry.Name;
                    if (entry is DirectoryInfo)
                    {
                        pending.Push((entry.FullName, entryRelative));
                    }
                    else if (entry.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                        || entry.Extension.Equals(".gui", StringComparison.OrdinalIgnoreCase))
                    {
                        found.Add(new ScannedFile(mod.Name, mod.IsOwnMod, entryRelative, entry.FullName));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                notices.Add(new SafetyNotice(SafetyNoticeKind.Unreadable, mod.Name, directory, ex.Message));
            }
        }

        files.AddRange(found.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase));
    }

    // ---- .txt ----

    /// <summary>Muster einer Skriptschlüssel-Regel für die Zeilensuche: das Muster klein geschrieben und der Ausdruck für das ganze Wort.</summary>
    private sealed record FallbackPattern(SafetyRule Rule, string Pattern, Regex Regex);

    private static void ScanScript(
        ScannedFile file,
        string text,
        SafetyRules rules,
        SafetyVerdicts verdicts,
        IReadOnlyList<string> keyPatterns,
        IReadOnlyList<FallbackPattern> fallback,
        List<SafetyFinding> findings)
    {
        // Ohne eines der Muster im Text kann weder ein Schlüssel noch die Zeilensuche treffen: Parsen wäre umsonst.
        if (!keyPatterns.Any(pattern => text.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        ScriptBlock root;
        try
        {
            root = ScriptDocument.Parse(text);
        }
        catch (ScriptParseException ex)
        {
            ScanScriptLines(file, text, verdicts, fallback, findings, ex.Line);
            return;
        }

        var lines = new LineIndex(text);
        Walk(root, new List<string>(), file, rules, verdicts, lines, findings);
    }

    private static void Walk(
        ScriptBlock block,
        List<string> ancestors,
        ScannedFile file,
        SafetyRules rules,
        SafetyVerdicts verdicts,
        LineIndex lines,
        List<SafetyFinding> findings)
    {
        foreach (var entry in block.Entries)
        {
            switch (entry)
            {
                case ScriptStatement statement:
                {
                    var token = statement.Value is ScriptScalar scalar ? scalar.Text : statement.Key;
                    Check(statement.Key, token, entry);
                    if (statement.Value is ScriptBlock inner)
                    {
                        ancestors.Add(statement.Key);
                        Walk(inner, ancestors, file, rules, verdicts, lines, findings);
                        ancestors.RemoveAt(ancestors.Count - 1);
                    }

                    break;
                }

                case ScriptListItem { Value: ScriptScalar item }:
                    Check(item.Text, item.Text, entry);
                    break;

                case ScriptListItem { Value: ScriptBlock inner }:
                    Walk(inner, ancestors, file, rules, verdicts, lines, findings);
                    break;
            }
        }

        void Check(string key, string token, ScriptEntry entry)
        {
            var rule = rules.ScriptKeyRule(key);
            if (rule is null)
            {
                return;
            }

            var context = ScriptContext.Classify(rules, file.RelativePath, ancestors);
            var line = entry.SourceStart is { } start ? lines.LineAt(start) : 1;
            findings.Add(MakeFinding(file, line, rule, token, context, verdicts, parsed: true));
        }
    }

    /// <summary>Zeilensuche für eine <c>.txt</c>-Datei, die sich nicht parsen lässt: Der Kontext ist unbekannt, weil die Blöcke fehlen.</summary>
    private static void ScanScriptLines(
        ScannedFile file,
        string text,
        SafetyVerdicts verdicts,
        IReadOnlyList<FallbackPattern> fallback,
        List<SafetyFinding> findings,
        int parseErrorLine)
    {
        if (fallback.Count == 0)
        {
            return;
        }

        var number = 0;
        foreach (var raw in SplitLines(text))
        {
            number++;
            var line = StripComment(raw);
            if (line.Length == 0)
            {
                continue;
            }

            foreach (var (rule, pattern, regex) in fallback)
            {
                foreach (Match match in regex.Matches(line))
                {
                    var value = ValueAfterOperator.Match(line, match.Index + match.Length);
                    var token = value.Success ? value.Groups[1].Value : pattern;
                    findings.Add(MakeFinding(file, number, rule, token, SafetyContext.Unknown, verdicts, parsed: false, parseErrorLine));
                }
            }
        }
    }

    /// <summary>Je Muster einer Skriptschlüssel-Regel ein Ausdruck für das ganze Wort; bei gleichem Muster in mehreren Regeln gilt die erste Regel, wie in <see cref="SafetyRules.ScriptKeyRule"/>.</summary>
    private static List<FallbackPattern> FallbackPatterns(SafetyRules rules)
    {
        var result = new List<FallbackPattern>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules.Rules.Where(r => r.Kind == SafetyRuleKind.ScriptKey))
        {
            foreach (var pattern in rule.Patterns)
            {
                if (seen.Add(pattern) && rules.ScriptKeyRule(pattern) == rule)
                {
                    result.Add(new FallbackPattern(
                        rule,
                        pattern.ToLowerInvariant(),
                        new Regex(@"\b" + Regex.Escape(pattern) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)));
                }
            }
        }

        return result;
    }

    // ---- .gui ----

    private static void ScanGui(ScannedFile file, string text, SafetyRules rules, SafetyVerdicts verdicts, List<SafetyFinding> findings)
    {
        if (!text.Contains("ExecuteConsoleCommand(", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var context = ScriptContext.Classify(rules, file.RelativePath, Array.Empty<string>());
        var number = 0;
        foreach (var raw in SplitLines(text))
        {
            number++;
            var line = StripComment(raw);
            foreach (Match call in ConsoleCall.Matches(line))
            {
                var literal = ConsoleLiteral.Match(line, call.Index);
                var select = literal.Success ? literal : ConsoleSelect.Match(line, call.Index);
                if (select.Success)
                {
                    for (var group = 1; group < select.Groups.Count; group++)
                    {
                        var token = CommandName(select.Groups[group].Value);
                        if (rules.ConsoleRule(token) is { } rule)
                        {
                            findings.Add(MakeFinding(file, number, rule, token, context, verdicts, parsed: true));
                        }
                    }
                }
                else if (rules.DynamicRule is { } dynamicRule)
                {
                    findings.Add(MakeFinding(file, number, dynamicRule, DynamicArgument(line, call.Index + call.Length), context, verdicts, parsed: true));
                }
            }
        }
    }

    /// <summary>
    /// Token eines zur Laufzeit gebauten Befehls: der Text ab <paramref name="start"/> (hinter <c>ExecuteConsoleCommand(</c>) bis zur passenden
    /// schließenden Klammer (Klammern in <c>'…'</c> zählen nicht), Leerraum zu einem Leerzeichen zusammengefasst, getrimmt, klein und höchstens
    /// <see cref="MaxDynamicTokenLength"/> Zeichen lang. Fehlt die Klammer auf der Zeile oder bleibt nichts übrig, ist es <c>dynamic</c>.
    /// So deckt ein Urteil über einen Ausdruck nicht jeden anderen mit ab.
    /// </summary>
    private static string DynamicArgument(string line, int start)
    {
        var depth = 0;
        var inQuote = false;
        for (var i = start; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '\'')
            {
                inQuote = !inQuote;
            }
            else if (inQuote)
            {
                continue;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                if (depth == 0)
                {
                    var token = Whitespace.Replace(line[start..i], " ").Trim().ToLowerInvariant();
                    if (token.Length == 0)
                    {
                        return DynamicToken;
                    }

                    return token.Length > MaxDynamicTokenLength ? token[..MaxDynamicTokenLength] : token;
                }

                depth--;
            }
        }

        return DynamicToken;
    }

    /// <summary>Der Befehlsname: der Text bis zum ersten Leerraum, klein geschrieben.</summary>
    private static string CommandName(string command)
    {
        var trimmed = command.Trim();
        var end = 0;
        while (end < trimmed.Length && !char.IsWhiteSpace(trimmed[end]))
        {
            end++;
        }

        return trimmed[..end].ToLowerInvariant();
    }

    // ---- gemeinsam ----

    private static SafetyFinding MakeFinding(
        ScannedFile file,
        int line,
        SafetyRule rule,
        string token,
        SafetyContext context,
        SafetyVerdicts verdicts,
        bool parsed,
        int? parseErrorLine = null)
    {
        var lower = token.ToLowerInvariant();
        var profile = rule.SeverityIn(context);
        return new SafetyFinding(
            file.ModName,
            file.IsOwnMod,
            file.RelativePath,
            file.FullPath,
            line,
            rule.Id,
            lower,
            context,
            verdicts.Apply(rule.Id, lower, profile),
            profile,
            parsed,
            parseErrorLine);
    }

    /// <summary>
    /// Schneidet alles ab <c>#</c> ab, das weder in <c>"…"</c> noch in <c>'…'</c> steht. Innerhalb des einen zählt das andere Zeichen nicht;
    /// ein <c>\</c> in <c>"…"</c> schützt das nächste Zeichen (in <c>'…'</c> nicht, wie bei den Texten der Konsolenbefehle).
    /// </summary>
    private static string StripComment(string line)
    {
        if (!line.Contains('#'))
        {
            return line;
        }

        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote == '"' && c == '\\')
            {
                i++;
            }
            else if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '#')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        var start = 0;
        while (start <= text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                yield return text[start..];
                yield break;
            }

            yield return text[start..end];
            start = end + 1;
        }
    }

    /// <summary>Zeilennummer (ab 1) zu einem Zeichenabstand: 1 plus die Zahl der <c>\n</c> davor.</summary>
    private sealed class LineIndex
    {
        private readonly List<int> _newlines = new();

        public LineIndex(string text)
        {
            for (var i = text.IndexOf('\n'); i >= 0; i = text.IndexOf('\n', i + 1))
            {
                _newlines.Add(i);
            }
        }

        public int LineAt(int offset)
        {
            var index = _newlines.BinarySearch(offset);
            return 1 + (index >= 0 ? index : ~index);
        }
    }
}
