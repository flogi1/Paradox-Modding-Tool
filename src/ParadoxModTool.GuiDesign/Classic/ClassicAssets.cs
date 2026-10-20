using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>Was die klassische Vorschau außer den <c>.gui</c>-Dateien braucht: Sprites und Schriften aus <c>.gfx</c>, Texturen, Bitmap-Schriften. Unveränderlich; spätere Aufgaben ergänzen weitere Teile.</summary>
public sealed class ClassicAssets
{
    private ClassicAssets(ClassicSpriteIndex sprites, ClassicTextureLocator textures, ClassicLocalisation localisation)
    {
        Sprites = sprites;
        Textures = textures;
        Localisation = localisation;
        Fonts = new ClassicFontLibrary(sprites, textures);
    }

    public ClassicSpriteIndex Sprites { get; }

    public ClassicTextureLocator Textures { get; }

    /// <summary>Bitmap-Schriften nach Name (aus den <c>.gfx</c>-Dateien dieses Stands).</summary>
    public ClassicFontLibrary Fonts { get; }

    /// <summary>Englische Texte (<c>localisation/*_l_english.yml</c>), träge geladen beim ersten Zugriff.</summary>
    public ClassicLocalisation Localisation { get; }

    /// <summary>
    /// <paramref name="roots"/> in Ladereihenfolge (Vanilla, Abhängigkeiten, Mod, wie <see cref="GuiSourceSet.ClassicRootsFor"/>); die Textursuche läuft
    /// umgekehrt: Mod, Abhängigkeiten, dann Vanilla rückwärts (DLC-Ordner vor der Installation). <paramref name="parse"/> ersetzt das Lesen von der Platte
    /// (Standard: <see cref="GuiTextReader"/>), <paramref name="previous"/> spart das erneute Lesen unveränderter Dateien.
    /// </summary>
    public static ClassicAssets Build(
        IReadOnlyList<GuiSourceRoot> roots,
        IReadOnlyList<GuiSourceFile> gfxFiles,
        bool spriteFirstWins,
        bool caseSensitive,
        Func<GuiSourceFile, GuiDocument>? parse = null,
        ClassicAssets? previous = null)
    {
        var sprites = ClassicSpriteIndex.Build(
            gfxFiles, parse ?? ParseFromDisk, spriteFirstWins, caseSensitive, previous?.Sprites);
        var search = roots.Where(r => r.Layer == GuiLayer.Mod)
            .Concat(roots.Where(r => r.Layer == GuiLayer.Dependency))
            .Concat(roots.Where(r => r.Layer == GuiLayer.Vanilla).Reverse())
            .Select(r => r.Directory)
            .ToList();
        return new ClassicAssets(sprites, new ClassicTextureLocator(search), ClassicLocalisation.Load(roots));
    }

    /// <summary>Neuer Stand, in dem die gespeicherte <c>.gfx</c>-Datei durch ihren neuen Text ersetzt ist. Texturen (samt gemerkten Größen) und Texte bleiben, denn eine <c>.gfx</c>-Datei ändert keine yml; fehlende oder geänderte Texturen sieht ohnehin der nächste Vorschaulauf (<see cref="ClassicTextureLocator.NewRun"/>).</summary>
    public ClassicAssets WithGfxFile(GuiSourceFile file, string text) =>
        new(Sprites.WithFile(file, GuiParser.Parse(text)), Textures, Localisation);

    private static GuiDocument ParseFromDisk(GuiSourceFile file) => GuiParser.Parse(GuiTextReader.Read(file.FullPath).Text);
}
