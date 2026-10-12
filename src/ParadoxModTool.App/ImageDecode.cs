using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.Generators;

namespace ParadoxModTool.App;

/// <summary>Bild eines Asset-Eintrags für Vorschau und Wappen-Raster: DDS über <see cref="DdsImage"/> (DXT1/DXT5), sonst was WPF selbst lesen kann (PNG, JPG, TGA …).</summary>
internal static class ImageDecode
{
    /// <summary>Eingefrorenes Bild (auch aus einem Hintergrund-Thread nutzbar); <see langword="null"/> bei einem DDS-Format, das der Decoder nicht kennt.</summary>
    /// <exception cref="IOException">und <see cref="NotSupportedException"/>, <see cref="InvalidDataException"/>, <see cref="FileFormatException"/> bei kaputten Daten.</exception>
    public static ImageSource? FromBytes(byte[] bytes, string entry)
    {
        ImageSource? source = null;
        if (DdsImage.Decode(bytes) is { } dds)
        {
            source = BitmapSource.Create(dds.Width, dds.Height, 96, 96, PixelFormats.Bgra32, null, dds.Bgra, dds.Width * 4);
        }
        else if (!entry.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            source = image;
        }

        source?.Freeze();
        return source;
    }
}
