using ParadoxModTool.GuiDesign.Imaging;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Findet Texturpfade klassischer Spiele (<c>gfx//interface//x.tga</c>) in den Wurzeln des Spiels. Fehlt eine <c>.tga</c>, gilt die gleichnamige
/// <c>.dds</c>. Suche, Merkliste und Köpfe siehe <see cref="TextureLocator"/>.
/// </summary>
public sealed class ClassicTextureLocator : TextureLocator
{
    /// <param name="rootsInSearchOrder">Wurzeln in Suchreihenfolge: Mod, Abhängigkeits-Mods, dann Vanilla rückwärts (DLC-Ordner vor der Installation).</param>
    public ClassicTextureLocator(IReadOnlyList<string> rootsInSearchOrder)
        : base(rootsInSearchOrder, tgaFallsBackToDds: true)
    {
    }
}
