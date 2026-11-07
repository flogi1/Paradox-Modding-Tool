using System.Text.RegularExpressions;

namespace ParadoxModTool.FlagTracker;

/// <summary>Liest Datenfunktionen für Variablen in <c>.gui</c>-Dateien und Lokalisierungs-<c>.yml</c>, z. B. <c>Var('x')</c> oder <c>GetGlobalVariable("g")</c>.</summary>
public static class DataFunctionReader
{
    private static readonly Regex Call = new(
        @"\b(Var|GetVariable|HasVariable|GetGlobalVariable|GlobalVariable|GetLocalVariable|LocalVariable|VariableList|GetVariableList|GetList)\(\s*\\?(['""])([^'""\\\s()]+)\\?\2",
        RegexOptions.Compiled);

    // Der Name ist das letzte Argument in Anführungszeichen (Victoria 3: GetVarTimeRemaining( GetPlayer.MakeScope, 'x')).
    private static readonly Regex TimeRemaining = new(
        @"\bGetVarTimeRemaining\([^)]*,\s*\\?(['""])([^'""\\\s()]+)\\?\1\s*\)",
        RegexOptions.Compiled);

    /// <summary>HOI IV <c>[?X]</c>; <c>|fmt</c> und alles danach gehört nicht zum Namen.</summary>
    private static readonly Regex Hoi4Bracket = new(@"\[\?([^\]\[|]*)", RegexOptions.Compiled);

    /// <summary>EU IV <c>[A.B.C]</c>: Inhalt einer eckigen Klammer ohne weitere Klammern.</summary>
    private static readonly Regex Eu4Bracket = new(@"\[([^\[\]]+)\]", RegexOptions.Compiled);

    /// <summary>Bezeichner (Event-Target-artiges Wort, Variablenname).</summary>
    private static readonly Regex Identifier = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static readonly Regex Eu4Tag = new(@"^[A-Z0-9]{3}$", RegexOptions.Compiled);

    /// <summary>Bereichswörter vor <c>.var.GetValue</c>; als vorletztes Glied sind sie kein Variablenname.</summary>
    private static readonly string[] Eu4ScopeWords = { "Root", "From", "This", "Prev" };

    /// <summary>Bereichsglieder, die weder Event-Target (<c>[X.GetY]</c>) noch Variable (<c>[Root.X.GetValue]</c>) sind.</summary>
    private static readonly string[] Eu4NonTargets = { "Root", "From", "This", "Prev", "Monarch", "Heir", "Consort", "Capital", "Owner", "Controller" };

    /// <summary>Anhänge, mit denen <c>[?X…]</c> die Zeit oder den Wert eines Flags liest.</summary>
    private static readonly string[] FlagTimeSuffixes = { ":days_left", ":days", ":value" };

    /// <summary>Glieder vor einem Flag-Namen in <c>[?…:days]</c>, die nur den Bereich wählen (<c>global.</c>, <c>ROOT.</c> …).</summary>
    private static readonly string[] FlagScopePrefixes = { "global", "root", "from", "prev", "this", "owner" };

    /// <summary>
    /// .gui und Lokalisierungs-.yml: Var('x'), GetVariable('x'), HasVariable('x'), GetGlobalVariable('x'), GlobalVariable('x'),
    /// GetLocalVariable('x'), LocalVariable('x'), VariableList('x'), GetVariableList('x'), GetList('x') (Victoria 3), auch mit doppelten Anführungszeichen.
    /// Art je nach Funktionsname (Variable, GlobalVariable, LocalVariable, VariableList), Rolle immer Read. <c>#</c>-Kommentare
    /// (außerhalb von Anführungszeichen) bleiben unbeachtet. <paramref name="localization"/> (.yml): Der Wert einer Zeile <c>key:N "..."</c>
    /// reicht vom ersten <c>"</c> nach dem Schlüssel bis zum letzten <c>"</c> der Zeile; ein <c>#</c> darin (z. B. <c>#P ... #!</c>) ist
    /// Formatierung, auch wenn der Text ungeschützte <c>"</c> enthält. Kommentar ist nur ein <c>#</c> davor oder danach.
    /// </summary>
    /// <remarks>
    /// <paramref name="style"/> wählt die Leseart der Tabelle: <see cref="LocStyle.DataFunctions"/> wie oben (EU V, CK III, Victoria 3);
    /// <see cref="LocStyle.Hoi4Brackets"/> liest nur Lokalisierung mit <c>[?x]</c> (siehe <see cref="ReadHoi4Brackets"/>), <c>.gui</c> ergibt nichts.
    /// <see cref="LocStyle.Eu4Brackets"/> und <see cref="LocStyle.StellarisBrackets"/> lesen nur Lokalisierung (siehe <see cref="ReadEu4Brackets"/>, <see cref="ReadStellarisBrackets"/>).
    /// </remarks>
    public static FileScan Read(string text, bool localization = false, LocStyle style = LocStyle.DataFunctions) => style switch
    {
        LocStyle.DataFunctions => ReadDataFunctions(text, localization),
        LocStyle.Hoi4Brackets when localization => ReadHoi4Brackets(text),
        LocStyle.Eu4Brackets when localization => ReadEu4Brackets(text),
        LocStyle.StellarisBrackets when localization => ReadStellarisBrackets(text),
        _ => new FileScan(),
    };

    /// <summary>Letztes Glied von <c>[a.b.c]</c>, das eine Stellaris-Variable sein kann: nur Kleinbuchstaben, Ziffern und <c>_</c>.</summary>
    private static readonly Regex StellarisVariable = new(@"^[a-z_][a-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Stellaris Lokalisierung, Zeilen mit <c>#</c> am Anfang und Kommentare wie bei <see cref="StripLocalizationComment"/> ausgenommen; Klammern mit
    /// <c>$</c> oder Leerraum zählen nicht:
    /// <list type="bullet">
    /// <item><c>[event_target:X…]</c>: X (bis zum ersten <c>.</c>) ist ein gelesenes Event-Target.</item>
    /// <item><c>[a.b.c]</c> bzw. <c>[a]</c>, kein Glied beginnt mit <c>Get</c> und das letzte besteht nur aus Kleinbuchstaben, Ziffern und <c>_</c>:
    /// das letzte Glied ist eine gelesene Variable als Kandidat (<see cref="RawSite.IsCandidate"/>; zählt nur, wenn das Skript sie setzt).</item>
    /// </list>
    /// </summary>
    private static FileScan ReadStellarisBrackets(string text)
    {
        const string EventTargetPrefix = "event_target:";
        var scan = new FileScan();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith('#'))
            {
                continue;
            }

            var line = StripLocalizationComment(lines[i]);
            foreach (Match match in Eu4Bracket.Matches(line))
            {
                var content = match.Groups[1].Value.Trim();
                if (content.Length == 0 || content.Contains('$') || content.Any(char.IsWhiteSpace))
                {
                    continue;
                }

                var segments = content.Split('.');
                if (segments[0].StartsWith(EventTargetPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var target = segments[0][EventTargetPrefix.Length..];
                    if (Identifier.IsMatch(target))
                    {
                        scan.Sites.Add(new RawSite(target, FlagKind.EventTarget, null, FlagRole.Read, i + 1));
                    }
                }

                if (!segments.Any(s => s.StartsWith("Get", StringComparison.Ordinal)) && StellarisVariable.IsMatch(segments[^1]))
                {
                    scan.Sites.Add(new RawSite(segments[^1], FlagKind.Variable, null, FlagRole.Read, i + 1, IsCandidate: true));
                }
            }
        }

        return scan;
    }

    private static FileScan ReadDataFunctions(string text, bool localization)
    {
        var scan = new FileScan();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = localization ? StripLocalizationComment(lines[i]) : StripComment(lines[i]);
            foreach (Match match in Call.Matches(line))
            {
                scan.Sites.Add(new RawSite(match.Groups[3].Value, KindOf(match.Groups[1].Value), null, FlagRole.Read, i + 1));
            }

            foreach (Match match in TimeRemaining.Matches(line))
            {
                scan.Sites.Add(new RawSite(match.Groups[2].Value, FlagKind.Variable, null, FlagRole.Read, i + 1));
            }
        }

        return scan;
    }

    /// <summary>
    /// HOI IV Lokalisierung: jedes <c>[?X]</c> (bis <c>]</c>, <c>[</c> oder <c>|</c>) ist gelesen, Kommentare wie bei <see cref="StripLocalizationComment"/>.
    /// <c>event_target:X…</c> ergibt ein Event-Target <c>X</c> (bis zum ersten <c>.</c>). Endet X auf <c>:days</c>, <c>:days_left</c> oder <c>:value</c>,
    /// ist es ein Flag (ScopeFlag ohne Familie) ohne diesen Anhang. Sonst eine Variable: hinten angehängte <c>.Get…</c>-Glieder fallen weg, dann
    /// <see cref="NameShapes.Hoi4VariableName"/>. Ein <c>@</c>-Anhang mit Bereichswort macht jeweils ein Muster (<see cref="NameShapes.AtSuffix"/>).
    /// </summary>
    private static FileScan ReadHoi4Brackets(string text)
    {
        var scan = new FileScan();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripLocalizationComment(lines[i]);
            foreach (Match match in Hoi4Bracket.Matches(line))
            {
                if (Hoi4BracketSite(match.Groups[1].Value.Trim(), i + 1) is { } site)
                {
                    scan.Sites.Add(site);
                }
            }
        }

        return scan;
    }

    /// <summary>
    /// EU IV Lokalisierung, Zeilen mit <c>#</c> am Anfang und Kommentare wie bei <see cref="StripLocalizationComment"/> ausgenommen:
    /// <list type="bullet">
    /// <item><c>[Scope.var.GetValue]</c> (3 und mehr Glieder, das letzte <c>GetValue</c>, das erste Root/From/This/Prev, eine Zahl oder ein
    /// Bezeichner): das vorletzte Glied ist eine gelesene Variable, außer es ist ein Bereichsglied (Root, From, This, Prev, Monarch, Heir, Consort,
    /// Capital, Owner, Controller).</item>
    /// <item><c>[X.GetY]</c> (2 Glieder): X ist ein gelesenes Event-Target als Kandidat (<see cref="RawSite.IsCandidate"/>), außer X ist eines dieser
    /// Bereichsglieder, eine Zahl oder ein Ländertag.</item>
    /// </list>
    /// </summary>
    private static FileScan ReadEu4Brackets(string text)
    {
        var scan = new FileScan();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith('#'))
            {
                continue;
            }

            var line = StripLocalizationComment(lines[i]);
            foreach (Match match in Eu4Bracket.Matches(line))
            {
                if (Eu4BracketSite(match.Groups[1].Value.Trim().Split('.'), i + 1) is { } site)
                {
                    scan.Sites.Add(site);
                }
            }
        }

        return scan;
    }

    private static RawSite? Eu4BracketSite(string[] segments, int line)
    {
        static bool IsAny(string word, string[] words) => Array.Exists(words, w => string.Equals(w, word, StringComparison.OrdinalIgnoreCase));
        static bool IsNumber(string word) => word.Length > 0 && word.All(char.IsAsciiDigit);

        if (segments.Length >= 3 && segments[^1] == "GetValue")
        {
            var first = segments[0];
            var variable = segments[^2];
            var scopeOk = IsAny(first, Eu4ScopeWords) || IsNumber(first) || Identifier.IsMatch(first);
            return scopeOk && Identifier.IsMatch(variable) && !IsAny(variable, Eu4NonTargets)
                ? new RawSite(variable, FlagKind.Variable, null, FlagRole.Read, line)
                : null;
        }

        if (segments.Length == 2 && segments[1].StartsWith("Get", StringComparison.Ordinal))
        {
            var target = segments[0];
            return Identifier.IsMatch(target) && !IsAny(target, Eu4NonTargets) && !Eu4Tag.IsMatch(target)
                ? new RawSite(target, FlagKind.EventTarget, null, FlagRole.Read, line, IsCandidate: true)
                : null;
        }

        return null;
    }

    private static RawSite? Hoi4BracketSite(string content, int line)
    {
        const string EventTargetPrefix = "event_target:";
        FlagKind kind;
        string raw;
        if (content.StartsWith(EventTargetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            kind = FlagKind.EventTarget;
            raw = content[EventTargetPrefix.Length..];
            var dot = raw.IndexOf('.');
            raw = dot >= 0 ? raw[..dot] : raw;
        }
        else if (FlagTimeSuffixes.FirstOrDefault(s => content.EndsWith(s, StringComparison.OrdinalIgnoreCase)) is { } suffix)
        {
            kind = FlagKind.ScopeFlag;
            raw = content[..^suffix.Length];

            // Vorne nur Bereichswörter und global. abschneiden; andere Punkte gehören zum Flag-Namen (baltic.5_fired).
            int dot;
            while ((dot = raw.IndexOf('.')) > 0 && Array.Exists(FlagScopePrefixes, p => string.Equals(p, raw[..dot], StringComparison.OrdinalIgnoreCase)))
            {
                raw = raw[(dot + 1)..];
            }
        }
        else
        {
            kind = FlagKind.Variable;
            var segments = content.Split('.').ToList();
            while (segments.Count > 1 && segments[^1].StartsWith("Get", StringComparison.Ordinal))
            {
                segments.RemoveAt(segments.Count - 1);
            }

            raw = string.Join(".", segments);
        }

        if (kind == FlagKind.Variable && FlagCommandTable.Hoi4.IsBuiltInVariable(NameShapes.Hoi4VariableName(raw)))
        {
            // Eingebaute Variable ([?ROOT.num_of_factories]): keine Fundstelle (FlagCommandTable.BuiltInVariables).
            return null;
        }

        var (name, isPattern) = NameShapes.AtSuffix(raw);
        if (kind == FlagKind.Variable)
        {
            name = NameShapes.Hoi4VariableName(name);
        }

        return name.Length == 0 || name.Contains('$') ? null : new RawSite(name, kind, null, FlagRole.Read, line, isPattern);
    }

    private static FlagKind KindOf(string function) => function switch
    {
        "GetGlobalVariable" or "GlobalVariable" => FlagKind.GlobalVariable,
        "GetLocalVariable" or "LocalVariable" => FlagKind.LocalVariable,
        "VariableList" or "GetVariableList" or "GetList" => FlagKind.VariableList,
        _ => FlagKind.Variable,
    };

    private static string StripLocalizationComment(string line)
    {
        var hash = line.IndexOf('#');
        if (hash < 0)
        {
            return line;
        }

        var colon = line.IndexOf(':');
        var open = colon < 0 ? -1 : line.IndexOf('"', colon + 1);
        if (open < 0 || hash < open)
        {
            // Kein Wert in Anführungszeichen oder das # steht davor: Kommentar ab dem ersten #.
            return line[..hash];
        }

        var close = line.LastIndexOf('"');
        if (close == open)
        {
            // Nicht geschlossener Wert: alles danach gilt als Text.
            return line;
        }

        var after = line.IndexOf('#', close + 1);
        return after < 0 ? line : line[..after];
    }

    private static string StripComment(string line)
    {
        var hash = line.IndexOf('#');
        if (hash < 0)
        {
            return line;
        }

        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '\\' && quoted)
            {
                i++;
            }
            else if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == '#' && !quoted)
            {
                return line[..i];
            }
        }

        return line;
    }
}
