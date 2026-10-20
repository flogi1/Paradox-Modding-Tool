using System.Text.Json;
using ParadoxModTool.Mods;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public sealed class NewCompatchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-newcompatch-" + Guid.NewGuid().ToString("N"));

    public NewCompatchTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Eu5_metadata_has_dependencies_for_targets_with_id()
    {
        var result = NewCompatch.Create(_root, "A + B Kompatch", ".metadata/metadata.json", new[]
        {
            new NewCompatchTarget("Glorp UI", "glorp.ui"),
            new NewCompatchTarget("Ohne ID", ""),
        }, "1.3.*");

        Assert.Equal(Path.Combine(_root, "A + B Kompatch"), result.Folder);
        Assert.Equal(new[] { "Ohne ID" }, result.WithoutDependency);
        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(result.Folder, ".metadata", "metadata.json"))).RootElement;
        Assert.Equal("A + B Kompatch", json.GetProperty("name").GetString());
        Assert.Equal("1.0.0", json.GetProperty("version").GetString());
        Assert.Equal("1.3.*", json.GetProperty("supported_game_version").GetString());
        var dependency = Assert.Single(json.GetProperty("relationships").EnumerateArray());
        Assert.Equal("dependency", dependency.GetProperty("rel_type").GetString());
        Assert.Equal("glorp.ui", dependency.GetProperty("id").GetString());
        Assert.Equal("Glorp UI", dependency.GetProperty("display_name").GetString());
        Assert.Equal("mod", dependency.GetProperty("resource_type").GetString());
        Assert.Equal("*", dependency.GetProperty("version").GetString());
        Assert.Equal("A + B Kompatch", ModMetadata.Read(result.Folder, ".metadata/metadata.json")!.Name);
    }

    [Fact]
    public void Eu4_gets_descriptor_and_sibling_mod_file_without_dependencies()
    {
        var result = NewCompatch.Create(_root, "Mein Patch", ModMetadata.ClassicSiblingMetadataFile, new[] { new NewCompatchTarget("A", null) }, null);

        Assert.Contains("name=\"Mein Patch\"", File.ReadAllText(Path.Combine(result.Folder, "descriptor.mod")));
        var sibling = File.ReadAllText(Path.Combine(_root, "Mein Patch.mod"));
        Assert.Contains("path=\"mod/Mein Patch\"", sibling);
        Assert.DoesNotContain("dependencies", sibling);
        Assert.Equal("Mein Patch", ModMetadata.Read(result.Folder, ModMetadata.ClassicSiblingMetadataFile)!.Name);
    }

    [Fact]
    public void Eu4_keeps_star_or_the_version_of_mod_a()
    {
        var star = NewCompatch.Create(_root, "Ohne", ModMetadata.ClassicSiblingMetadataFile, new[] { new NewCompatchTarget("A", null) }, null, "1.37.2");
        var fromMod = NewCompatch.Create(_root, "Mit", ModMetadata.ClassicSiblingMetadataFile, new[] { new NewCompatchTarget("A", null) }, "1.37.*.*", "1.37.2");

        Assert.Contains("supported_version=\"*\"", File.ReadAllText(Path.Combine(star.Folder, "descriptor.mod")));
        Assert.Contains("supported_version=\"1.37.*.*\"", File.ReadAllText(Path.Combine(fromMod.Folder, "descriptor.mod")));
    }

    [Theory]
    [InlineData(null, "v4.5.2", "v4.5.*")]
    [InlineData("*", "v4.5.2", "v4.5.*")]
    [InlineData("v4.*.*", "v4.5.2", "v4.5.*")]
    [InlineData("v4.*", "Cygnus v4.5.2 (9776)", "v4.5.*")]
    [InlineData(null, "4.5.2", "v4.5.*")]
    [InlineData("v4.4.*", "v4.5.2", "v4.4.*")]
    [InlineData("v4.5.2", "v4.5.3", "v4.5.2")]
    [InlineData("4.5.*", null, "v4.5.*")]
    [InlineData("v4.5", null, "v4.5.*")]
    [InlineData("v4.*.*", null, "*")]
    [InlineData(null, null, "*")]
    public void Stellaris_writes_only_exact_or_major_minor_star(string? fromMod, string? installed, string expected)
    {
        // Stellaris meldet "Invalid supported_version" für alles außer v4.5.2 und v4.5.* (gemessen 2026-10-16)
        Assert.Equal(expected, NewCompatch.SupportedVersion(fromMod, installed, majorMinor: true));
    }

    [Fact]
    public void Stellaris_descriptor_gets_the_installed_major_minor()
    {
        var result = NewCompatch.Create(_root, "Stellaris Patch", ModMetadata.ClassicSiblingMetadataFile, new[] { new NewCompatchTarget("A", null) }, "v4.*.*", "v4.5.2",
            majorMinorSupportedVersion: true);

        Assert.Contains("supported_version=\"v4.5.*\"", File.ReadAllText(Path.Combine(result.Folder, "descriptor.mod")));
        Assert.Contains("supported_version=\"v4.5.*\"", File.ReadAllText(Path.Combine(_root, "Stellaris Patch.mod")));
    }

    [Fact]
    public void Other_games_keep_their_supported_version_unchanged()
    {
        Assert.Equal("*", NewCompatch.SupportedVersion(null, "v4.5.2", majorMinor: false));
        Assert.Equal("v4.*.*", NewCompatch.SupportedVersion("v4.*.*", "v4.5.2", majorMinor: false));
    }

    [Fact]
    public void Shipped_stellaris_profile_asks_for_major_minor_versions()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ParadoxModTool.slnx")))
        {
            dir = dir.Parent;
        }

        bool Flag(string file) => ParadoxModTool.GameProfiles.GameProfileLoader.LoadFromFile(Path.Combine(dir!.FullName, "profiles", file)).SupportedVersionMajorMinor;

        Assert.True(Flag("stellaris.json"));
        Assert.False(Flag("eu4.json"));
        Assert.False(Flag("ck3.json"));
        Assert.False(Flag("eu5.json"));
        Assert.False(Flag("vic3.json"));
    }

    [Fact]
    public void Invalid_name_or_existing_folder_is_rejected()
    {
        Assert.Throws<CompatchInputException>(() => NewCompatch.Create(_root, "  ", ".metadata/metadata.json", Array.Empty<NewCompatchTarget>(), null));
        Directory.CreateDirectory(Path.Combine(_root, "Da"));
        Assert.Throws<CompatchInputException>(() => NewCompatch.Create(_root, "Da", ".metadata/metadata.json", Array.Empty<NewCompatchTarget>(), null));
        Assert.Equal("A_B", NewCompatch.FolderName("A:B"));
    }
}
