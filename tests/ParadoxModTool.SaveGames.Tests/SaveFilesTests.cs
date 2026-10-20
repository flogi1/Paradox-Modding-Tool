using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.SaveGames.Tests;

public class SaveFilesTests
{
    private static readonly SaveTextMarker Eu5Marker = new() { Prefix = "SAV", Offset = 5, Value = "00" };

    [Fact]
    public void TextSaveIsReadableAndMetadataIsRead()
    {
        var path = SaveText.Write(
            "metadata={\n\tdate=1363.11.17\n\tplaythrough_id=\"abc-1\"\n\tplaythrough_name=\"Castile #6d\"\n\tversion=\"1.3.99\"\n" +
            "\tflag=\"CAS={\n\tdate=\\\"x\\\"\n}\"\n}\n");

        var info = SaveFiles.Inspect(path, Eu5Marker);

        Assert.True(info.Readable);
        Assert.Equal(("1363.11.17", "abc-1", "Castile #6d", "1.3.99"), (info.GameDate, info.PlaythroughId, info.PlaythroughName, info.Version));
        Assert.Equal(new FileInfo(path).Length, info.Length);
    }

    private static readonly SaveTextMarker Hoi4Marker = new() { Prefix = "HOI4", Offset = 4, Value = "txt" };

    [Fact]
    public void Hoi4_header_without_metadata_block_reads_date_and_version()
    {
        var path = SaveText.Write(
            "player=\"GER\"\r\nideology=fascism\r\ndate=\"1936.1.1.12\"\r\nversion=\"Operation Postern v1.19.3.0.c01a (5632)\"\r\n" +
            "player_countries={\r\n\tGER={\r\n\t\tdate=\"1999.1.1\"\r\n\t}\r\n}\r\n",
            "HOI4txt\r");

        var info = SaveFiles.Inspect(path, Hoi4Marker, string.Empty, "date");

        Assert.True(info.Readable);
        Assert.False(info.Compressed);
        Assert.Equal("1936.1.1.12", info.GameDate);
        Assert.Equal("Operation Postern v1.19.3.0.c01a (5632)", info.Version);
    }

    [Fact]
    public void Hoi4_binary_save_is_not_readable()
    {
        var path = SaveText.Write("binary", "HOI4bin");

        Assert.False(SaveFiles.Inspect(path, Hoi4Marker, string.Empty, "date").Readable);
    }

    private static readonly SaveTextMarker Ck3Marker = new() { Prefix = "SAV", Offset = 5, Value = "00", CompressedValue = "02" };

    [Fact]
    public void Ck3_compressed_save_is_readable_and_compressed()
    {
        var path = SaveText.Write("meta_data={\n\tversion=\"1.19.0.6\"\n\tmeta_date=1066.9.15\n}\nPK\u0003\u0004", "SAV01024d8dc5dc0000814d");

        var info = SaveFiles.Inspect(path, Ck3Marker, "meta_data", "meta_date");

        Assert.True(info.Readable);
        Assert.True(info.Compressed);
        Assert.Equal("1066.9.15", info.GameDate);
        Assert.Equal("1.19.0.6", info.Version);
        Assert.Null(info.PlaythroughId);
    }

    [Fact]
    public void Ck3_plain_save_is_readable_not_compressed()
    {
        var info = SaveFiles.Inspect(SaveText.Write("meta_data={\n\tmeta_date=1066.9.15\n}\n", "SAV0100076bbdd200008171"), Ck3Marker, "meta_data", "meta_date");

        Assert.True(info.Readable);
        Assert.False(info.Compressed);
    }

    [Fact]
    public void Eu5_marker_keeps_compressed_unreadable()
    {
        var info = SaveFiles.Inspect(SaveText.Write("PK\u0003\u0004", SaveText.CompressedHeader), Eu5Marker);

        Assert.False(info.Readable);
        Assert.False(info.Compressed);
    }

    [Fact]
    public void CompressedSaveIsNotReadable() =>
        Assert.False(SaveFiles.Inspect(SaveText.Write("PK\u0003\u0004", SaveText.CompressedHeader), Eu5Marker).Readable);

    [Fact]
    public void ForeignOrEmptyFileIsNotReadable()
    {
        Assert.False(SaveFiles.Inspect(SaveText.WriteBytes(Array.Empty<byte>()), Eu5Marker).Readable);
        Assert.False(SaveFiles.Inspect(SaveText.WriteBytes("hello"u8.ToArray()), Eu5Marker).Readable);
    }

    [Fact]
    public void ListReturnsSavesWithTheExtensionNewestFirst()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pmt-saves-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var older = Path.Combine(folder, "a.eu5");
        var newer = Path.Combine(folder, "b.eu5");
        File.WriteAllText(older, SaveText.TextHeader + "\nmetadata={ date=1337.4.1 }\n");
        File.WriteAllText(newer, SaveText.TextHeader + "\nmetadata={ date=1338.4.1 }\n");
        File.WriteAllText(Path.Combine(folder, "c.txt"), "x");
        File.SetLastWriteTimeUtc(older, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newer, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));

        var saves = SaveFiles.List(folder, new SaveGameSettings { Extension = ".eu5", TextMarker = Eu5Marker });

        Assert.Equal(new[] { "b.eu5", "a.eu5" }, saves.Select(s => Path.GetFileName(s.Path)));
        Assert.Equal("1338.4.1", saves[0].GameDate);
    }

    [Fact]
    public void DefaultComparisonPrefersTheSamePlaythroughAndNeverTheCurrentSave()
    {
        static SaveFileInfo Save(string name, int day, string playthrough, bool readable = true) =>
            new($@"C:\saves\{name}.eu5", new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Utc), 1, readable, null, playthrough, null, null);

        var c = Save("c", 5, "A");
        var x = Save("x", 4, "B");
        var z = Save("z", 3, "A", readable: false);
        var b = Save("b", 2, "A");
        var a = Save("a", 1, "A");
        var saves = new[] { c, x, z, b, a };

        Assert.Equal(b, SaveFiles.DefaultComparison(saves, c));
        Assert.Equal(b, SaveFiles.DefaultComparison(saves, x));
        Assert.Null(SaveFiles.DefaultComparison(saves, a));
        Assert.Equal(b, SaveFiles.DefaultComparison(saves, c with { Path = @"C:\SAVES\C.EU5" }));
    }

    [Fact]
    public void ListOfMissingFolderIsEmpty() =>
        Assert.Empty(SaveFiles.List(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), new SaveGameSettings { Extension = ".eu5" }));
}
