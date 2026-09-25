using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class SteamDataTests : TempDirTests
{
    [Fact]
    public void ModMetadata_LiestKurzbeschreibungUndBild()
    {
        var folder = Make("mod", ".metadata");
        File.WriteAllText(Path.Combine(folder, "metadata.json"), "{ \"name\": \"X\", \"short_description\": \"Kurz\", \"picture\": \"bild.png\" }");

        var metadata = ModMetadata.Read(Path.Combine(Dir, "mod"), ".metadata/metadata.json");

        Assert.Equal("Kurz", metadata!.ShortDescription);
        Assert.Equal("bild.png", metadata.Picture);
    }

    [Fact]
    public void ModMetadata_LiestDieModIdDesSpiels()
    {
        var folder = Make("mod", ".metadata");
        File.WriteAllText(Path.Combine(folder, "metadata.json"), "{ \"name\": \"X\", \"id\": \"Idea_Variation_2\" }");

        Assert.Equal("Idea_Variation_2", ModMetadata.Read(Path.Combine(Dir, "mod"), ".metadata/metadata.json")!.Id);
        Assert.Equal("", ModMetadata.Read(Path.Combine(Dir, "mod"), ".metadata/metadata.json")!.ShortDescription);
    }

    [Fact]
    public void ModMetadata_FehlendeNeueFelder_SindLeer()
    {
        var folder = Make("mod", ".metadata");
        File.WriteAllText(Path.Combine(folder, "metadata.json"), "{ \"name\": \"X\" }");

        var metadata = ModMetadata.Read(Path.Combine(Dir, "mod"), ".metadata/metadata.json");

        Assert.Equal("", metadata!.ShortDescription);
        Assert.Equal("", metadata.Picture);
    }

    [Fact]
    public void SetSteamWorkshopId_Speichert_Entfernt_UndLehntNichtZiffernAb()
    {
        var registry = ModRegistry.Load(Dir);
        var mod = registry.Add("A", Make("a"));

        registry.SetSteamWorkshopId(mod.Id, " 3456789012 ");
        Assert.Equal("3456789012", ModRegistry.Load(Dir).Get(mod.Id)!.SteamWorkshopId);

        registry.SetSteamWorkshopId(mod.Id, "0");
        Assert.Null(ModRegistry.Load(Dir).Get(mod.Id)!.SteamWorkshopId);

        registry.SetSteamWorkshopId(mod.Id, "12");
        registry.SetSteamWorkshopId(mod.Id, null);
        Assert.Null(ModRegistry.Load(Dir).Get(mod.Id)!.SteamWorkshopId);

        Assert.Throws<ArgumentException>(() => registry.SetSteamWorkshopId(mod.Id, "12ab"));
        Assert.Throws<InvalidOperationException>(() => registry.SetSteamWorkshopId("gibtsnicht", "1"));
    }

    [Fact]
    public void ReleaseLog_SetSteam_SpeichertUndLaedt_UndAlteDateiOhneBlockLaedtUnveraendert()
    {
        var folder = Path.Combine(Dir, "mod");
        var log = ReleaseLog.Load(folder);
        log.Add("2.0.5", "2026-09-20");
        log.Add("2.0.4", "");

        log.SetSteam("2.0.5", new SteamPublication { PublishedAt = "2026-09-20T14:03:00", WorkshopId = "3456789012" });

        var reloaded = ReleaseLog.Load(folder);
        Assert.Equal("3456789012", reloaded.Find("2.0.5")!.Steam!.WorkshopId);
        Assert.Equal("2026-09-20T14:03:00", reloaded.Find("2.0.5")!.Steam!.PublishedAt);
        Assert.Null(reloaded.Find("2.0.4")!.Steam);
        Assert.DoesNotContain("\"steam\": null", File.ReadAllText(Path.Combine(folder, ReleaseLog.FileName)));
        Assert.Throws<InvalidOperationException>(() => reloaded.SetSteam("9.9", new SteamPublication()));
    }

    [Fact]
    public void ReleaseLog_LaedtEineDateiOhneSteamBlock()
    {
        var folder = Make("mod");
        File.WriteAllText(Path.Combine(folder, ReleaseLog.FileName), "{\"version\":1,\"releases\":[{\"version\":\"1.0\",\"date\":\"2026-01-01\"}]}");

        var entry = Assert.Single(ReleaseLog.Load(folder).Releases);

        Assert.Equal("1.0", entry.Version);
        Assert.Null(entry.Steam);
    }
}
