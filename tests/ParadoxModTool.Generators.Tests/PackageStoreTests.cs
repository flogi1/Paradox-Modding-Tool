using ParadoxModTool.Generators.Editing.Package;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Neu, Kopie und Löschen im Paket-Editor (Spec Teil 5a, Abschnitt 2).</summary>
public sealed class PackageStoreTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Shipped => _temp.PathOf("program/generator-packages");

    private string Own => _temp.PathOf("data/generator-packages");

    /// <summary>Mitgeliefertes Paket "ship" im Programmordner, sein Datenordner (Arbeitsstand, Asset) im Datenordner.</summary>
    private void ShippedPackage()
    {
        _temp.Write("program/generator-packages/ship/package.json",
            "{ \"id\": \"ship\", \"name\": \"Ship\", \"gameId\": \"eu5\", \"keyField\": \"tag\", \"head\": [ { \"id\": \"tag\", \"type\": \"text\" } ], " +
            "\"outputs\": [ { \"id\": \"o\", \"kind\": \"ownFile\", \"path\": \"x.txt\", \"template\": \"a.sbn\" } ] }");
        _temp.Write("program/generator-packages/ship/templates/a.sbn", "A");
        _temp.Write("program/generator-packages/ship/functions.sbn", "F");
        _temp.Write("data/generator-packages/ship/states/BYZ.json", "{}");
        _temp.Write("data/generator-packages/ship/assets/coa.zip", "Z");
    }

    [Fact]
    public void Create_makes_loadable_package()
    {
        var folder = PackageStore.Create(Own, "my-pack", "Mein Paket", "ck3");

        var package = GeneratorPackageLoader.Load(folder);
        Assert.Equal("my-pack", package.Id);
        Assert.Equal("Mein Paket", package.Name);
        Assert.Equal("ck3", package.GameId);
        Assert.Equal("key", package.KeyField);
        Assert.Equal(new[] { "mod", "output" }, package.Targets);
        Assert.True(Directory.Exists(Path.Combine(folder, "templates")));
    }

    [Fact]
    public void IdTaken_sees_shipped_and_own()
    {
        ShippedPackage();
        PackageStore.Create(Own, "mine", "M", "eu5");

        Assert.True(PackageStore.IdTaken("ship", Shipped, Own));
        Assert.True(PackageStore.IdTaken("mine", Shipped, Own));
        Assert.False(PackageStore.IdTaken("free", Shipped, Own));
    }

    [Fact]
    public void Copy_of_shipped_takes_states_from_data_folder()
    {
        ShippedPackage();

        var folder = PackageStore.Copy(Path.Combine(Shipped, "ship"), Path.Combine(Own, "ship"), Own, "ship2", "Kopie", withStates: true, withAssets: true);

        Assert.Equal(Path.Combine(Own, "ship2"), folder);
        Assert.Equal("A", File.ReadAllText(Path.Combine(folder, "templates", "a.sbn")));
        Assert.Equal("F", File.ReadAllText(Path.Combine(folder, "functions.sbn")));
        Assert.True(File.Exists(Path.Combine(folder, "states", "BYZ.json")));
        Assert.True(File.Exists(Path.Combine(folder, "assets", "coa.zip")));
    }

    [Fact]
    public void Copy_without_flags_has_no_states()
    {
        ShippedPackage();

        var folder = PackageStore.Copy(Path.Combine(Shipped, "ship"), Path.Combine(Own, "ship"), Own, "ship2", "Kopie", withStates: false, withAssets: false);

        Assert.False(Directory.Exists(Path.Combine(folder, "states")));
        Assert.False(Directory.Exists(Path.Combine(folder, "assets")));
    }

    [Fact]
    public void Copy_sets_new_id()
    {
        ShippedPackage();

        var folder = PackageStore.Copy(Path.Combine(Shipped, "ship"), Path.Combine(Own, "ship"), Own, "ship2", "Kopie", false, false);

        var package = GeneratorPackageLoader.Load(folder);
        Assert.Equal("ship2", package.Id);
        Assert.Equal("Kopie", package.Name);
    }

    [Fact]
    public void Copy_keeps_marker_id_of_original()
    {
        ShippedPackage();

        var first = PackageStore.Copy(Path.Combine(Shipped, "ship"), Path.Combine(Own, "ship"), Own, "ship2", "Kopie", false, false);
        var second = PackageStore.Copy(first, first, Own, "ship3", "Kopie 2", false, false);

        Assert.Equal("ship", GeneratorPackageLoader.Load(first).MarkerId);
        Assert.Equal("ship", GeneratorPackageLoader.Load(second).MarkerId);
    }

    [Fact]
    public void Contents_counts_states_and_assets()
    {
        ShippedPackage();

        Assert.Equal((1, 1), PackageStore.Contents(Path.Combine(Own, "ship")));
        Assert.Equal((0, 0), PackageStore.Contents(_temp.PathOf("nothing")));
    }

    [Theory]
    [InlineData("iv2", true)]
    [InlineData("iv2-slice", true)]
    [InlineData("a_b", true)]
    [InlineData("IV2", false)]
    [InlineData("", false)]
    [InlineData("a b", false)]
    [InlineData("../x", false)]
    [InlineData("12345678901234567890123456789012345678901", false)]
    public void IsValidPackageId_cases(string id, bool valid) => Assert.Equal(valid, PackageStore.IsValidPackageId(id));

    [Fact]
    public void Delete_calls_delegate_with_folder()
    {
        var folder = PackageStore.Create(Own, "gone", "G", "eu5");
        string? deleted = null;

        PackageStore.Delete(folder, f => deleted = f);

        Assert.Equal(folder, deleted);
    }
}
