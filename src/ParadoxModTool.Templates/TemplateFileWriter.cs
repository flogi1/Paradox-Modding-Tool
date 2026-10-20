using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Templates;

public sealed record AppendResult(int Added, int Skipped, string? ExistingLanguage);

/// <summary>
/// Hängt erzeugten Code oder erzeugte Lokalisierung an eine Datei an. Die Textfunktionen arbeiten auf Zeichenketten, damit dieselbe Logik auch für ein offenes Dokument im Editor gilt;
/// die Dateifunktionen erhalten Kodierung (UTF-8 mit oder ohne BOM) und Zeilenenden einer vorhandenen Datei und legen fehlende Dateien mit UTF-8-BOM an
/// (Code-Dateien in der übergebenen Kodierung aus dem Spielprofil).
/// </summary>
public static class TemplateFileWriter
{
    private static readonly Regex KeyLine = new(@"^\s*([^\s:#""]+)\s*:", RegexOptions.Compiled);

    private static readonly Regex HeaderLine = new(@"^\s*l_([A-Za-z_]+)\s*:", RegexOptions.Compiled);

    /// <summary>Schlüssel, die in einem Lokalisierungstext schon vorkommen.</summary>
    public static HashSet<string> ExistingKeys(string yamlText)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in yamlText.TrimStart('﻿').Split('\n'))
        {
            if (HeaderLine.IsMatch(line))
            {
                continue;
            }

            var match = KeyLine.Match(line);
            if (match.Success)
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    public static string? ReadLanguage(string yamlText)
    {
        foreach (var line in yamlText.TrimStart('﻿').Split('\n'))
        {
            var match = HeaderLine.Match(line);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            if (line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Hängt Code an vorhandenen Text an, getrennt durch eine Leerzeile.</summary>
    public static string AppendCodeText(string existing, string code)
    {
        var newline = DetectNewline(existing);
        var body = NormalizeNewlines(code.TrimEnd(), newline);
        var trimmed = existing.TrimEnd();
        return trimmed.Length == 0 ? body + newline : trimmed + newline + newline + body + newline;
    }

    /// <summary>
    /// Hängt Einträge an vorhandenen Lokalisierungstext an. Fehlt der Sprachkopf, wird er vorangestellt. Bei <paramref name="skipDuplicates"/> werden Schlüssel, die schon vorkommen, ausgelassen.
    /// </summary>
    public static (string Text, AppendResult Result) AppendLocalizationText(string existing, IReadOnlyList<LocalizationEntry> entries, string language, bool skipDuplicates)
    {
        var newline = DetectNewline(existing);
        var existingKeys = ExistingKeys(existing);
        var toAdd = new List<LocalizationEntry>();
        var skipped = 0;
        foreach (var entry in entries)
        {
            if (skipDuplicates && existingKeys.Contains(entry.Key))
            {
                skipped++;
            }
            else
            {
                toAdd.Add(entry);
            }
        }

        var existingLanguage = ReadLanguage(existing);
        var sb = new StringBuilder(existing.TrimEnd());
        if (sb.Length == 0)
        {
            sb.Append(LocalizationBuilder.Header(language));
        }
        else if (existingLanguage is null)
        {
            sb.Insert(0, LocalizationBuilder.Header(language) + newline);
        }

        foreach (var entry in toAdd)
        {
            sb.Append(newline).Append(LocalizationBuilder.Line(entry));
        }

        sb.Append(newline);
        return (sb.ToString(), new AppendResult(toAdd.Count, skipped, existingLanguage));
    }

    /// <param name="newFileEncoding">Kodierung, falls die Datei neu angelegt wird (aus dem Spielprofil, Stellaris: ohne BOM); ohne Angabe UTF-8 mit BOM.</param>
    public static void AppendCodeToFile(string path, string code, Encoding? newFileEncoding = null)
    {
        var (existing, encoding) = ReadFile(path, newFileEncoding);
        Write(path, AppendCodeText(existing, code), encoding);
    }

    public static AppendResult AppendLocalizationToFile(string path, IReadOnlyList<LocalizationEntry> entries, string language, bool skipDuplicates)
    {
        var (existing, encoding) = ReadFile(path);
        var (text, result) = AppendLocalizationText(existing, entries, language, skipDuplicates);
        Write(path, text, encoding);
        return result;
    }

    /// <summary>Liest eine Datei, falls vorhanden; sonst leerer Text mit <paramref name="newFileEncoding"/> (ohne Angabe UTF-8-BOM) als Kodierung.</summary>
    public static (string Text, Encoding Encoding) ReadFile(string path, Encoding? newFileEncoding = null)
    {
        if (!File.Exists(path))
        {
            return (string.Empty, newFileEncoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom);
        return (encoding.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0)), encoding);
    }

    private static void Write(string path, string text, Encoding encoding)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, text, encoding);
    }

    private static string DetectNewline(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : text.Contains('\n') ? "\n" : "\n";

    private static string NormalizeNewlines(string text, string newline) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", newline, StringComparison.Ordinal);
}
