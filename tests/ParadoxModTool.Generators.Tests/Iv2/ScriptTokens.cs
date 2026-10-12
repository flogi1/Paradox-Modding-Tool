using System.Text;

namespace ParadoxModTool.Generators.Tests.Iv2;

/// <summary>Skripttext als Token-Folge: Kommentare (<c>#</c> außerhalb von Anführungszeichen) weg, Text in Anführungszeichen als ein Token, Trennung an Leerraum, <c>{</c>, <c>}</c>, <c>=</c>.</summary>
internal static class ScriptTokens
{
    public static IReadOnlyList<string> Of(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        void Flush()
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                Flush();
                var end = text.IndexOf('"', i + 1);
                end = end < 0 ? text.Length - 1 : end;
                tokens.Add(text[i..(end + 1)]);
                i = end;
            }
            else if (c == '#')
            {
                Flush();
                var end = text.IndexOf('\n', i);
                i = end < 0 ? text.Length : end;
            }
            else if (c is '{' or '}' or '=')
            {
                Flush();
                tokens.Add(c.ToString());
            }
            else if (char.IsWhiteSpace(c) || c == '﻿')
            {
                Flush();
            }
            else
            {
                current.Append(c);
            }
        }

        Flush();
        return tokens;
    }
}

/// <summary>
/// Fixture-Bündel: mehrere Dateien in einer Textdatei, je mit Kopfzeile <c>##FILE &lt;relativer Pfad&gt;</c> (die IV2-Pfade sind im Repo zu lang für
/// Windows). Geschrieben von <c>make_expected.py</c>; Zeilenenden als LF.
/// </summary>
internal static class Bundle
{
    private const string Mark = "##FILE ";

    public static IReadOnlyDictionary<string, string> Read(string path)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        foreach (var chunk in text.Split(Mark).Skip(1))
        {
            var newLine = chunk.IndexOf('\n');
            files[chunk[..newLine]] = chunk[(newLine + 1)..];
        }

        return files;
    }

    /// <summary>Die Dateien eines Bündels in einen Ordner schreiben (UTF-8 mit BOM, CRLF außer .yml, wie der IV2-Generator).</summary>
    public static void Unpack(string path, string folder)
    {
        foreach (var (relative, content) in Read(path))
        {
            var full = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var text = relative.EndsWith(".yml", StringComparison.Ordinal) ? content : content.Replace("\n", "\r\n");
            File.WriteAllText(full, text, new UTF8Encoding(true));
        }
    }
}

/// <summary>Lokalisierung als Schlüssel → Wert. PMT schreibt <c>\"</c> und <c>\n</c>, Python <c>'</c> und Leerzeichen; mit <c>pythonStyle: false</c> wird PMT auf die Python-Form gebracht.</summary>
internal static class YmlMap
{
    public static IReadOnlyDictionary<string, string> Of(string text, bool pythonStyle)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("l_", StringComparison.Ordinal) && line.EndsWith(':'))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            var open = line.IndexOf('"');
            var close = line.LastIndexOf('"');
            if (colon < 0 || open < colon || close <= open)
            {
                continue;
            }

            var value = line[(open + 1)..close];
            if (!pythonStyle)
            {
                value = value.Replace("\\\"", "'").Replace("\\n", " ");
            }

            map[line[..colon].Trim()] = value.Trim();
        }

        return map;
    }
}
