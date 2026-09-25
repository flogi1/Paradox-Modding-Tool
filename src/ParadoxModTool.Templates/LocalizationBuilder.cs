using System.Text;
using System.Text.Json.Serialization;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.Templates;

/// <summary>
/// Eine Zeile der Lokalisierungstabelle: ein Schlüsselmuster mit Zähler-Platzhaltern (<c>idea_{n}_desc</c>) und ein Textmuster (<c>Idee {n}</c>).
/// Die Platzhalter <c>{n}</c>, <c>{n2}</c>, … stehen für die Zählpunkte der Ziffernblöcke des Schlüssels in Reihenfolge; im Text steht außerdem <c>{key}</c> für den ganzen Schlüssel.
/// </summary>
public sealed class LocalizationRow
{
    public string KeyPattern { get; set; } = string.Empty;

    public string TextPattern { get; set; } = "TODO";

    public bool Selected { get; set; }

    /// <summary>Die Zeile stammt von einem Blockschlüssel (<c>idea_1 = { … }</c>) und trägt eine Endung aus der Endungsliste; sonst von einem Wert (<c>name = idea_1</c>).</summary>
    public bool FromBlockKey { get; set; }

    /// <summary>Kennungen der Zählpunkte, die zu <c>{n}</c>, <c>{n2}</c>, … gehören.</summary>
    public List<string> PointKeys { get; set; } = new();

    /// <summary>Warum die Zeile vorgeschlagen wird, z. B. <c>Objektname · Endung _desc</c> oder <c>Feld text</c>.</summary>
    public string Origin { get; set; } = string.Empty;
}

public sealed class LocalizationSettings
{
    /// <summary>Lokalisierung mit erzeugen.</summary>
    public bool Enabled { get; set; }

    /// <summary>Sprache im Kopf der Datei (<c>l_english:</c>).</summary>
    public string Language { get; set; } = "english";

    /// <summary>Text neuer Zeilen.</summary>
    public string DefaultText { get; set; } = "TODO";

    /// <summary>Nur zum Lesen alter Vorlagen (früher die Endungsliste, meist <c>"", "_desc"</c>); siehe <see cref="MigrateLegacy"/>. Wird nicht mehr geschrieben.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Suffixes { get; set; }

    /// <summary>Gewähltes Profil: leer = automatisch (aus dem Ordner der Quelldatei), <c>*fallback</c> oder der Pfad eines gelernten Profils.</summary>
    public string ProfilePath { get; set; } = string.Empty;

    /// <summary>Haken dieser Vorlage, die vom Profil abweichen (Schlüssel: <see cref="LocalizationProfileResolver.ObjectKey"/> und <see cref="LocalizationProfileResolver.FieldKey"/>).</summary>
    public Dictionary<string, bool> ProfileChoices { get; set; } = new();

    /// <summary>Weitere Endungen dieser Vorlage, die zu jedem Blockschlüssel dazukommen (zusätzlich zum Profil).</summary>
    public List<string> ExtraSuffixes { get; set; } = new();

    /// <summary>
    /// Setzt die alte Endungsliste um: Endungen außer <c>""</c> und <c>_desc</c> werden zu <see cref="ExtraSuffixes"/>; fehlt <c>_desc</c>, wird es für alle Profile
    /// abgeschaltet. Danach ist <see cref="Suffixes"/> <see langword="null"/> und wird nicht mehr geschrieben.
    /// </summary>
    public void MigrateLegacy()
    {
        if (Suffixes is null)
        {
            return;
        }

        foreach (var suffix in Suffixes.Where(s => !string.IsNullOrWhiteSpace(s) && s != "_desc").Select(s => s.Trim()))
        {
            if (!ExtraSuffixes.Contains(suffix))
            {
                ExtraSuffixes.Add(suffix);
            }
        }

        if (!Suffixes.Contains("_desc"))
        {
            ProfileChoices[LocalizationProfileResolver.ObjectKey(LocalizationProfileResolver.AllProfiles, string.Empty, "_desc")] = false;
        }

        Suffixes = null;
    }

    public List<LocalizationRow> Rows { get; set; } = new();

    /// <summary>Einzeln bearbeitete Texte je erzeugtem Schlüssel; sie gelten, solange der Schlüssel gleich bleibt.</summary>
    public Dictionary<string, string> Overrides { get; set; } = new();
}

public sealed record LocalizationEntry(string Key, string Text);

public static class LocalizationBuilder
{
    /// <summary>
    /// Schlägt Zeilen für die Tabelle vor, nur nach dem Profil: Blockschlüssel auf oberster Ebene des Snippets (<c>marketplace = { … }</c>) ergeben je eingeschaltetem Paar aus
    /// Präfix und Endung eine Zeile, Werte in eingeschalteten Feldern (<c>text = "KEY"</c>) eine Zeile für den Wert. Es zählen nur Namen und Werte mit mindestens einem
    /// eingeschalteten Zählpunkt und ohne Leerzeichen; alles andere ergibt keine Zeile. Bereits bekannte Zeilen (gleiches Schlüsselmuster) behalten Auswahl und Text.
    /// </summary>
    public static List<LocalizationRow> Suggest(TemplateModel model, LocalizationSettings settings, ResolvedLocProfile profile)
    {
        var text = model.Text;
        var tokens = ScopeLexer.Tokenize(text);
        var pointAt = new Dictionary<int, VariationPoint>();
        foreach (var point in model.Points.Where(p => p.Enabled && p.Kind != PointKind.Number))
        {
            foreach (var occurrence in point.Occurrences)
            {
                pointAt[occurrence.Start] = point;
            }
        }

        var objects = profile.Objects.Where(o => o.Active).ToList();
        var fields = profile.Fields.Where(f => f.Active).Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = new List<LocalizationRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string keyPattern, bool fromBlockKey, List<string> pointKeys, string origin)
        {
            if (!seen.Add(keyPattern))
            {
                return;
            }

            rows.Add(new LocalizationRow
            {
                KeyPattern = keyPattern,
                Selected = true,
                FromBlockKey = fromBlockKey,
                PointKeys = pointKeys,
                TextPattern = settings.DefaultText,
                Origin = origin,
            });
        }

        var depth = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == ScopeTokenKind.Open)
            {
                depth++;
                continue;
            }

            if (token.Kind == ScopeTokenKind.Close)
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (token.Kind is not (ScopeTokenKind.Word or ScopeTokenKind.String))
            {
                continue;
            }

            var isBlockKey = depth == 0 && i + 2 < tokens.Count && tokens[i + 1].Kind == ScopeTokenKind.Operator && tokens[i + 2].Kind == ScopeTokenKind.Open;
            var isFieldValue = i >= 2
                && tokens[i - 1] is { Kind: ScopeTokenKind.Operator, Text: "=" }
                && tokens[i - 2].Kind == ScopeTokenKind.Word
                && fields.Contains(tokens[i - 2].Text);
            if (!isBlockKey && !isFieldValue)
            {
                continue;
            }

            // Bei einem Text in Anführungszeichen zählt nur der Inhalt, und nur ohne Leerzeichen kommt er als Schlüssel in Frage.
            var contentStart = token.Kind == ScopeTokenKind.String ? token.Start + 1 : token.Start;
            var contentEnd = Math.Min(token.End, text.Length);
            if (token.Kind == ScopeTokenKind.String && contentEnd > contentStart && text[contentEnd - 1] == '"')
            {
                contentEnd--;
            }

            if (token.Kind == ScopeTokenKind.String && text[contentStart..Math.Max(contentStart, contentEnd)].Any(char.IsWhiteSpace))
            {
                continue;
            }

            var pattern = BuildPattern(text, contentStart, contentEnd, pointAt, out var pointKeys);
            if (pointKeys.Count == 0)
            {
                continue;
            }

            if (isBlockKey)
            {
                foreach (var pair in objects)
                {
                    Add(pair.Prefix + pattern + pair.Suffix, true, new List<string>(pointKeys), DescribeObject(pair));
                }
            }
            else
            {
                Add(pattern, false, pointKeys, "Feld " + tokens[i - 2].Text);
            }
        }

        foreach (var row in rows)
        {
            var previous = settings.Rows.FirstOrDefault(r => r.KeyPattern == row.KeyPattern);
            if (previous is not null)
            {
                row.Selected = previous.Selected;
                row.TextPattern = previous.TextPattern;
            }
        }

        return rows;
    }

    private static string DescribeObject(ResolvedObjectPattern pair)
    {
        var parts = new List<string>();
        if (pair.Prefix.Length > 0)
        {
            parts.Add("Präfix " + pair.Prefix);
        }

        parts.Add("Objektname");
        if (pair.Suffix.Length > 0)
        {
            parts.Add("Endung " + pair.Suffix);
        }

        return string.Join(" · ", parts);
    }

    private static string BuildPattern(string text, int start, int end, Dictionary<int, VariationPoint> pointAt, out List<string> pointKeys)
    {
        pointKeys = new List<string>();
        var sb = new StringBuilder();
        var pos = start;
        while (pos < end)
        {
            if (pointAt.TryGetValue(pos, out var point))
            {
                var occurrence = point.Occurrences.First(o => o.Start == pos);
                pointKeys.Add(point.Key);
                sb.Append(pointKeys.Count == 1 ? "{n}" : "{n" + pointKeys.Count + "}");
                pos += occurrence.Length;
            }
            else
            {
                sb.Append(text[pos]);
                pos++;
            }
        }

        return sb.ToString();
    }

    /// <summary>Erzeugt die Einträge für <paramref name="count"/> Durchläufe. Zeilen, deren Zählpunkte fehlen oder ausgeschaltet sind, entfallen.</summary>
    public static List<LocalizationEntry> Build(TemplateModel model, LocalizationSettings settings, int count)
    {
        var entries = new List<LocalizationEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = settings.Rows.Where(r => r.Selected).ToList();

        for (var i = 0; i < count; i++)
        {
            foreach (var row in rows)
            {
                var points = row.PointKeys.Select(k => model.Points.FirstOrDefault(p => p.Key == k && p.Enabled)).ToList();
                if (points.Count == 0 || points.Any(p => p is null))
                {
                    continue;
                }

                var repeats = 1;
                foreach (var p in points)
                {
                    if (p!.RepeatGroup >= 0 && p.RepeatGroup < model.Groups.Count)
                    {
                        repeats = Math.Max(repeats, Math.Max(1, model.Groups[p.RepeatGroup].Count));
                    }
                }

                for (var j = 0; j < repeats; j++)
                {
                    var values = points.Select(p => p!.Format(i, j)).ToList();
                    var key = Substitute(row.KeyPattern, values);
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var text = settings.Overrides.TryGetValue(key, out var overridden)
                        ? overridden
                        : Substitute(row.TextPattern.Replace("{key}", key, StringComparison.Ordinal), values);
                    entries.Add(new LocalizationEntry(key, text));
                }
            }
        }

        return entries;
    }

    private static string Substitute(string pattern, IReadOnlyList<string> values)
    {
        // Höhere Nummern zuerst, damit {n2} nicht von {n} getroffen wird (die Muster sind verschieden, das ist nur zur Sicherheit bei Nachbesserungen).
        for (var s = values.Count; s >= 1; s--)
        {
            pattern = pattern.Replace(s == 1 ? "{n}" : "{n" + s + "}", values[s - 1], StringComparison.Ordinal);
        }

        return pattern;
    }

    /// <summary>Schreibt die Einträge im Format der Spiel-Lokalisierung (<c>l_english:</c>, danach <c> KEY: "Text"</c>).</summary>
    public static string ToYaml(IReadOnlyList<LocalizationEntry> entries, string language, string newline = "\n")
    {
        var sb = new StringBuilder();
        sb.Append(Header(language)).Append(newline);
        foreach (var entry in entries)
        {
            sb.Append(Line(entry)).Append(newline);
        }

        return sb.ToString();
    }

    public static string Header(string language) => "l_" + (string.IsNullOrWhiteSpace(language) ? "english" : language.Trim()) + ":";

    public static string Line(LocalizationEntry entry) =>
        " " + entry.Key + ": \"" + entry.Text.Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
}
