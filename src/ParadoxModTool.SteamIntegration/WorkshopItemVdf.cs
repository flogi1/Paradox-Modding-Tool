using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.SteamIntegration;

/// <summary>
/// Die Angaben einer VDF-Datei für <c>workshop_build_item</c>. Optionale Felder, die <see langword="null"/> sind, stehen nicht in der Datei; SteamCMD lässt sie dann bei einem
/// Update unverändert.
/// </summary>
/// <param name="PublishedFileId"><c>0</c> beim ersten Veröffentlichen, sonst die Workshop-ID.</param>
/// <param name="ContentFolder">Absoluter Pfad des Ordners, dessen Inhalt hochgeladen wird.</param>
public sealed record WorkshopItem(
    string AppId,
    string PublishedFileId,
    string ContentFolder,
    string ChangeNote,
    WorkshopVisibility? Visibility = null,
    string? Title = null,
    string? Description = null,
    string? PreviewFile = null);

/// <summary>Schreibt und liest die VDF-Datei, die SteamCMD für <c>+workshop_build_item</c> erwartet.</summary>
public static class WorkshopItemVdf
{
    private static readonly Regex PublishedFileIdPattern = new("\"publishedfileid\"\\s+\"(\\d+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Build(WorkshopItem item)
    {
        var text = new StringBuilder();
        text.Append("\"workshopitem\"\n{\n");
        Append(text, "appid", item.AppId);
        Append(text, "publishedfileid", item.PublishedFileId);
        Append(text, "contentfolder", item.ContentFolder);
        if (item.PreviewFile is not null)
        {
            Append(text, "previewfile", item.PreviewFile);
        }

        if (item.Visibility is { } visibility)
        {
            Append(text, "visibility", ((int)visibility).ToString());
        }

        if (item.Title is not null)
        {
            Append(text, "title", item.Title);
        }

        if (item.Description is not null)
        {
            Append(text, "description", item.Description);
        }

        Append(text, "changenote", item.ChangeNote);
        text.Append("}\n");
        return text.ToString();
    }

    /// <summary>Die Zahl in <c>publishedfileid</c> (SteamCMD trägt beim ersten Veröffentlichen die neue ID ein); <see langword="null"/>, wenn das Feld fehlt oder keine Zahl ist.</summary>
    public static string? ReadPublishedFileId(string vdfText)
    {
        var match = PublishedFileIdPattern.Match(vdfText);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static void Append(StringBuilder text, string key, string value) =>
        text.Append('\t').Append('"').Append(key).Append("\"\t\t\"").Append(Escape(value)).Append("\"\n");

    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n")
        .Replace("\r", "\\n")
        .Replace("\t", "\\t");
}
