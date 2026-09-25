using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ParadoxModTool.LaunchControl;

/// <summary>Datei (und Zeile), die ein Spiel PMT als externem Editor übergibt (Spec "PMT als externer Editor des Spiels").</summary>
public sealed record EditorRequest(string Path, int? Line);

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

/// <summary>Nachricht über die Pipe zwischen zwei PMT-Prozessen: die <see cref="EditorRequest"/> als JSON.</summary>
public static class EditorHandOffMessage
{
    public static string Encode(EditorRequest request) => JsonSerializer.Serialize(new Wire(request.Path, request.Line));

    /// <summary>Null bei leerem oder kaputtem Text und ohne Pfad; eine Zeile unter 1 gilt als keine.</summary>
    public static EditorRequest? Decode(string text)
    {
        try
        {
            var wire = JsonSerializer.Deserialize<Wire>(text);
            return wire?.Path is { Length: > 0 } path ? new EditorRequest(path, wire.Line is > 0 ? wire.Line : null) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal sealed record Wire(string? Path, int? Line);
}
