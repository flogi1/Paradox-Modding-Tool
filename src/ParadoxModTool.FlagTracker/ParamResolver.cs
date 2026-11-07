using System.Text.RegularExpressions;

namespace ParadoxModTool.FlagTracker;

/// <summary>Das Leseergebnis einer Datei mit ihrem Ort und ihrer Herkunft, so wie die Parameter-Auflösung es braucht. File.Line ist ohne Bedeutung.</summary>
public sealed record FlagScannedFile(SiteLocation File, FlagOrigin Origin, string OriginName, FileScan Scan);

/// <summary>Löst Namen mit <c>$PARAM$</c> in Scripted Effects und Triggers über deren Aufrufstellen zu Fundstellen auf.</summary>
public static class ParamResolver
{
    /// <summary>Höchstzahl der Durchreich-Schritte (Aufruf in einer Definition, der <c>$X$</c> weitergibt).</summary>
    public const int MaxDepth = 5;

    /// <summary><c>$P$</c> oder (Stellaris) <c>$P|vorgabe$</c>; Gruppe 1 der Name, Gruppe 2 der Vorgabewert.</summary>
    private static readonly Regex Param = new(@"\$([A-Za-z0-9_]+)(?:\|([^$]*))?\$", RegexOptions.Compiled);

    /// <summary>Ein Parameterwert, der genau ein <c>$X$</c> bzw. <c>$X|v$</c> ist (Durchreichen); jeder andere Wert mit <c>$</c> wird beim Einsetzen zu <c>*</c>.</summary>
    private static readonly Regex WholeParam = new(@"^\$[A-Za-z0-9_]+(?:\|[^$]*)?\$$", RegexOptions.Compiled);

    /// <summary>Rest eines <c>$</c> nach dem Einsetzen (z. B. Wert <c>$kaputt</c>); wird zu <c>*</c>.</summary>
    private static readonly Regex Leftover = new(@"\$[A-Za-z0-9_]*(?:\|[^$]*)?\$?", RegexOptions.Compiled);

    /// <summary>EU IV <c>[[P]text]</c> bzw. <c>[[!P]text]</c> in einem Namen (innerster Teil ohne weitere Klammern).</summary>
    private static readonly Regex Conditional = new(@"\[\[(!?)([A-Za-z0-9_]+)\]([^\[\]]*)\]", RegexOptions.Compiled);

    /// <summary>
    /// Erzeugt Fundstellen aus allen Vorlagen (TemplateSite) der Dateien.
    /// <paramref name="effective"/>: Definition (Name, Effect oder Trigger) → Datei mit der wirksamen Definition; nur deren Vorlagen zählen.
    /// Ob Effect oder Trigger, folgt aus der Datei, in der die Vorlage bzw. der Aufruf steht. Aufrufe werden nur nach Namen zugeordnet
    /// (ein Aufruf kann einen gleichnamigen Effect oder Trigger meinen und fächert für beide auf).
    /// null = alle Definitionen zählen (Bereich „Alle installierten“), dann werden die Vorlagen aus jeder Datei gesammelt, die den Namen definiert.
    /// Ein Name, der in <paramref name="effective"/> fehlt, zählt ebenfalls in jeder Datei.
    /// </summary>
    /// <remarks>
    /// Regeln:
    /// <list type="bullet">
    /// <item>Jedes <c>$P$</c> wird mit dem Wert des Aufrufs ersetzt (Namen groß/klein-genau, mehrfach und mehrere verschiedene erlaubt).
    /// Ein skalarer Wert wird eingesetzt (auch <c>yes</c> und Zahlen); fehlt der Parameter, ist er ein Block oder enthält er <c>:</c>
    /// (z. B. <c>scope:x</c>) oder beginnt er mit <c>@</c> (Skript-Konstante), wird daraus <c>*</c>. Ein übrig gebliebenes <c>$</c>
    /// im fertigen Namen wird ebenfalls zu <c>*</c>.</item>
    /// <item>Aufrufe in einer Definition, die laut <paramref name="effective"/> in einer anderen Datei wirksam ist (überschrieben),
    /// zählen nicht: Sie laufen im Spiel nie und fächern nicht auf.</item>
    /// <item>Enthält der Wert selbst <c>$X$</c> (Durchreichen) und steht der Aufruf in einer Definition D, geht es mit dem eingesetzten
    /// Text weiter über jeden Aufruf von D (Auffächern, eine Fundstelle je äußerem Aufruf). Alle Parameter einer Vorlage werden dabei
    /// zusammen über denselben äußeren Aufruf aufgelöst, es entsteht kein Kreuzprodukt.</item>
    /// <item>Ort der Fundstelle ist der äußerste erreichte Aufruf (dort steht der konkrete Wert), Herkunft die seiner Datei.
    /// Via ist die Vorlagenzeile in der innersten Definition, ViaName deren Name.</item>
    /// <item>Bricht die Kette ab (Aufruf außerhalb einer Definition, D ohne Aufrufe, mehr als <see cref="MaxDepth"/> Schritte oder ein
    /// Kreis, also D steht schon in der Kette), wird der Rest zu <c>*</c> (bzw. zum Vorgabewert von <c>$P|v$</c>) und die Fundstelle ist am Aufruf,
    /// an dem die Kette endet; ein Muster, wenn ein <c>*</c> bleibt oder <c>[[P]</c> zwei Varianten ergibt.
    /// Bei einer Selbstrekursion (A ruft A mit <c>{ P = $P$ }</c>) geht dadurch nichts verloren: Der äußere Aufruf von A liefert den
    /// konkreten Namen schon auf dem direkten Weg.</item>
    /// <item>EU IV <c>[[P]text]</c> im Namen: <c>text</c> zählt, wenn der Aufruf P übergibt (bei <c>[[!P]</c>, wenn nicht); das wird vor dem Einsetzen
    /// der <c>$P$</c> aufgelöst. Ohne Aufruf entstehen zwei Muster, mit und ohne <c>text</c>. Bringt ein eingesetzter Wert selbst ein <c>[[P]</c> mit,
    /// gehört P zur umgebenden Definition: Es geht wie beim Durchreichen nach außen weiter; endet die Kette, entstehen beide Varianten als Muster.</item>
    /// <item>Stellaris <c>$P|v$</c>: Fehlt P im Aufruf (oder gibt es keinen Aufruf), gilt v. Ohne Aufruf ist die Stelle dann kein Muster,
    /// wenn jeder Parameter so einen Wert bekam (und kein <c>[[P]</c> im Namen steht).</item>
    /// <item>Stellaris-Inline-Skripte sind Definitionen <c>inline_script:&lt;pfad&gt;</c>; ihre Pfade gelten ohne Rücksicht auf Groß/Klein (<see cref="CalleeNameComparer"/>). Ein Aufruf mit <c>$</c> im Pfad (dynamisch) trifft jedes
    /// Inline-Skript, auf dessen Namen der Pfad passt (<c>$…$</c> = beliebig): Dessen Vorlagen werden Muster am Aufruf, Via = Vorlagenzeile.</item>
    /// <item>Eine Definition mit Vorlagen ohne jeden (auch dynamischen) Aufruf ergibt je Vorlage ein Muster an der Definitionszeile, Via = Vorlagenzeile.</item>
    /// <item>Eine Vorlage außerhalb jeder Definition ergibt ein Muster an ihrer eigenen Zeile ohne Via (Stellaris Script-Values).</item>
    /// <item>Gleiche Ergebnisse (gleicher Datensatz) erscheinen nur einmal.</item>
    /// </list>
    /// Die Aufrufe werden einmal nach Aufgerufenem gruppiert; je Vorlage und Aufruf ist der Aufwand die Zahl der Wege durch die Aufrufkette.
    /// </remarks>
    public static IReadOnlyList<FlagSite> Resolve(IReadOnlyList<FlagScannedFile> files, IReadOnlyDictionary<DefinitionKey, FlagScannedFile>? effective)
    {
        var callsByCallee = new Dictionary<string, List<(FlagScannedFile File, CallSite Call)>>(CalleeNameComparer.Instance);
        foreach (var file in files)
        {
            foreach (var call in file.Scan.Calls)
            {
                // Ein Aufruf in einer überschriebenen (nicht wirksamen) Definition läuft im Spiel nie.
                if (effective is not null && call.CallerDefinition is { } caller
                    && effective.TryGetValue(new DefinitionKey(caller, IsTriggerFile(file)), out var callerOwner) && callerOwner != file)
                {
                    continue;
                }

                if (!callsByCallee.TryGetValue(call.Callee, out var list))
                {
                    list = new List<(FlagScannedFile, CallSite)>();
                    callsByCallee[call.Callee] = list;
                }

                list.Add((file, call));
            }
        }

        var result = new List<FlagSite>();
        var seen = new HashSet<FlagSite>();

        void Add(FlagSite site)
        {
            if (seen.Add(site))
            {
                result.Add(site);
            }
        }

        var dynamicCalls = DynamicCalls(callsByCallee);
        foreach (var file in files)
        {
            foreach (var template in file.Scan.Templates)
            {
                if (template.DefinitionName is not { } definition)
                {
                    foreach (var variant in Variants(template.NameTemplate))
                    {
                        var name = Star(variant);
                        Add(new FlagSite(
                            name, template.Kind, template.Family, template.Role,
                            At(file, template.Line), file.Origin, file.OriginName, IsPattern: IsUncalledPattern(template.NameTemplate, name)));
                    }

                    continue;
                }

                if (effective is not null && effective.TryGetValue(new DefinitionKey(definition, IsTriggerFile(file)), out var owner) && owner != file)
                {
                    continue;
                }

                var via = At(file, template.Line);
                var dynamicHit = false;
                var inlineDefinition = definition.StartsWith(ScriptReader.InlineScriptPrefix, StringComparison.Ordinal);
                foreach (var (path, dynamicFile, dynamicCall) in inlineDefinition ? dynamicCalls : Enumerable.Empty<(Regex, FlagScannedFile, CallSite)>())
                {
                    if (!path.IsMatch(definition))
                    {
                        continue;
                    }

                    dynamicHit = true;
                    foreach (var variant in Variants(template.NameTemplate))
                    {
                        Add(new FlagSite(
                            Star(variant), template.Kind, template.Family, template.Role, At(dynamicFile, dynamicCall.Line), dynamicFile.Origin,
                            dynamicFile.OriginName, via, definition, IsPattern: true));
                    }
                }

                if (!callsByCallee.TryGetValue(definition, out var calls))
                {
                    if (dynamicHit)
                    {
                        continue;
                    }

                    var definitionLine = file.Scan.Definitions.FirstOrDefault(d => d.Name == definition)?.Line ?? template.Line;
                    foreach (var variant in Variants(template.NameTemplate))
                    {
                        var name = Star(variant);
                        Add(new FlagSite(
                            name, template.Kind, template.Family, template.Role,
                            At(file, definitionLine), file.Origin, file.OriginName, via, definition, IsUncalledPattern(template.NameTemplate, name)));
                    }

                    continue;
                }

                var chain = new List<string> { definition };
                foreach (var (callFile, call) in calls)
                {
                    Expand(template.NameTemplate, callFile, call, 0, chain, callsByCallee, (name, siteFile, line, pattern) =>
                        Add(new FlagSite(
                            name, template.Kind, template.Family, template.Role, At(siteFile, line), siteFile.Origin, siteFile.OriginName,
                            via, definition, pattern || name.Contains('*'))));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Setzt die Parameter von <paramref name="call"/> in <paramref name="text"/> ein und reicht offen gebliebene <c>$X$</c> an die
    /// Aufrufe der umgebenden Definition weiter. <paramref name="depth"/> zählt die Durchreich-Schritte, <paramref name="chain"/> die
    /// Definitionen auf dem Weg (Kreisschutz).
    /// </summary>
    private static void Expand(
        string text,
        FlagScannedFile file,
        CallSite call,
        int depth,
        List<string> chain,
        Dictionary<string, List<(FlagScannedFile File, CallSite Call)>> callsByCallee,
        Action<string, FlagScannedFile, int, bool> emit)
    {
        if (text.Contains("[["))
        {
            text = ResolveConditionals(text, p => call.Params is not null && call.Params.ContainsKey(p));
        }

        var substituted = Param.Replace(text, m =>
        {
            if (call.Params is null || !call.Params.ContainsKey(m.Groups[1].Value))
            {
                // Fehlt der Parameter im Aufruf, gilt der Vorgabewert von $P|v$ (Stellaris).
                return m.Groups[2].Success ? m.Groups[2].Value : "*";
            }

            if (!call.Params.TryGetValue(m.Groups[1].Value, out var value) || value is null
                || value.Length == 0 || value.Contains(':') || value.StartsWith('@')
                || (value.Contains('$') && !WholeParam.IsMatch(value)))
            {
                return "*";
            }

            return value;
        });

        // Ein [[P] … ] aus einem eingesetzten Wert gehört zu den Parametern der umgebenden Definition: wie ein offenes $X$ weiterreichen.
        var open = substituted.Contains("[[");
        if (!open && !Param.IsMatch(substituted))
        {
            emit(Star(substituted), file, call.Line, false);
            return;
        }

        if (call.CallerDefinition is not { } caller
            || depth >= MaxDepth
            || chain.Contains(caller, CalleeNameComparer.Instance)
            || !callsByCallee.TryGetValue(caller, out var outerCalls))
        {
            foreach (var variant in Variants(substituted))
            {
                // Muster, solange ein * bleibt oder [[P] zwei Varianten macht; ein Vorgabewert ($S|2$) ergibt einen konkreten Namen.
                var name = Star(variant);
                emit(name, file, call.Line, IsUncalledPattern(substituted, name));
            }

            return;
        }

        chain.Add(caller);
        foreach (var (outerFile, outerCall) in outerCalls)
        {
            Expand(substituted, outerFile, outerCall, depth + 1, chain, callsByCallee, emit);
        }

        chain.RemoveAt(chain.Count - 1);
    }

    /// <summary>
    /// Setzt jedes <c>[[P]text]</c> ein: <c>text</c>, wenn <paramref name="passed"/>(P) gilt, sonst nichts; bei <c>[[!P]text]</c> umgekehrt.
    /// Verschachtelte Teile werden von innen nach außen aufgelöst.
    /// </summary>
    private static string ResolveConditionals(string text, Func<string, bool> passed)
    {
        string previous;
        do
        {
            previous = text;
            text = Conditional.Replace(text, m => passed(m.Groups[2].Value) != (m.Groups[1].Value == "!") ? m.Groups[3].Value : string.Empty);
        }
        while (text != previous);

        return text;
    }

    /// <summary>
    /// Ohne Aufruf: ein Name mit <c>[[P]text]</c> ergibt zwei Varianten, mit allen Teilen und ohne sie (EU IV); jeder andere Name sich selbst.
    /// Gleiche Varianten erscheinen nur einmal.
    /// </summary>
    private static IEnumerable<string> Variants(string template)
    {
        if (!template.Contains("[["))
        {
            return new[] { template };
        }

        return new[] { ResolveConditionals(template, _ => true), ResolveConditionals(template, _ => false) }.Distinct(StringComparer.Ordinal);
    }

    /// <summary>Jedes offene <c>$P$</c> und jeder übrige <c>$</c>-Rest wird zu <c>*</c>; ein <c>$P|v$</c> zu seinem Vorgabewert v.</summary>
    private static string Star(string text) => Leftover.Replace(Param.Replace(text, m => m.Groups[2].Success ? m.Groups[2].Value : "*"), "*");

    /// <summary>Ohne Aufruf: Muster, außer jeder Parameter hatte einen Vorgabewert (kein <c>*</c>) und es gab kein <c>[[P]</c>, das zwei Varianten macht.</summary>
    private static bool IsUncalledPattern(string template, string name) => name.Contains('*') || template.Contains("[[");

    /// <summary>
    /// Dynamische Inline-Skript-Aufrufe (Pfad mit <c>$</c>): je Aufruf ein Ausdruck, in dem jedes <c>$…$</c> des Pfads für beliebige Zeichen steht;
    /// ohne Rücksicht auf Groß/Klein wie <see cref="CalleeNameComparer"/>.
    /// </summary>
    private static List<(Regex Path, FlagScannedFile File, CallSite Call)> DynamicCalls(
        Dictionary<string, List<(FlagScannedFile File, CallSite Call)>> callsByCallee)
    {
        var result = new List<(Regex, FlagScannedFile, CallSite)>();
        foreach (var (callee, calls) in callsByCallee)
        {
            if (!callee.StartsWith(ScriptReader.InlineScriptPrefix, StringComparison.Ordinal) || !callee.Contains('$'))
            {
                continue;
            }

            var glob = Star(Param.Replace(callee, "*"));
            var regex = new Regex("^" + Regex.Escape(glob).Replace(@"\*", ".*") + "$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            foreach (var (file, call) in calls)
            {
                result.Add((regex, file, call));
            }
        }

        return result;
    }

    /// <summary>Alle Definitionen einer Datei sind gleicher Art (der Ordner entscheidet); ohne Definitionen: false.</summary>
    private static bool IsTriggerFile(FlagScannedFile file) => file.Scan.Definitions.Count > 0 && file.Scan.Definitions[0].IsTrigger;

    private static SiteLocation At(FlagScannedFile file, int line) => file.File with { Line = line };
}
