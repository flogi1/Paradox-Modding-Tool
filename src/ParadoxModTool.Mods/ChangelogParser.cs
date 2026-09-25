using System.Text.RegularExpressions;

namespace ParadoxModTool.Mods;

/// <summary>
/// Ein Versions-Abschnitt eines Changelogs. Zeilen sind 0-basierte Indizes in <see cref="ChangelogParser.SplitLines"/>: <see cref="BannerLine"/> ist die erste
/// Rautenzeile des Banners, <see cref="BodyStart"/> die Zeile nach dem Banner, <see cref="EndLine"/> die erste Zeile des nächsten Versions-Banners (oder das Dateiende).
/// </summary>
/// <param name="Version">Die Nummer, wie sie im Titel steht (z. B. <c>2.0.5</c>).</param>
/// <param name="Title">Der Titel des Banners ohne Rauten (z. B. <c>Idea Variation 2 Version 2.0.5</c>).</param>
/// <param name="Text">Der Inhalt zwischen diesem und dem nächsten Versions-Banner, ohne führende und abschließende Leerzeilen, Zeilenumbruch <c>\n</c>.</param>
public sealed record ChangelogEntry(string Version, string Title, int BannerLine, int BodyStart, int EndLine, string Text);

/// <summary>
/// Liest die Versions-Abschnitte einer Changelog-Datei im Stil der Paradox-Mods: ein Banner aus einer Rautenzeile, einer Titelzeile <c>####### Titel</c> und einer weiteren
/// Rautenzeile. Ein Banner ist ein <b>Versions-Banner</b>, wenn der Titel <c>Version &lt;Zahl(.Zahl)*&gt;</c> enthält (auch <c>Version 1.3.0</c> ohne Modname oder
/// <c>… (Version 2.0.0)</c>). Andere Banner (<c>General</c>, <c>Ideas</c>, …) sind Unterabschnitte und bleiben im Text der Version; alles vor dem ersten Versions-Banner ist der Kopf.
/// </summary>
public static class ChangelogParser
{
    private static readonly Regex HashLine = new(@"^#{10,}\s*$", RegexOptions.Compiled);
    private static readonly Regex TitleLine = new(@"^#{3,}[ \t]+([^#\s].*?)\s*$", RegexOptions.Compiled);
    private static readonly Regex VersionInTitle = new(@"\bVersion\s+(\d+(?:\.\d+)*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Teilt in Zeilen (<c>\r\n</c> und <c>\n</c>); ein abschließender Zeilenumbruch ergibt ein leeres letztes Element, damit <c>string.Join</c> die Datei wieder ergibt.</summary>
    public static string[] SplitLines(string text) => text.Replace("\r\n", "\n").Split('\n');

    public static IReadOnlyList<ChangelogEntry> Parse(string text)
    {
        var lines = SplitLines(text);
        var banners = new List<(string Version, string Title, int Line)>();
        for (var i = 0; i + 2 < lines.Length; i++)
        {
            if (!HashLine.IsMatch(lines[i]) || !HashLine.IsMatch(lines[i + 2]))
            {
                continue;
            }

            var title = TitleLine.Match(lines[i + 1]);
            if (!title.Success || VersionInTitle.Match(title.Groups[1].Value) is not { Success: true } version)
            {
                continue;
            }

            banners.Add((version.Groups[1].Value, title.Groups[1].Value, i));
            i += 2;
        }

        var entries = new List<ChangelogEntry>();
        for (var b = 0; b < banners.Count; b++)
        {
            var bodyStart = banners[b].Line + 3;
            var end = b + 1 < banners.Count ? banners[b + 1].Line : lines.Length;
            entries.Add(new ChangelogEntry(banners[b].Version, banners[b].Title, banners[b].Line, bodyStart, end, BodyText(lines, bodyStart, end)));
        }

        return entries;
    }

    /// <summary>Der Text der Zeilen <paramref name="start"/> bis <paramref name="end"/> (ausschließlich) ohne führende und abschließende Leerzeilen.</summary>
    internal static string BodyText(string[] lines, int start, int end)
    {
        while (start < end && string.IsNullOrWhiteSpace(lines[start]))
        {
            start++;
        }

        while (end > start && string.IsNullOrWhiteSpace(lines[end - 1]))
        {
            end--;
        }

        return string.Join("\n", lines[start..end]);
    }
}
