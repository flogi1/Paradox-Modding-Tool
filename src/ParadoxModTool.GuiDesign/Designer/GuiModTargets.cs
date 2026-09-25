using System.Text;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// Zieldateien der Mod für "In Mod übernehmen": Dateinamen je Mod einstellbar, der Ordner folgt aus dem Kontext der Quelldatei
/// (<c>&lt;Mod&gt;/in_game/gui/&lt;Datei&gt;</c>, Plan-Ruling 2). Typen stehen in <c>types &lt;Basis&gt;Overrides</c> bzw. <c>types &lt;Basis&gt;Types</c>.
/// </summary>
public sealed record GuiModTargets(string ModRoot, string OverrideFileName, string TypesFileName, string ContainerBase)
{
    public string OverrideContainer => ContainerBase + "Overrides";

    public string TypesContainer => ContainerBase + "Types";

    public string OverridePath(string context) => Path.Combine(GuiContexts.GuiFolder(ModRoot, context), OverrideFileName);

    public string TypesPath(string context) => Path.Combine(GuiContexts.GuiFolder(ModRoot, context), TypesFileName);

    /// <summary>Standardnamen aus dem Ordnernamen der Mod (<c>&lt;name&gt;_overrides.gui</c>, <c>&lt;name&gt;_types.gui</c>); gesetzte Namen gehen vor.</summary>
    public static GuiModTargets For(string modRoot, string? overrideFileName = null, string? typesFileName = null)
    {
        var baseName = BaseName(Path.GetFileName(modRoot.TrimEnd('\\', '/')));
        return new GuiModTargets(
            modRoot,
            string.IsNullOrWhiteSpace(overrideFileName) ? baseName + "_overrides.gui" : overrideFileName.Trim(),
            string.IsNullOrWhiteSpace(typesFileName) ? baseName + "_types.gui" : typesFileName.Trim(),
            Pascal(baseName));
    }

    /// <summary>Bereinigter Ordnername: Kleinbuchstaben, Ziffern und "_" (alles andere wird "_"), ohne doppelte "_"; leer ergibt "mod".</summary>
    public static string BaseName(string folderName)
    {
        var builder = new StringBuilder();
        foreach (var c in folderName.Trim().ToLowerInvariant())
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        }

        var name = builder.ToString();
        while (name.Contains("__", StringComparison.Ordinal))
        {
            name = name.Replace("__", "_", StringComparison.Ordinal);
        }

        name = name.Trim('_');
        return name.Length == 0 ? "mod" : name;
    }

    /// <summary>Gültiger Dateiname für eine Zieldatei: Endung .gui, ohne Ordner und ohne unzulässige Zeichen.</summary>
    public static bool IsValidFileName(string name) =>
        name.Length > 4
        && name.EndsWith(".gui", StringComparison.OrdinalIgnoreCase)
        && name.Trim() == name
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string Pascal(string baseName)
    {
        var name = string.Concat(baseName.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return char.IsAsciiLetter(name[0]) ? name : "Mod" + name;
    }
}
