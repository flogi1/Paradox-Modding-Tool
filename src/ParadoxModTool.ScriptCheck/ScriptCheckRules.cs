using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.ScriptCheck;

/// <summary>
/// Regeln der Skript-Prüfung für ein Spiel (Datei <c>profiles/scriptcheck/&lt;spiel&gt;.json</c>): wo Events, Effekte, Trigger und Skriptwerte liegen,
/// wie Events aufgerufen werden und welche Events sich selbst auslösen. Die Regeln sind reine Daten, der Code kennt kein Spiel.
/// </summary>
public sealed class ScriptCheckRules
{
    private static readonly string[] PhaseFolders = { "in_game", "main_menu", "loading_screen" };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly JsonSerializerOptions NormalizeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Ordner mit Event-Dateien (Schrägstrich-Pfade relativ zum Mod-Stamm).</summary>
    public IReadOnlyList<string> EventFolders { get; init; } = Array.Empty<string>();

    /// <summary>Ob Unterordner der Event-Ordner geladen werden (Stellaris: nein).</summary>
    public bool EventSubfolders { get; init; } = true;

    /// <summary>
    /// Ob Events über Parameter eines <c>inline_script</c>-Blocks oberster Ebene in den Event-Dateien entstehen (Stellaris, <c>EVENT_ID = shroud.10420</c>).
    /// Jeder Skalar-Parameter in Event-ID-Form zählt dann als selbstauslösende Event-Definition, weil man nicht sieht, was das Inline-Skript damit tut.
    /// </summary>
    public bool InlineScriptEventParams { get; init; }

    /// <summary>
    /// Stammpräfixe, die vor den Ordnern (und vor den Phasenordnern) stehen dürfen, z. B. <c>dlc/*/</c> für die DLC-Inhalte von EU V
    /// (<c>game/dlc/D001_x/in_game/events</c>). <c>*</c> steht für genau einen Ordnernamen; höchstens ein Präfix wird entfernt.
    /// </summary>
    public IReadOnlyList<string> RootPrefixes { get; init; } = Array.Empty<string>();

    /// <summary>Namen der Event-Blöcke auf oberster Ebene (Spiele mit <c>country_event = { id = … }</c> als Definition); leer = <c>namespace.N</c>-Form.</summary>
    public IReadOnlyList<string> EventBlocks { get; init; } = Array.Empty<string>();

    /// <summary>Schlüssel, die ein Event aufrufen (<c>trigger_event = x</c> oder <c>{ id = x }</c>).</summary>
    public IReadOnlyList<string> EventCallKeys { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> OnActionFolders { get; init; } = Array.Empty<string>();

    /// <summary>Schlüssel in On-Actions, deren Inhalt Event-IDs auflistet (<c>events</c>, <c>random_events</c>, <c>first_valid</c>).</summary>
    public IReadOnlyList<string> OnActionEventLists { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EffectFolders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TriggerFolders { get; init; } = Array.Empty<string>();

    /// <summary>Ordner mit Skriptwerten; leer = das Spiel hat keine (EU IV, HOI IV).</summary>
    public IReadOnlyList<string> ValueFolders { get; init; } = Array.Empty<string>();

    /// <summary>Ordner mit benannten Blöcken, die man als <c>name = yes</c> oder <c>name = { PARAM = … }</c> aufruft, die aber weder Effekt noch Trigger sind (CK III <c>common/scripted_modifiers</c>); ihre Schlüssel sind bekannte Namen.</summary>
    public IReadOnlyList<string> KnownNameFolders { get; init; } = Array.Empty<string>();

    /// <summary>Felder im Event, die es von selbst auslösen (<c>mean_time_to_happen</c>, <c>dynamic_historical_event</c>).</summary>
    public IReadOnlyList<string> SelfFiringFields { get; init; } = Array.Empty<string>();

    /// <summary>Feld, das ein Event als nur ausgelöst kennzeichnet (<c>is_triggered_only</c>); null = das Spiel kennt keins.</summary>
    public string? TriggeredOnlyField { get; init; }

    /// <summary>Schlüssel, in deren Block Effekte oder Trigger stehen (Kontext für die Aufruf-Erkennung).</summary>
    public IReadOnlyList<string> ScriptContextKeys { get; init; } = Array.Empty<string>();

    /// <summary>"Durchsichtige" Schlüssel (<c>AND</c>, <c>OR</c>, <c>NOT</c>, <c>hidden_effect</c> …); zusammen mit <see cref="ScriptContextKeys"/> ändern sie nichts an der Ausführung. Jeder andere Vorfahr (Kontrollfluss, Scope-Wechsel, Schleifen) macht einen Aufruf bedingt.</summary>
    public IReadOnlyList<string> TransparentKeys { get; init; } = Array.Empty<string>();

    /// <summary>Schlüssel, deren Block für die Aufruf-Erkennung Skript enthält, die aber nicht durchsichtig sind (Mechanik-Hooks wie <c>can_construct</c>, Tooltip-Blöcke wie <c>show_as_tooltip</c>): Ein Zyklus über sie ist nie ein Error. Kein <c>modifier</c>/<c>weight</c>: dort stehen auch Daten.</summary>
    public IReadOnlyList<string> CallContextKeys { get; init; } = Array.Empty<string>();

    /// <summary>Ordner, deren Dateien für die Prüfung kein Skript sind (EU IV <c>patchnotes</c>, im Installationsordner); auch Dateien einer Mod dort werden nicht gelesen.</summary>
    public IReadOnlyList<string> IgnoredFolders { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Funktionen der englischen Lokalisierung, deren Argument einen Skriptwert, Trigger oder Effekt nennt (CK III, Vic3, EU V: <c>ScriptValue</c>,
    /// z. B. <c>[GetPlayer.MakeScope.ScriptValue('x')]</c>). Ein solcher Aufruf zählt als Verwendung des Namens; ein bloß gleiches Wort nie.
    /// Leer = das Spiel hat keine; dann werden die <c>.yml</c>-Dateien gar nicht gelesen.
    /// </summary>
    public IReadOnlyList<string> LocCallFunctions { get; init; } = Array.Empty<string>();

    /// <summary>Zusätzliche eingebaute Namen, die nie als fehlender Effekt oder Trigger gemeldet werden.</summary>
    public IReadOnlyList<string> ExtraBuiltins { get; init; } = Array.Empty<string>();

    /// <summary>Events, die die Engine selbst auslöst (kein Aufrufer im Skript nötig).</summary>
    public IReadOnlyList<string> EngineEvents { get; init; } = Array.Empty<string>();

    /// <summary>Regeln für teure globale Iteratoren (Objekt <c>performance</c>); null = das Spiel hat keine.</summary>
    public ScriptCheckPerformanceRules? Performance { get; init; }

    /// <summary>Quelle und Datum der Messung; nur zur Information.</summary>
    public string? Notes { get; init; }

    /// <summary>SHA-256 des normalisierten JSON, die ersten 16 Hex-Zeichen; ändert sich mit jeder Regeländerung (Cache-Schlüssel).</summary>
    [JsonIgnore]
    public string Fingerprint
    {
        get
        {
            var json = JsonSerializer.Serialize(this, NormalizeOptions);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(hash).ToLowerInvariant()[..16];
        }
    }

    /// <summary>Liest eine Regeldatei; wirft bei ungültigem JSON.</summary>
    public static ScriptCheckRules Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ScriptCheckRules>(json, ReadOptions)
            ?? throw new JsonException("Leere Regeldatei: " + path);
    }

    /// <summary>Regeln des Spielprofils; null ohne Verweis im Profil oder ohne Datei.</summary>
    public static ScriptCheckRules? ForProfile(GameProfile profile, string profilesDirectory)
    {
        if (string.IsNullOrWhiteSpace(profile.ScriptCheckRules))
        {
            return null;
        }

        var path = Path.Combine(profilesDirectory, profile.ScriptCheckRules.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? Load(path) : null;
    }

    /// <summary>Betrifft die Regel der ständig geprüften Blöcke die Datei? Im Ordner (mit Unterordnern) und in keinem <see cref="CheckedBlockRule.ExcludeFolders"/>.</summary>
    public bool Matches(CheckedBlockRule rule, string relativePath) =>
        IsUnder(relativePath, new[] { rule.Folder }, true) && (rule.ExcludeFolders is not { Count: > 0 } excluded || !IsUnder(relativePath, excluded, true));

    /// <summary>
    /// Liegt der Pfad (relativ zum Mod-Stamm, Schrägstriche) in einem der Ordner? Die EU-V-Phasenordner (<c>in_game</c>, <c>main_menu</c>,
    /// <c>loading_screen</c>) dürfen davor stehen, davor wiederum eines der <see cref="RootPrefixes"/>. Ohne <paramref name="subfolders"/> zählt nur
    /// eine Datei direkt im Ordner.
    /// </summary>
    public bool IsUnder(string relativePath, IReadOnlyList<string> folders, bool subfolders)
    {
        var path = relativePath.Replace((char)92, '/').TrimStart('/');
        foreach (var rootPrefix in RootPrefixes)
        {
            if (StripPrefix(path, rootPrefix) is { } rest)
            {
                path = rest;
                break;
            }
        }

        foreach (var phase in PhaseFolders)
        {
            if (path.StartsWith(phase + "/", StringComparison.OrdinalIgnoreCase))
            {
                path = path[(phase.Length + 1)..];
                break;
            }
        }

        foreach (var folder in folders)
        {
            var prefix = folder.Trim('/') + "/";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (subfolders || path.IndexOf('/', prefix.Length) < 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Der Rest von <paramref name="path"/> hinter dem Präfix <paramref name="pattern"/> (Ordnernamen ohne Groß-/Kleinschreibung, <c>*</c> = genau ein
    /// nicht leerer Ordnername); null, wenn der Pfad nicht so beginnt oder danach nichts mehr folgt.
    /// </summary>
    private static string? StripPrefix(string path, string pattern)
    {
        var parts = pattern.Replace((char)92, '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var position = 0;
        foreach (var part in parts)
        {
            var slash = path.IndexOf('/', position);
            if (slash <= position)
            {
                return null;
            }

            var segment = path[position..slash];
            if (part != "*" && !string.Equals(segment, part, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            position = slash + 1;
        }

        return position < path.Length ? path[position..] : null;
    }
}
