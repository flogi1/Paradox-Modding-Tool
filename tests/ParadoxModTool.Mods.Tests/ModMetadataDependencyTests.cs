using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModMetadataDependencyTests : TempDirTests
{
    private const string JsonMetadataFile = ".metadata/metadata.json";

    private string JsonMod(string relationships)
    {
        var folder = Make("m", ".metadata");
        File.WriteAllText(Path.Combine(folder, "metadata.json"), "{ \"name\": \"Demo\", \"version\": \"1.0\", \"relationships\": " + relationships + " }");
        return Path.Combine(Dir, "m");
    }

    private string ClassicMod(string body)
    {
        var folder = Make("mod", "Demo");
        File.WriteAllText(Path.Combine(Dir, "mod", "Demo.mod"), "name=\"Demo\"\nversion=\"1.0\"\n" + body);
        return folder;
    }

    [Fact]
    public void Json_RelationshipsDependency()
    {
        var folder = JsonMod("""
            [ { "rel_type": "dependency", "id": "Idea_Variation_2", "display_name": "The Idea Variation 2", "resource_type": "mod", "version": "1.*" } ]
            """);

        var metadata = ModMetadata.Read(folder, JsonMetadataFile)!;

        Assert.Equal(new[] { new ModDependency("Idea_Variation_2", "The Idea Variation 2") }, metadata.Dependencies);
    }

    [Fact]
    public void Json_IgnoresOtherRelTypes()
    {
        var folder = JsonMod("""[ { "rel_type": "incompatible", "id": "x", "display_name": "X" } ]""");

        Assert.Empty(ModMetadata.Read(folder, JsonMetadataFile)!.Dependencies);
    }

    [Fact]
    public void Json_EmptyRelationships()
    {
        var folder = JsonMod("[]");

        Assert.Empty(ModMetadata.Read(folder, JsonMetadataFile)!.Dependencies);
    }

    [Fact]
    public void Classic_SingleLine()
    {
        var folder = ClassicMod("dependencies={ \"Beyond Typus\" \"Idea Variation\" }\n");

        var metadata = ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile)!;

        Assert.Equal(new[] { new ModDependency(null, "Beyond Typus"), new ModDependency(null, "Idea Variation") }, metadata.Dependencies);
    }

    [Fact]
    public void Classic_MultilineWithComments()
    {
        var folder = ClassicMod("# dependencies={ \"Commented\" }\ndependencies = {\n    \"Anbennar\" # base mod\n    \"Idea Variation\"\n}\n");

        var metadata = ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile)!;

        Assert.Equal(new[] { new ModDependency(null, "Anbennar"), new ModDependency(null, "Idea Variation") }, metadata.Dependencies);
    }

    [Fact]
    public void Classic_RemoteFileId()
    {
        var folder = ClassicMod("remote_file_id=\"2656711777\"\n");

        Assert.Equal("2656711777", ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile)!.RemoteFileId);
        Assert.Equal(string.Empty, ModMetadata.Read(ClassicMod("tags={}\n"), ModMetadata.ClassicSiblingMetadataFile)!.RemoteFileId);
    }

    [Fact]
    public void Classic_NoDependencies()
    {
        var folder = ClassicMod("tags={ \"Gameplay\" }\n");

        Assert.Empty(ModMetadata.Read(folder, ModMetadata.ClassicSiblingMetadataFile)!.Dependencies);
    }
}
