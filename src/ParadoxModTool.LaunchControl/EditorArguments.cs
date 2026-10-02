using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ParadoxModTool.LaunchControl;

/// <summary>Was eine Weitergabe an ein laufendes PMT tun soll.</summary>
public enum HandOffKind
{
    /// <summary>Datei (und Zeile) öffnen, wie vom Spiel als externem Editor.</summary>
    OpenFile,

    /// <summary>Mod aktiv machen und ihren Ordner im Explorer zeigen (Übersicht "Meine Mods").</summary>
    OpenMod,

    /// <summary>Releases-Fenster der Mod öffnen.</summary>
    OpenReleases,

    /// <summary>Bugtracker auf das Projekt der Mod stellen, optional einen Bug auswählen.</summary>
    OpenBug,
}

/// <summary>
/// Datei (und Zeile), die ein Spiel PMT als externem Editor übergibt (Spec "PMT als externer Editor des Spiels"), oder ein Befehl der Übersicht
/// "Meine Mods" (<see cref="Kind"/> ungleich <see cref="HandOffKind.OpenFile"/>, dann ist <see cref="Path"/> leer).
/// </summary>
public sealed record EditorRequest(string Path, int? Line)
{
    public HandOffKind Kind { get; init; } = HandOffKind.OpenFile;

    public string? ModId { get; init; }

    public string? BugId { get; init; }

    public static EditorRequest ForMod(HandOffKind kind, string modId, string? bugId = null) =>
        new(string.Empty, null) { Kind = kind, ModId = modId, BugId = bugId };
}

/// <summary>
/// Liest Datei und Zeile aus den Startargumenten (Plan 4 Ruling 1). Wie das Spiel den Editor aufruft, ist unbekannt; erkannt werden die Datei als
/// ein Argument oder an Leerzeichen zerteilt, <c>datei:zeile</c>, <c>datei(zeile)</c>, eine Zahl nach der Datei sowie <c>-n12</c>, <c>+12</c>,
/// <c>-l12</c> und <c>--line=12</c>. Nur existierende Dateien zählen; andere Argumente mit <c>--</c> werden übergangen, <c>--game</c> samt Wert.
/// </summary>
public static class EditorArguments
{
    private static readonly Regex LineOption = new(@"^(?:-n|-l|\+|--line=)(?<line>\d+)$", RegexOptions.CultureInvariant);

    private static readonly Regex LineSuffix = new(@"^(?<path>.+?)(?::(?<line>\d+)(?::\d+)?|\((?<line2>\d+)(?:,\s*\d+)?\))$", RegexOptions.CultureInvariant);

    public static EditorRequest? Parse(IReadOnlyList<string> args, Func<string, bool> fileExists)
    {
        int? optionLine = null;
        var parts = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (LineOption.Match(arg) is { Success: true } option)
            {
                optionLine = PositiveOrNull(option.Groups["line"].Value);
                continue;
            }

            if (arg == "--game")
            {
                i++;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            parts.Add(arg);
        }

        // Längste zusammenhängende Folge zuerst: ein nicht in Anführungszeichen stehender Pfad kommt an Leerzeichen zerteilt an.
        for (var start = 0; start < parts.Count; start++)
        {
            for (var end = parts.Count; end > start; end--)
            {
                var candidate = string.Join(" ", parts.Skip(start).Take(end - start)).Trim().Trim('"');
                if (candidate.Length == 0)
                {
                    continue;
                }

                if (fileExists(candidate))
                {
                    return new EditorRequest(FullPath(candidate), optionLine ?? (end < parts.Count ? PositiveOrNull(parts[end]) : null));
                }

                if (LineSuffix.Match(candidate) is { Success: true } suffix && fileExists(suffix.Groups["path"].Value))
                {
                    var line = suffix.Groups["line"].Success ? suffix.Groups["line"].Value : suffix.Groups["line2"].Value;
                    return new EditorRequest(FullPath(suffix.Groups["path"].Value), PositiveOrNull(line) ?? optionLine);
                }
            }
        }

        return null;
    }

    /// <summary>Voller Pfad (Plan 4 Ruling 1): Das empfangende PMT hat einen anderen Arbeitsordner, und "/" ergäbe einen zweiten Tab.</summary>
    private static string FullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    /// <summary>
    /// Ein Argument außer Optionen mit <c>--</c> und dem Wert von <c>--game</c>: Aufruf als externer Editor, auch wenn das Format unbekannt ist (Review
    /// Plan 4). Dann darf PMT nicht still ein anderes Spiel öffnen.
    /// </summary>
    public static bool LooksLikeEditorCall(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--game")
            {
                i++;
                continue;
            }

            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static int? PositiveOrNull(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0 ? number : null;
}

/// <summary>
/// Nachricht über die Pipe zwischen zwei PMT-Prozessen: die <see cref="EditorRequest"/> als JSON. Eine Datei-Anfrage hat kein <c>Kind</c> und sieht
/// damit aus wie vor den Befehlen; ältere PMT-Versionen verstehen sie weiter.
/// </summary>
public static class EditorHandOffMessage
{
    private static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static string Encode(EditorRequest request) => JsonSerializer.Serialize(
        new Wire(
            request.Kind == HandOffKind.OpenFile ? request.Path : null,
            request.Kind == HandOffKind.OpenFile ? request.Line : null,
            request.Kind == HandOffKind.OpenFile ? null : request.Kind.ToString(),
            request.ModId,
            request.BugId),
        Options);

    /// <summary>
    /// Null bei leerem oder kaputtem Text, unbekanntem Befehl und fehlenden Angaben: eine Datei braucht einen Pfad (eine Zeile unter 1 gilt als keine),
    /// jeder Befehl eine Mod-ID; die Bug-ID ist freiwillig.
    /// </summary>
    public static EditorRequest? Decode(string text)
    {
        try
        {
            var wire = JsonSerializer.Deserialize<Wire>(text);
            if (wire is null)
            {
                return null;
            }

            if (wire.Kind is null)
            {
                return wire.Path is { Length: > 0 } path ? new EditorRequest(path, wire.Line is > 0 ? wire.Line : null) : null;
            }

            if (!Enum.TryParse<HandOffKind>(wire.Kind, ignoreCase: false, out var kind) || kind == HandOffKind.OpenFile || !Enum.IsDefined(kind)
                || wire.ModId is not { Length: > 0 } modId)
            {
                return null;
            }

            return EditorRequest.ForMod(kind, modId, wire.BugId is { Length: > 0 } bugId && kind == HandOffKind.OpenBug ? bugId : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal sealed record Wire(string? Path, int? Line, string? Kind = null, string? ModId = null, string? BugId = null);
}
