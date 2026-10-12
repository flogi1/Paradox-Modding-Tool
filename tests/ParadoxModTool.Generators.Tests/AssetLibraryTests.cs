using System.IO.Compression;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Asset-Bibliotheken: Zip (wie coa_library.zip) oder Ordner; Einträge listen und lesen.</summary>
public sealed class AssetLibraryTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    internal static string Zip(TempFolder temp, string name, params (string Entry, byte[] Bytes)[] entries)
    {
        var path = temp.PathOf(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        zip.CreateEntry("coa/");
        foreach (var (entry, bytes) in entries)
        {
            using var stream = zip.CreateEntry(entry).Open();
            stream.Write(bytes);
        }

        return path;
    }

    [Fact]
    public void Zip_entries_are_listed_and_read()
    {
        var path = Zip(_temp, "coa.zip", ("coa/FRA.dds", new byte[] { 1 }), ("coa/BYZ.dds", new byte[] { 2, 3 }));

        var library = AssetLibrary.Open(path);

        Assert.Equal(new[] { "coa/BYZ.dds", "coa/FRA.dds" }, library.Entries);
        Assert.Equal(new byte[] { 2, 3 }, library.Read("coa/BYZ.dds"));
    }

    [Fact]
    public void Folder_entries_are_listed_and_read()
    {
        File.WriteAllBytes(_temp.Write("lib/sub/a.dds", string.Empty), new byte[] { 7 });

        var library = AssetLibrary.Open(_temp.PathOf("lib"));

        Assert.Equal(new[] { "sub/a.dds" }, library.Entries);
        Assert.Equal(new byte[] { 7 }, library.Read("sub/a.dds"));
    }

    [Fact]
    public void Unknown_entry_is_an_error() =>
        Assert.Throws<GeneratorOutputException>(() => AssetLibrary.Open(Zip(_temp, "coa.zip", ("coa/FRA.dds", new byte[] { 1 }))).Read("coa/XXX.dds"));
}
