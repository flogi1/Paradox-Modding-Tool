using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Export und Import als <c>.pmtgen</c> (Spec Teil 5a, Abschnitt 4).</summary>
public sealed class GeneratorPackageArchiveTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Packages => _temp.PathOf("packages");

    /// <summary>IV2-Kopie mit Arbeitsstand, einem Mod-Asset und dem (Spiel-)Wappen-Asset.</summary>
    private string Iv2WithExtras()
    {
        var source = Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2");
        var folder = _temp.PathOf("src/iv2");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(folder, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }

        _temp.Write("src/iv2/states/BYZ.json", "{ \"key\": \"BYZ\" }");
        _temp.Write("src/iv2/assets/coa.zip", "GAME");
        _temp.Write("src/iv2/assets/icons.zip", "MOD");
        var document = PackageDocument.Load(folder);
        document.Assets = document.Assets.Append(("icons", new AssetDef("assets/icons.zip", AssetOrigin.Mod))).ToList();
        document.Save();
        return folder;
    }

    private string Export(string folder, bool withStates, params string[] assets)
    {
        var file = _temp.PathOf("out/pack" + GeneratorPackageArchive.Extension);
        GeneratorPackageArchive.Export(file, new PackageExportRequest(folder, folder, withStates, assets, "9.9.9"));
        return file;
    }

    private static IReadOnlyList<string> Entries(string file)
    {
        using var zip = ZipFile.OpenRead(file);
        return zip.Entries.Select(e => e.FullName).ToList();
    }

    private string Zip(string name, params (string Entry, byte[] Bytes)[] entries)
    {
        var file = _temp.PathOf("zips/" + name);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
        foreach (var (entry, bytes) in entries)
        {
            using var stream = zip.CreateEntry(entry).Open();
            stream.Write(bytes);
        }

        return file;
    }

    private static (string, byte[]) Text(string entry, string text) => (entry, Encoding.UTF8.GetBytes(text));

    private const string SmallPackage = "{ \"id\": \"p\", \"name\": \"P\", \"gameId\": \"eu5\", \"keyField\": \"tag\", \"head\": [ { \"id\": \"tag\", \"type\": \"text\" } ], " +
        "\"outputs\": [ { \"id\": \"o\", \"kind\": \"ownFile\", \"path\": \"x.txt\", \"template\": \"a.sbn\" } ], " +
        "\"assets\": { \"m\": { \"path\": \"assets/m.zip\", \"origin\": \"mod\" }, \"g\": { \"path\": \"assets/game.zip\", \"origin\": \"game\" } } }";

    [Fact]
    public void Round_trip_gives_same_package()
    {
        var folder = Iv2WithExtras();
        var file = Export(folder, withStates: true, "icons", "coa");

        var content = GeneratorPackageArchive.Read(file);
        Assert.Equal("iv2", content.PackageId);
        Assert.Equal("eu5", content.GameId);
        Assert.Equal(1, content.States);
        Assert.Equal(Directory.GetFiles(Path.Combine(folder, "templates")).Length, content.Templates);
        Assert.Empty(content.Skipped);
        var target = GeneratorPackageArchive.Extract(content, Packages, "iv2b", ImportMode.NewPackage, _ => true);

        Assert.Equal(Path.Combine(Packages, "iv2b"), target);
        var before = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "package.json")))!;
        var after = JsonNode.Parse(File.ReadAllText(Path.Combine(target, "package.json")))!;
        Assert.Equal("iv2b", after["id"]!.GetValue<string>());
        Assert.Equal("iv2", after["markerId"]!.GetValue<string>());
        after["id"] = "iv2";
        after.AsObject().Remove("markerId");
        Assert.True(JsonNode.DeepEquals(before, after));
        foreach (var template in Directory.GetFiles(Path.Combine(folder, "templates")))
        {
            Assert.Equal(File.ReadAllText(template), File.ReadAllText(Path.Combine(target, "templates", Path.GetFileName(template))));
        }

        Assert.Equal(File.ReadAllText(Path.Combine(folder, "functions.sbn")), File.ReadAllText(Path.Combine(target, "functions.sbn")));
        Assert.True(File.Exists(Path.Combine(target, "states", "BYZ.json")));
        Assert.Equal("MOD", File.ReadAllText(Path.Combine(target, "assets", "icons.zip")));
        Assert.False(Directory.EnumerateFileSystemEntries(Packages).Any(p => Path.GetFileName(p).StartsWith('.')));
        Assert.NotNull(GeneratorPackageLoader.Load(target));
    }

    [Fact]
    public void Game_assets_are_never_exported()
    {
        var file = Export(Iv2WithExtras(), withStates: false, "coa", "ideaDatabase");

        var entries = Entries(file);
        Assert.DoesNotContain(entries, e => e.StartsWith("assets/", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => e.StartsWith("states/", StringComparison.Ordinal));
        Assert.Contains("manifest.json", entries);
        Assert.Contains("package.json", entries);
    }

    [Fact]
    public void Import_skips_unsafe_and_undeclared_entries()
    {
        var file = Zip("bad.pmtgen",
            Text("package.json", SmallPackage),
            Text("templates/a.sbn", "A"),
            Text("assets/m.zip", "M"),
            Text("assets/undeclared.zip", "U"),
            Text("assets/game.zip", "G"),
            Text("templates/../../x.sbn", "X"),
            Text("templates/sub/x.sbn", "X"),
            Text("/abs.sbn", "X"),
            Text("C:/x.sbn", "X"),
            Text("states/../../x.sbn", "X"));

        var content = GeneratorPackageArchive.Read(file);

        Assert.Equal(new[] { "assets/undeclared.zip", "assets/game.zip", "templates/../../x.sbn", "templates/sub/x.sbn", "/abs.sbn", "C:/x.sbn", "states/../../x.sbn" }, content.Skipped);
        var target = GeneratorPackageArchive.Extract(content, Packages, null, ImportMode.NewPackage, _ => true);
        Assert.Equal("M", File.ReadAllText(Path.Combine(target, "assets", "m.zip")));
        Assert.Empty(Directory.GetFiles(_temp.Root, "x.sbn", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(target, "assets", "game.zip")));
    }

    [Fact]
    public void Import_counts_bytes_while_reading()
    {
        var file = Zip("big.pmtgen", Text("package.json", SmallPackage), ("templates/big.sbn", new byte[6 * 1024 * 1024]));

        var error = Assert.Throws<GeneratorPackageException>(() => GeneratorPackageArchive.Read(file));
        Assert.Contains("templates/big.sbn", error.Message);
    }

    [Fact]
    public void Replace_keeps_other_states_and_asks_for_same_names()
    {
        _temp.Write("packages/p/package.json", SmallPackage.Replace("\"name\": \"P\"", "\"name\": \"Alt\""));
        _temp.Write("packages/p/templates/old.sbn", "OLD");
        _temp.Write("packages/p/states/A.json", "old");
        _temp.Write("packages/p/states/B.json", "keep");
        var file = Zip("p.pmtgen",
            Text("package.json", SmallPackage),
            Text("templates/a.sbn", "A"),
            Text("states/A.json", "new"),
            Text("states/C.json", "added"));
        IReadOnlyList<string>? asked = null;

        var target = GeneratorPackageArchive.Extract(GeneratorPackageArchive.Read(file), Packages, null, ImportMode.Replace, names =>
        {
            asked = names;
            return true;
        });

        Assert.Equal(new[] { "A.json" }, asked);
        Assert.Equal("new", File.ReadAllText(Path.Combine(target, "states", "A.json")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "states", "B.json")));
        Assert.Equal("added", File.ReadAllText(Path.Combine(target, "states", "C.json")));
        Assert.False(File.Exists(Path.Combine(target, "templates", "old.sbn")));
        Assert.Equal("P", GeneratorPackageLoader.Load(target).Name);
    }

    [Fact]
    public void Replace_declined_keeps_old_states()
    {
        _temp.Write("packages/p/package.json", SmallPackage);
        _temp.Write("packages/p/states/A.json", "old");
        var file = Zip("p.pmtgen", Text("package.json", SmallPackage), Text("templates/a.sbn", "A"), Text("states/A.json", "new"));

        var target = GeneratorPackageArchive.Extract(GeneratorPackageArchive.Read(file), Packages, null, ImportMode.Replace, _ => false);

        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "states", "A.json")));
    }

    [Fact]
    public void Read_without_manifest_uses_package_json()
    {
        var content = GeneratorPackageArchive.Read(Zip("plain.pmtgen", Text("package.json", SmallPackage), Text("templates/a.sbn", "A")));

        Assert.Equal("p", content.PackageId);
        Assert.Equal("P", content.Name);
        Assert.Equal(1, content.Templates);
        Assert.Throws<GeneratorPackageException>(() => GeneratorPackageArchive.Read(Zip("empty.pmtgen", Text("readme.txt", "x"))));
    }

    [Fact]
    public void Not_a_zip_is_a_package_exception()
    {
        var file = _temp.Write("broken.pmtgen", "no zip");

        Assert.Throws<GeneratorPackageException>(() => GeneratorPackageArchive.Read(file));
    }
}
