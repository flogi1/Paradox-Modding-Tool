using ParadoxModTool.Mods;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class CompatMembersTests : TempDirTests
{
    [Fact]
    public void FromFolder_Workshop_KlassischesFormat_LiestDescriptorModImOrdner()
    {
        var folder = Make("123456789");
        Write("123456789/descriptor.mod", "name = \"The Idea Variation 2\"\n");

        var member = CompatMembers.FromFolder(folder, CompatOrigin.Workshop, null, ModMetadata.ClassicSiblingMetadataFile);

        Assert.Equal("The Idea Variation 2", member.Name);
    }

    [Fact]
    public void FromFolder_Workshop_KlassischesFormat_OhneDescriptor_FaelltAufOrdnernameZurueck()
    {
        var folder = Make("123456789");

        var member = CompatMembers.FromFolder(folder, CompatOrigin.Workshop, null, ModMetadata.ClassicSiblingMetadataFile);

        Assert.Equal("123456789", member.Name);
    }

    [Fact]
    public void FromFolder_Register_KlassischesFormat_LiestWeiterhinDieGeschwisterdatei()
    {
        var folder = Make("m");
        Write("m.mod", "name = \"Lokale Mod\"\n");

        var member = CompatMembers.FromFolder(folder, CompatOrigin.Register, "id-1", ModMetadata.ClassicSiblingMetadataFile);

        Assert.Equal("Lokale Mod", member.Name);
    }

    [Fact]
    public void FromFolder_Workshop_JsonFormat_LiestWeiterhinAusDemOrdnerSelbst()
    {
        var folder = Make("123456789", ".metadata");
        File.WriteAllText(Path.Combine(folder, "metadata.json"), "{ \"name\": \"CK3-Mod\" }");
        var modFolder = Make("123456789");

        var member = CompatMembers.FromFolder(modFolder, CompatOrigin.Workshop, null, ".metadata/metadata.json");

        Assert.Equal("CK3-Mod", member.Name);
    }
}
