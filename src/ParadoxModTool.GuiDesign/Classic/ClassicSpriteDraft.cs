using System.Text;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Wunsch für ein neues Sprite. <paramref name="Kind"/> ist <see cref="ClassicSpriteKind.Sprite"/> oder <see cref="ClassicSpriteKind.CorneredTile"/>.
/// <paramref name="GfxRelativePath"/> ist relativ zum Mod-Ordner (<c>interface/x.gfx</c>), <paramref name="OverwriteCopy"/> erlaubt, eine vorhandene
/// Bilddatei im Mod zu ersetzen.
/// </summary>
public sealed record ClassicSpriteRequest(
    string ImageFullPath,
    string Name,
    ClassicSpriteKind Kind,
    int Frames,
    int BorderX,
    int BorderY,
    string GfxRelativePath,
    bool OverwriteCopy);

/// <summary>
/// Was "Sprite anlegen" schreiben würde: die <c>.gfx</c>-Datei (neu oder ergänzt) und, wenn das Bild außerhalb der <c>gfx/</c>-Ordner liegt, die
/// Kopie <paramref name="CopyFrom"/> nach <paramref name="CopyTo"/>. <paramref name="TexturePath"/> steht als <c>texturefile</c> im Eintrag.
/// Mit <paramref name="Problems"/> bleibt <see cref="Files"/> leer.
/// </summary>
public sealed record ClassicSpritePlan(
    IReadOnlyList<GuiPlanFile> Files,
    string? CopyFrom,
    string? CopyTo,
    string TexturePath,
    IReadOnlyList<string> Problems)
{
    public bool CanApply => Problems.Count == 0;
}

/// <summary>
/// Plant einen neuen Sprite-Eintrag für HOI IV und EU IV (nur neue Namen, vorhandene Sprites werden nicht überschrieben). Schreibt nichts: Der Plan
/// nennt die Dateien mit altem und neuem Text und die Bildkopie, der Aufrufer schreibt.
/// </summary>
public static class ClassicSpriteDraft
{
    private const string Interface = "interface/";

    private const string Gfx = "gfx/";

    /// <summary><c>GFX_</c> plus Dateiname ohne Endung; alles außer <c>[A-Za-z0-9_]</c> wird <c>_</c>.</summary>
    public static string SuggestName(string imageFullPath)
    {
        var builder = new StringBuilder("GFX_");
        foreach (var c in Path.GetFileNameWithoutExtension(imageFullPath))
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_');
        }

        return builder.ToString();
    }

    /// <summary>Breite durch Höhe, wenn das glatt aufgeht und mehr als ein Frame ergibt (Filmstreifen), sonst 1.</summary>
    public static int SuggestFrames(int width, int height) =>
        height > 0 && width % height == 0 && width / height > 1 ? width / height : 1;

    /// <summary>
    /// <c>interface/&lt;Basis&gt;.gfx</c>; gibt es den Pfad schon in Vanilla oder einer Abhängigkeit (die Datei ersetzte sie ganz), dann
    /// <c>&lt;Basis&gt;_sprites</c>, <c>&lt;Basis&gt;_sprites_2</c> und so weiter. Eine Datei des Mods selbst zählt nicht, sie wird ergänzt.
    /// </summary>
    public static string DefaultGfxPath(string modRoot, IReadOnlyList<GuiSourceFile> gfxFiles)
    {
        var baseName = BaseNameOf(modRoot);
        var taken = new HashSet<string>(
            gfxFiles.Where(f => f.Layer != GuiLayer.Mod).Select(f => f.RelativePath.Replace('\\', '/')), StringComparer.OrdinalIgnoreCase);
        var candidate = Interface + baseName + ".gfx";
        for (var n = 1; taken.Contains(candidate); n++)
        {
            candidate = Interface + baseName + (n == 1 ? "_sprites" : "_sprites_" + n) + ".gfx";
        }

        return candidate;
    }

    /// <summary>
    /// Der <c>texturefile</c>-Pfad für ein Bild. Liegt es unter <c>&lt;Wurzel&gt;/gfx/</c> des Mods oder einer Wurzel, gilt der Pfad ab <c>gfx/</c> ohne
    /// Kopie; sonst kommt es nach <c>&lt;Mod&gt;/gfx/interface/&lt;Basis&gt;/&lt;Dateiname&gt;</c> (<c>CopyTo</c>).
    /// </summary>
    public static (string TexturePath, string? CopyTo) TexturePathFor(string imageFullPath, string modRoot, IReadOnlyList<GuiSourceRoot> roots)
    {
        var image = Path.GetFullPath(imageFullPath);
        foreach (var root in roots.Select(r => r.Directory).Prepend(modRoot))
        {
            var gfx = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar + "gfx" + Path.DirectorySeparatorChar;
            if (image.StartsWith(gfx, StringComparison.OrdinalIgnoreCase))
            {
                return (Gfx + image[gfx.Length..].Replace('\\', '/'), null);
            }
        }

        var fileName = Path.GetFileName(image);
        var texture = Gfx + "interface/" + BaseNameOf(modRoot) + "/" + fileName;
        return (texture, Path.Combine(modRoot, "gfx", "interface", BaseNameOf(modRoot), fileName));
    }

    /// <summary>
    /// Plant Eintrag und Kopie. <paramref name="readFile"/> liefert den Text einer vorhandenen Datei (voller Pfad) oder <see langword="null"/>;
    /// <paramref name="newline"/> gilt für neue Dateien, eine vorhandene behält ihr Zeilenende.
    /// </summary>
    public static ClassicSpritePlan Plan(
        ClassicSpriteRequest request,
        string modRoot,
        ClassicAssets assets,
        IReadOnlyList<GuiSourceRoot> roots,
        Func<string, string?> readFile,
        string newline)
    {
        if (request.Kind is not (ClassicSpriteKind.Sprite or ClassicSpriteKind.CorneredTile))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Nur Sprite und CorneredTile lassen sich anlegen.");
        }

        var problems = new List<string>();
        CheckName(request.Name, assets.Sprites, problems);

        var imageGiven = !string.IsNullOrWhiteSpace(request.ImageFullPath);
        if (!imageGiven || TextureLocator.ReadSizeOfFile(request.ImageFullPath) is null)
        {
            problems.Add(GuiTexts.ClassicSpriteImageUnreadable(request.ImageFullPath));
        }

        var (texturePath, copyTo) = imageGiven ? TexturePathFor(request.ImageFullPath, modRoot, roots) : (string.Empty, null);
        if (copyTo is not null && !request.OverwriteCopy && File.Exists(copyTo))
        {
            problems.Add(GuiTexts.ClassicSpriteCopyExists(copyTo));
        }

        var gfxRelative = NormalizeGfxPath(request.GfxRelativePath);
        string? gfxFull = null;
        string? existing = null;
        if (gfxRelative is null)
        {
            problems.Add(GuiTexts.ClassicSpriteGfxPathInvalid(request.GfxRelativePath));
        }
        else
        {
            gfxFull = Path.Combine(modRoot, gfxRelative.Replace('/', Path.DirectorySeparatorChar));
            existing = readFile(gfxFull);
            if (existing is null && roots.Any(r => r.Layer != GuiLayer.Mod && File.Exists(Path.Combine(r.Directory, gfxRelative.Replace('/', Path.DirectorySeparatorChar)))))
            {
                // Eine neue Mod-Datei mit dem Pfad einer Vanilla- oder Abhängigkeitsdatei ersetzte diese ganz.
                problems.Add(GuiTexts.ClassicSpriteGfxPathShadows(gfxRelative));
            }
        }

        if (problems.Count > 0 || gfxFull is null)
        {
            return new ClassicSpritePlan(Array.Empty<GuiPlanFile>(), null, null, texturePath, problems);
        }

        var entry = Entry(request, texturePath);
        var file = existing is null
            ? new GuiPlanFile(gfxFull, null, NewFileText(entry, newline))
            : new GuiPlanFile(gfxFull, existing, Insert(existing, entry));
        return new ClassicSpritePlan(new[] { file }, copyTo is null ? null : request.ImageFullPath, copyTo, texturePath, problems);
    }

    private static void CheckName(string name, ClassicSpriteIndex sprites, List<string> problems)
    {
        if (name.Length == 0 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            problems.Add(GuiTexts.ClassicSpriteNameInvalid);
            return;
        }

        if (sprites.FindAll(name) is { Count: > 0 } same)
        {
            problems.Add(GuiTexts.ClassicSpriteNameTaken(name, Where(same[^1])));
        }
        else if (sprites.Find(name) is { CaseMismatch: true, Sprite: { } other })
        {
            // Ob das Spiel Groß-/Kleinschreibung bei Sprite-Namen trennt, ist nicht gemessen: wie eine Kollision sperren.
            problems.Add(GuiTexts.ClassicSpriteNameCaseOnly(name, other.Name, Where(other)));
        }
    }

    private static string Where(ClassicSprite sprite) => sprite.File.Label + ": " + sprite.File.RelativePath;

    /// <summary>Pfad mit "/", unter <c>interface/</c>, auf <c>.gfx</c> endend, ohne ".."; sonst <see langword="null"/>.</summary>
    private static string? NormalizeGfxPath(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        var segments = normalized.Split('/');
        var valid = normalized.StartsWith(Interface, StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith(".gfx", StringComparison.OrdinalIgnoreCase)
            && normalized.Length > Interface.Length + ".gfx".Length
            && segments.All(s => s.Length > 0 && s != "." && s != "..")
            && segments[^1] != ".gfx"
            && path.IndexOfAny(Path.GetInvalidPathChars()) < 0
            && !normalized.Contains(':');
        return valid ? normalized : null;
    }

    /// <summary>Der Eintrag mit Tabs wie in Vanilla, Zeilen mit <c>\n</c> (Einrückung und Zeilenende setzt der Aufrufer).</summary>
    private static string Entry(ClassicSpriteRequest request, string texturePath)
    {
        var cornered = request.Kind == ClassicSpriteKind.CorneredTile;
        var builder = new StringBuilder();
        builder.Append(cornered ? "corneredTileSpriteType" : "spriteType").Append(" = {\n");
        builder.Append("\tname = \"").Append(request.Name).Append("\"\n");
        builder.Append("\ttexturefile = \"").Append(texturePath).Append("\"\n");
        if (cornered)
        {
            builder.Append("\tborderSize = { x = ").Append(request.BorderX).Append(" y = ").Append(request.BorderY).Append(" }\n");
        }

        if (request.Frames > 1)
        {
            builder.Append("\tnoOfFrames = ").Append(request.Frames).Append('\n');
        }

        return builder.Append('}').ToString();
    }

    private static string NewFileText(string entry, string newline) =>
        ("spriteTypes = {\n" + string.Join("\n", entry.Split('\n').Select(l => "\t" + l)) + "\n}\n").Replace("\n", newline);

    /// <summary>In den ersten obersten <c>spriteTypes</c>-Block (beliebige Schreibung) als letztes Kind, sonst ein neuer Block am Ende.</summary>
    private static string Insert(string existing, string entry)
    {
        var document = GuiParser.Parse(existing);
        var edits = new GuiTextEdits(document);
        var block = document.Root.Children.FirstOrDefault(c =>
            c.Kind == GuiNodeKind.KeyedBlock && c.HasBody && c.CloseBrace >= 0 && string.Equals(c.Name, "spriteTypes", StringComparison.OrdinalIgnoreCase));
        if (block is not null)
        {
            return TextEdit.Apply(existing, edits.InsertChild(block, entry, InsertPosition.LastChild));
        }

        var newline = edits.Newline;
        var text = new StringBuilder(existing);
        if (existing.Trim().Length > 0)
        {
            text.Append(existing.EndsWith('\n') ? string.Empty : newline).Append(newline);
        }

        return text.Append(NewFileText(entry, newline)).ToString();
    }

    private static string BaseNameOf(string modRoot) => GuiModTargets.BaseName(Path.GetFileName(modRoot.TrimEnd('\\', '/')));
}
