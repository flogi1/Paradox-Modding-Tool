using System.Text;

namespace ParadoxModTool.GuiDesign.Designer;

public enum GuiScaffoldKind
{
    Empty,
    Window,
}

/// <summary>
/// Gerüst für "Neue GUI-Datei" (Plan-Ruling 10): leer oder ein Fenster aus den Standardteilen von Vanilla (Hintergrund, Rand, Titelleiste,
/// Schließen-Button). Fehlt ein Teil im Index (anderes Spiel, anderer Stand), entfällt er. Der Schließen-Button bekommt keine Aktion; die
/// braucht Skript.
/// </summary>
public static class GuiScaffold
{
    public const string HeaderType = "window_header_alt";

    public const string CloseType = "button_close_alt";

    public const string BackgroundTemplate = "bg_window_default_alt";

    public const string MarginTemplate = "window_margin_alt";

    /// <summary>Widget-Name aus dem Dateinamen: Kleinbuchstaben, Ziffern und "_" (alles andere wird "_"), ohne Endung.</summary>
    public static string NameFor(string fileName)
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

    public static string Text(GuiScaffoldKind kind, string name, Func<string, bool> isType, Func<string, bool> isTemplate)
    {
        if (kind == GuiScaffoldKind.Empty)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        text.Append("window = {\n");
        text.Append($"\tname = \"{name}\"\n");
        text.Append("\tparentanchor = center\n");
        text.Append("\twidgetanchor = center\n");
        text.Append("\tsize = { 600 400 }\n");
        text.Append("\tmovable = yes\n");
        if (isTemplate(BackgroundTemplate))
        {
            text.Append($"\tusing = {BackgroundTemplate}\n");
        }

        text.Append("\n\tvbox = {\n");
        if (isTemplate(MarginTemplate))
        {
            text.Append($"\t\tusing = {MarginTemplate}\n\n");
        }

        if (isType(HeaderType))
        {
            text.Append($"\t\t{HeaderType} = {{\n");
            text.Append("\t\t\tblockoverride \"header_text\" {\n");
            text.Append($"\t\t\t\ttext = \"{name.ToUpperInvariant()}\"\n");
            text.Append("\t\t\t}\n");
            text.Append("\t\t}\n\n");
        }

        text.Append("\t\twidget = {\n");
        text.Append("\t\t\tlayoutpolicy_horizontal = expanding\n");
        text.Append("\t\t\tlayoutpolicy_vertical = expanding\n");
        text.Append("\t\t}\n");
        text.Append("\t}\n");
        if (isType(CloseType))
        {
            text.Append($"\n\t{CloseType} = {{ }}\n");
        }

        text.Append("}\n");
        return text.ToString();
    }
}
