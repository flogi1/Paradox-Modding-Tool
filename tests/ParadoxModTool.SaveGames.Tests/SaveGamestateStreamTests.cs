using System.IO.Compression;
using System.Text;
using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

/// <summary>
/// Komprimierte Text-Stände von Crusader Kings III: Kopfzeile und <c>meta_data</c> als Text, dahinter ein ZIP mit dem Eintrag <c>gamestate</c>, dessen
/// Offsets ab Archivbeginn zählen (deshalb liegt das ZIP in den Tests hinter einem Text-Vorspann).
/// </summary>
public class SaveGamestateStreamTests
{
    private const string Body = "variables={ data={ { flag=a data={ type=value identity=100000 } } } }\n";

    private static string WriteCompressed(string gamestate, string entryName = "gamestate")
    {
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
            writer.Write(gamestate);
        }

        var prefix = new UTF8Encoding(false).GetBytes("SAV01024d8dc5dc0000814d\nmeta_data={\n\tmeta_date=1066.9.15\n}\n");
        return SaveText.WriteBytes(prefix.Concat(zip.ToArray()).ToArray());
    }

    private static IReadOnlyList<SaveVariableEntry> Read(string path, bool compressed, IProgress<double>? progress = null) =>
        SaveVariableReader.ReadFile(path, SaveCaptureRequest.None, progress, CancellationToken.None, compressed).Entries;

    [Fact]
    public void Compressed_save_gives_the_same_entries_as_plain()
    {
        var plain = Read(SaveText.Write(Body, "SAV0100076bbdd200008171"), compressed: false);
        var compressed = Read(WriteCompressed(Body), compressed: true);

        Assert.Single(plain);
        Assert.Equal(plain, compressed);
    }

    [Fact]
    public void Gamestate_without_header_keeps_first_line() =>
        Assert.Single(Read(WriteCompressed(Body), compressed: true));

    [Fact]
    public void Gamestate_with_header_skips_it() =>
        Assert.Single(Read(WriteCompressed("SAV0100076bbdd200008171\n" + Body), compressed: true));

    [Fact]
    public void No_gamestate_entry_gives_error()
    {
        var error = Assert.Throws<InvalidDataException>(() => Read(WriteCompressed(Body, "other"), compressed: true));

        Assert.Equal(SaveExplorerTexts.CompressedUnreadable, error.Message);
    }

    [Fact]
    public void Truncated_zip_gives_error_not_exception()
    {
        var path = WriteCompressed(string.Concat(Enumerable.Repeat(Body, 2000)));
        var bytes = File.ReadAllBytes(path);
        var zipStart = IndexOf(bytes, new byte[] { 0x50, 0x4B, 0x03, 0x04 });
        File.WriteAllBytes(path, bytes[..(zipStart + 100)]);

        try
        {
            var result = SaveVariableReader.ReadFile(path, SaveCaptureRequest.None, null, CancellationToken.None, compressed: true);
            Assert.NotEqual(SaveReadStatus.Complete, result.Status);
        }
        catch (InvalidDataException error)
        {
            Assert.Equal(SaveExplorerTexts.CompressedUnreadable, error.Message);
        }
    }

    [Fact]
    public void Progress_follows_compressed_bytes()
    {
        var fractions = new List<double>();
        Read(WriteCompressed(string.Concat(Enumerable.Repeat(Body, 5000))), compressed: true, new Collect(fractions));

        Assert.NotEmpty(fractions);
        Assert.All(fractions, f => Assert.InRange(f, 0, 1));
        Assert.Equal(1, fractions[^1]);
    }

    private static int IndexOf(byte[] data, byte[] pattern)
    {
        for (var i = 0; i + pattern.Length <= data.Length; i++)
        {
            if (data.AsSpan(i, pattern.Length).SequenceEqual(pattern))
            {
                return i;
            }
        }

        return -1;
    }

    private sealed class Collect(List<double> values) : IProgress<double>
    {
        public void Report(double value) => values.Add(value);
    }
}
