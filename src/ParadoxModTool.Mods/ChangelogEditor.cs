namespace ParadoxModTool.Mods;

/// <summary>
/// Ändert den Text einer Changelog-Datei (siehe <see cref="ChangelogParser"/>), ohne die übrigen Abschnitte anzufassen. Beide Funktionen arbeiten auf dem Dateitext
/// und geben den neuen Dateitext zurück; das Lesen und Schreiben der Datei gehört zu <see cref="ChangelogFile"/>. Die Zeilenenden der Datei bleiben erhalten
/// (Windows, wenn die Datei <c>\r\n</c> enthält, sonst <c>\n</c>).
/// </summary>
public static class ChangelogEditor
{
    private const string HashLine = "#############################################################";

    /// <summary>Ersetzt den Text der Version <paramref name="version"/> (Banner bleibt). Unveränderter Text lässt die Datei unverändert. Wirft <see cref="InvalidOperationException"/>, wenn es die Version nicht gibt.</summary>
    public static string SetText(string fileText, string version, string newText)
    {
        var entry = Find(ChangelogParser.Parse(fileText), version)
            ?? throw new InvalidOperationException($"Der Changelog enthält keine Version {version}.");
        if (newText.Replace("\r\n", "\n") == entry.Text)
        {
            return fileText;
        }

        var lines = ChangelogParser.SplitLines(fileText);
        var hasNext = entry.EndLine < lines.Length;

        var leading = 0;
        var trailing = 0;
        var bodyEnd = entry.EndLine;
        var bodyStart = entry.BodyStart;
        while (bodyStart < bodyEnd && string.IsNullOrWhiteSpace(lines[bodyStart]))
        {
            bodyStart++;
            leading++;
        }

        if (bodyStart == bodyEnd)
        {
            leading = 1;
            trailing = hasNext ? 1 : 0;
        }
        else
        {
            while (string.IsNullOrWhiteSpace(lines[bodyEnd - 1]))
            {
                bodyEnd--;
                trailing++;
            }
        }

        var replacement = new List<string>();
        replacement.AddRange(Enumerable.Repeat(string.Empty, leading));
        if (newText.Length > 0)
        {
            replacement.AddRange(newText.Replace("\r\n", "\n").Split('\n'));
        }

        replacement.AddRange(Enumerable.Repeat(string.Empty, trailing));

        var result = new List<string>(lines[..entry.BodyStart]);
        result.AddRange(replacement);
        result.AddRange(lines[entry.EndLine..]);
        return string.Join(NewLine(fileText), result);
    }

    /// <summary>
    /// Legt das Banner einer neuen Version mit leerem Unterabschnitt <c>General</c> direkt über der neuesten vorhandenen Version an (unter dem Kopf); gibt es noch
    /// keine Version, wird es angehängt. Wirft <see cref="InvalidOperationException"/>, wenn es die Version (nach Zahlenwert) schon gibt.
    /// </summary>
    public static string InsertVersion(string fileText, string version, string modName)
    {
        if (!VersionNumber.TryParse(version, out var number))
        {
            throw new InvalidOperationException($"\"{version}\" ist keine Versionsnummer.");
        }

        var entries = ChangelogParser.Parse(fileText);
        if (Find(entries, version) is not null)
        {
            throw new InvalidOperationException($"Der Changelog enthält die Version {number.Text} schon.");
        }

        var block = new List<string>
        {
            HashLine,
            $"####### {modName} Version {number.Text}",
            HashLine,
            string.Empty,
            HashLine,
            "####### General",
            HashLine,
            string.Empty,
            string.Empty,
        };

        var lines = new List<string>(ChangelogParser.SplitLines(fileText));
        if (entries.Count > 0)
        {
            lines.InsertRange(entries[0].BannerLine, block);
        }
        else
        {
            if (lines.Count > 0 && lines[^1].Length > 0)
            {
                lines.Add(string.Empty);
            }

            if (lines.Count > 1 || (lines.Count == 1 && lines[0].Length > 0))
            {
                lines.Add(string.Empty);
            }

            block.RemoveAt(block.Count - 1);
            lines.AddRange(block);
        }

        return string.Join(NewLine(fileText), lines);
    }

    private static ChangelogEntry? Find(IReadOnlyList<ChangelogEntry> entries, string version)
    {
        if (!VersionNumber.TryParse(version, out var wanted))
        {
            return null;
        }

        return entries.FirstOrDefault(e => VersionNumber.TryParse(e.Version, out var v) && v.Equals(wanted));
    }

    private static string NewLine(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
}
