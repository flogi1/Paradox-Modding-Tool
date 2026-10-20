using System.Text;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Hilfen für synthetische Schrift-Tests: Suchwurzeln mit <c>fonts/test.font</c> und zur Laufzeit kopierten Windows-Schriften (keine Schriftdateien im
/// Repository).
/// </summary>
internal static class JominiTestFonts
{
    public static readonly string WindowsFonts = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", "Fonts");

    /// <summary>Legt <paramref name="temp"/>/<paramref name="name"/> an, schreibt <c>fonts/test.font</c> und kopiert die genannten Windows-Schriften nach <c>fonts/a/</c>.</summary>
    public static string Root(string temp, string name, string fontText, params string[] windowsFonts)
    {
        var root = Path.Combine(temp, name);
        var fonts = Path.Combine(root, "fonts");
        Directory.CreateDirectory(Path.Combine(fonts, "a"));
        File.WriteAllText(Path.Combine(fonts, "test.font"), fontText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        foreach (var file in windowsFonts)
        {
            File.Copy(Path.Combine(WindowsFonts, file), Path.Combine(fonts, "a", file));
        }

        return root;
    }

    /// <summary>Pfad einer kopierten Schrift unter <c>fonts/a/</c>.</summary>
    public static string Copy(string root, string file) => Path.Combine(root, "fonts", "a", file);

    public static string FontFiles(string name, params (string Languages, string Files)[] groups) =>
        "fontfiles = {\n\tname = \"" + name + "\"\n\talways_load = yes\n" +
        string.Concat(groups.Select(g => "\tgroup = {\n\t\tlanguages = { " + g.Languages + " }\n\t\tfiles = { " + g.Files + " }\n\t}\n")) +
        "}\n";

    public static string Font(string name, params (string Style, string FontFiles)[] styles) =>
        "font = {\n\tname = \"" + name + "\"\n" +
        string.Concat(styles.Select(s => "\tfontstyle = {\n\t\tstyle = " + s.Style + "\n\t\tfontfiles = \"" + s.FontFiles + "\"\n\t}\n")) +
        "\tunderlineformats = { default = { thickness = 1 offset = 0.12 } }\n}\n";

    /// <summary>Löscht den Temp-Ordner; WPF kann eine Schriftdatei noch eingeblendet halten, Aufräumen ist deshalb nur ein Versuch.</summary>
    public static void TryDelete(string temp)
    {
        try
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
