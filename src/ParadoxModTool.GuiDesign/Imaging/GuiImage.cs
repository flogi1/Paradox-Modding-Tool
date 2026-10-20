namespace ParadoxModTool.GuiDesign.Imaging;

/// <summary>Dekodiertes Bild für die Vorschau: BGRA, 4 Byte je Pixel, gerade Alpha (nicht vormultipliziert), Zeilen von oben nach unten.</summary>
public sealed record GuiImage(int Width, int Height, byte[] Bgra);
