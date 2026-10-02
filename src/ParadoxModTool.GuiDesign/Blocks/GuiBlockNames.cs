using System.Text;

namespace ParadoxModTool.GuiDesign.Blocks;

public static class GuiBlockNames
{
    public const string Extension = ".guiblock";

    /// <summary>Widget-Name aus dem Dateinamen: Kleinbuchstaben, Ziffern und "_" (alles andere wird "_"), ohne Endung .gui.</summary>
    public static string WidgetNameFor(string fileName)
    {
        var stem = fileName.Trim();
        if (stem.EndsWith(".gui", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^4];
        }

        var builder = new StringBuilder();
        foreach (var c in stem.ToLowerInvariant())
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        }

        var name = builder.ToString().Trim('_');
        while (name.Contains("__", StringComparison.Ordinal))
        {
            name = name.Replace("__", "_", StringComparison.Ordinal);
        }

        return name.Length == 0 ? "new_window" : name;
    }

    /// <summary>Dateiname für einen Baustein: im Dateinamen unzulässige Zeichen werden "_", leer wird "block".</summary>
    public static string FileNameFor(string blockName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(blockName.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return (name.Length == 0 ? "block" : name) + Extension;
    }
}
