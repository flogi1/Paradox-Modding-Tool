using System.IO;
using System.Text;
using ParadoxModTool.GuiDesign.Blocks;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// "Neue GUI-Datei" im klassischen Format (HOI IV, EU IV; Plan 3 Task 9): Die Datei liegt in <c>&lt;Mod&gt;/interface</c>, auf Wunsch in einem Unterordner,
/// und enthält ein Fenster aus dem Baustein "Fenster" in <c>guiTypes</c>.
/// </summary>
public static class ClassicNewFile
{
    /// <summary>Name des Bausteins, aus dem das Fenster der neuen Datei entsteht.</summary>
    public const string WindowBlockName = "Fenster";

    /// <summary>Ordner der neuen Datei: <c>&lt;Mod&gt;\interface</c>, mit gültigem Unterordner <c>&lt;Mod&gt;\interface\&lt;Unterordner&gt;</c>.</summary>
    public static string Folder(string modRoot, string? subfolder)
    {
        var folder = Path.Combine(modRoot, "interface");
        var segments = Segments(subfolder);
        return segments is { Count: > 0 } ? Path.Combine(new[] { folder }.Concat(segments).ToArray()) : folder;
    }

    /// <summary>
    /// Prüft den Unterordner: leer ist erlaubt; abgelehnt werden absolute Pfade, <c>..</c>, unzulässige Zeichen und (EU IV) ein Ordner, den ein
    /// <c>replace_path</c> der Mod nennt, solange das Profil <c>interface</c> als Absturz-Wurzel führt (dieselbe Regel wie <see cref="Checks.ReplacePathCheck"/>
    /// und die Mod-Kompatibilität: eine .gui-Datei dort lässt das Spiel beim Start abstürzen, gemessen 2026-09-30).
    /// </summary>
    /// <returns>Der Fehlertext oder <see langword="null"/>.</returns>
    public static string? SubfolderProblem(string modRoot, GuiLayout layout, string? subfolder)
    {
        if (string.IsNullOrWhiteSpace(subfolder))
        {
            return null;
        }

        var segments = Segments(subfolder);
        if (segments is null)
        {
            return GuiTexts.NewFileInvalidSubfolder;
        }

        var relative = "interface/" + string.Join('/', segments);
        if (layout.ReplacePathGuiCrashRoots is { Count: > 0 } roots
            && roots.Any(r => string.Equals(r, "interface", StringComparison.OrdinalIgnoreCase))
            && global::ParadoxModTool.Mods.ModMetadata.Read(modRoot, layout.MetadataFile ?? string.Empty)?.ReplacePaths is { } entries
            && entries.Select(global::ParadoxModTool.Mods.ReplacePaths.Normalize).Any(e => string.Equals(e, relative, StringComparison.OrdinalIgnoreCase)))
        {
            return GuiTexts.NewFileReplacePathSubfolder(relative);
        }

        return null;
    }

    /// <summary>
    /// Pfad der neuen Datei relativ zur Wurzel mit "/" (<c>interface/&lt;Unterordner&gt;/&lt;Datei&gt;</c>), die Teile des Unterordners so bereinigt wie in
    /// <see cref="Folder"/>; ein unzulässiger Unterordner zählt wie keiner.
    /// </summary>
    public static string RelativePath(string? subfolder, string fileName) =>
        string.Join('/', new[] { "interface" }.Concat(Segments(subfolder) ?? new List<string>()).Append(fileName));

    /// <summary>Die Teile des Unterordners oder <see langword="null"/>, wenn er unzulässig ist; leer gibt eine leere Liste.</summary>
    private static List<string>? Segments(string? subfolder)
    {
        var text = subfolder?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return new List<string>();
        }

        if (Path.IsPathRooted(text) || text.StartsWith('/') || text.StartsWith('\\') || text.Contains(':'))
        {
            return null;
        }

        var parts = text.Replace('\\', '/').Split('/', StringSplitOptions.TrimEntries).Where(p => p.Length > 0).ToList();
        var invalid = Path.GetInvalidFileNameChars();
        return parts.Count == 0 || parts.Any(p => p is "." or ".." || p.IndexOfAny(invalid) >= 0) ? null : parts;
    }

    /// <summary>
    /// Inhalt der neuen Datei: <c>guiTypes = { … }</c> mit dem Baustein (Vorgabewerte, <c>name</c> = <paramref name="windowName"/>), einen Tabulator eingerückt.
    /// </summary>
    public static string Scaffold(GuiBlock windowBlock, string windowName)
    {
        var values = GuiBlockValues.Defaults(windowBlock);
        if (windowBlock.Parameters.Any(p => p.Key == "name"))
        {
            values.Values["name"] = windowName;
        }

        var body = GuiBlockRenderer.Render(windowBlock, values).Replace("\r\n", "\n").TrimEnd('\n');
        var text = new StringBuilder("guiTypes = {\n");
        foreach (var line in body.Split('\n'))
        {
            text.Append(line.Length == 0 ? string.Empty : "\t" + line).Append('\n');
        }

        return text.Append("}\n").ToString();
    }

    /// <summary>Anzeigename der Aktion, die das neue Fenster ins Spiel bringt (Eigenschaftsfeld), oder <see langword="null"/>.</summary>
    public static string? ScriptActionName(GuiLayout layout) => layout.ScriptWiring switch
    {
        GuiScriptWiring.ScriptedGuis => GuiTexts.ScriptedGuiCreate.TrimEnd('…'),
        GuiScriptWiring.CustomGui => GuiTexts.CustomGuiBuildInto.TrimEnd('…'),
        _ => null,
    };
}
