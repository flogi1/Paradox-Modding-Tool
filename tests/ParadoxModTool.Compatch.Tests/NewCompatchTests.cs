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
    public void Invalid_name_or_existing_folder_is_rejected()
    {
        Assert.Throws<CompatchInputException>(() => NewCompatch.Create(_root, "  ", ".metadata/metadata.json", Array.Empty<NewCompatchTarget>(), null));
        Directory.CreateDirectory(Path.Combine(_root, "Da"));
        Assert.Throws<CompatchInputException>(() => NewCompatch.Create(_root, "Da", ".metadata/metadata.json", Array.Empty<NewCompatchTarget>(), null));
        Assert.Equal("A_B", NewCompatch.FolderName("A:B"));
    }
}
