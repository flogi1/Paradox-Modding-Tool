using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>Art eines Sprite-Blocks in einer klassischen <c>.gfx</c>-Datei.</summary>
public enum ClassicSpriteKind
{
    Sprite,
    CorneredTile,
    FrameAnimated,
    ProgressBar,
    TextSprite,
    MaskedShield,
    Chart,
    Other,
}

/// <summary>
/// Ein Sprite aus einer <c>.gfx</c>-Datei. <paramref name="TexturePath"/> kommt aus <c>texturefile</c> (bei Fortschrittsbalken aus der ersten Textur),
/// <paramref name="Frames"/> aus <c>noOfFrames</c> (mindestens 1), <paramref name="BorderX"/>/<paramref name="BorderY"/> aus <c>borderSize</c>.
/// </summary>
public sealed record ClassicSprite(
    string Name,
    ClassicSpriteKind Kind,
    string? TexturePath,
    int Frames,
    int BorderX,
    int BorderY,
    bool TilingCenter,
    GuiSourceFile File,
    GuiNode Node);

/// <summary>Eine Schrift aus <c>bitmapfont</c>. <paramref name="FontPath"/> ohne Endung (<c>gfx/fonts/standard</c>).</summary>
public sealed record ClassicFont(string Name, string FontPath, GuiSourceFile File);
