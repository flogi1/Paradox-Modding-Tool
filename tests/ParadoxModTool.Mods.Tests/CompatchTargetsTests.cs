using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class CompatchTargetsTests : TempDirTests
{
    private ModRegistry NewRegistry() => ModRegistry.Load(Make("game"));

    private ModInfo AddMod(ModRegistry registry, string name) => registry.Add(name, Make(name));

    [Fact]
    public void WorkshopIdOf_ErkenntNurWorkshopOrdner()
    {
        Assert.Equal("3599735023", CompatchTargets.WorkshopIdOf(@"D:\SteamLibrary\steamapps\workshop\content\3450310\3599735023"));
        Assert.Equal("3599735023", CompatchTargets.WorkshopIdOf(@"D:\SteamLibrary\steamapps\workshop\content\3450310\3599735023\"));
        Assert.Null(CompatchTargets.WorkshopIdOf(@"C:\Users\x\Documents\Paradox Interactive\Europa Universalis V\mod\3599735023"));
        Assert.Null(CompatchTargets.WorkshopIdOf(@"D:\SteamLibrary\steamapps\workshop\content\3450310\abc"));
        Assert.True(CompatchTargets.IsWorkshopFolder(@"D:\s\steamapps\workshop\content\1\2"));
    }

    [Fact]
    public void Matches_PrueftJeNachArt()
    {
        var folder = Make("Fremd");
        Assert.True(CompatchTargets.Matches(new CompatchTarget { ModId = "abc" }, "abc", null));
        Assert.False(CompatchTargets.Matches(new CompatchTarget { ModId = "abc" }, "xyz", folder));
        Assert.True(CompatchTargets.Matches(new CompatchTarget { Folder = folder }, null, folder.ToUpperInvariant() + "\\"));
        Assert.True(CompatchTargets.Matches(new CompatchTarget { WorkshopId = "42" }, null, @"D:\s\steamapps\workshop\content\1\42"));
        Assert.False(CompatchTargets.Matches(new CompatchTarget { WorkshopId = "42" }, null, @"D:\s\mod\42"));
    }

    [Fact]
    public void Kompatch_UndZiel_SindAddon_UndRollentextNenntDieZiele()
    {
        var registry = NewRegistry();
        var a = AddMod(registry, "A");
        var other = AddMod(registry, "Andere");
        var patch = AddMod(registry, "Patch");
        registry.SetCompatch(patch.Id, new[]
        {
            new CompatchTarget { ModId = a.Id, DisplayName = "A" },
            new CompatchTarget { WorkshopId = "42", DisplayName = "B" },
        });

        Assert.Equal(ModRelationKind.Addon, ModProjects.Relation(registry, patch, a));
        Assert.Equal(ModRelationKind.Addon, ModProjects.Relation(registry, a, patch));
        Assert.Equal(ModRelationKind.Unrelated, ModProjects.Relation(registry, patch, other));
        Assert.Equal("Kompatch für A + B", ModProjects.RoleText(registry, patch));
        Assert.Equal(new[] { patch.Id }, CompatchTargets.CompatchesFor(registry, a).Select(m => m.Id));
        Assert.Empty(CompatchTargets.CompatchesFor(registry, other));
    }

    [Fact]
    public void CompatchesFor_FindetOrdnerziele_UeberAlleOrdnerDerMod()
    {
        var registry = NewRegistry();
        var a = AddMod(registry, "A");
        var copy = Make("A-Kopie");
        registry.AddPath(a.Id, copy);
        var patch = AddMod(registry, "Patch");
        registry.SetCompatch(patch.Id, new[] { new CompatchTarget { Folder = copy, DisplayName = "A" } });

        Assert.Single(CompatchTargets.CompatchesFor(registry, a));
        Assert.True(CompatchTargets.Covers(registry, patch, null, copy));
    }

    [Fact]
    public void Resolve_LiefertOrdnerOderNull()
    {
        var registry = NewRegistry();
        var a = AddMod(registry, "A");
        var gone = Path.Combine(Dir, "weg");
        var workshop = Make("W");

        Assert.Equal(a.Paths[0], CompatchTargets.Resolve(registry, new CompatchTarget { ModId = a.Id }, null));
        Assert.Null(CompatchTargets.Resolve(registry, new CompatchTarget { Folder = gone }, null));
        Assert.Equal(workshop, CompatchTargets.Resolve(registry, new CompatchTarget { WorkshopId = "7" }, id => id == "7" ? workshop : null));
        Assert.Null(CompatchTargets.Resolve(registry, new CompatchTarget { WorkshopId = "7" }, null));
    }
}
