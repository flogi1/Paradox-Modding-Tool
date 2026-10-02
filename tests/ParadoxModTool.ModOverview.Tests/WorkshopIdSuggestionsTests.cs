using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;
using Xunit;
using static ParadoxModTool.ModOverview.Tests.TestMods;

namespace ParadoxModTool.ModOverview.Tests;

public class WorkshopIdSuggestionsTests
{
    private static OverviewMod WithId(OverviewMod mod, string id)
    {
        var copy = new OverviewMod
        {
            GameId = mod.GameId, Mod = mod.Mod, Metadata = mod.Metadata, Links = mod.Links, WorkshopId = id,
        };
        copy.ProjectRoot = copy;
        return copy;
    }

    [Fact]
    public void MatchesRegistryOrDescriptorNameIgnoringCaseAndSpaces()
    {
        var byName = Mod("The Idea Variation 2", "eu5");
        var byDescriptor = Mod("IV2 Manpower", "eu5", descriptorName: "The Idea Variation 2  Manpower Addon");
        var published = new[]
        {
            new WorkshopMatch("111", " the idea variation 2 ", "profile"),
            new WorkshopMatch("222", "The Idea Variation 2 Manpower Addon", "profile"),
            new WorkshopMatch("333", "Something else", "profile"),
        };

        var suggestions = WorkshopIdSuggestions.Find(new[] { byName, byDescriptor }, "eu5", published);

        Assert.Equal(new[] { ("The Idea Variation 2", "111"), ("IV2 Manpower", "222") }, suggestions.Select(s => (s.Mod.Mod.Name, s.WorkshopId)));
        Assert.Equal("The Idea Variation 2 Manpower Addon", suggestions[1].Title);
    }

    [Fact]
    public void SkipsModsWithIdOtherGamesTakenIdsAndAmbiguousTitles()
    {
        var known = WithId(Mod("Known", "eu5"), "999");
        var twin = Mod("Twin", "eu5");
        var otherGame = Mod("Known", "eu4");
        var published = new[]
        {
            new WorkshopMatch("999", "Known", "profile"),
            new WorkshopMatch("500", "Twin", "profile"),
            new WorkshopMatch("501", "twin", "profile"),
        };

        Assert.Empty(WorkshopIdSuggestions.Find(new[] { known, twin, otherGame }, "eu5", published));
    }
}
