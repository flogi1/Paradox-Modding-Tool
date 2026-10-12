using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModDetectorTests : TempDirTests
{
    private const string Meta = ".metadata/metadata.json";

    private ModRegistry NewRegistry() => ModRegistry.Load(Path.Combine(Dir, "game"));

    /// <summary>Ein Ordner mit Metadaten-Datei, wie ihn die Spiele für Mods erwarten.</summary>
    private string ModFolder(string name)
    {
        var folder = Make("mods", name);
        Directory.CreateDirectory(Path.Combine(folder, ".metadata"));
        File.WriteAllText(Path.Combine(folder, ".metadata", "metadata.json"), "{ \"name\": \"" + name + "\" }");
        return folder;
    }

    private static string TouchFile(string folder, params string[] parts)
    {
        var directory = Path.Combine(new[] { folder }.Concat(parts.Take(parts.Length - 1)).ToArray());
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, parts[^1]);
        File.WriteAllText(file, "x = 1");
        return file;
    }

    [Fact]
    public void An_open_file_in_a_registered_mod_is_known()
    {
        var registry = NewRegistry();
        var folder = ModFolder("a");
        var mod = registry.Add("A", folder);
        var file = TouchFile(folder, "common", "ideas", "x.txt");

        var result = ModDetector.Detect(registry, new[] { file }, Meta);

        Assert.Equal(ModDetectionKind.Known, result.Kind);
        Assert.Same(mod, result.Mod);
    }

    [Fact]
    public void A_later_candidate_is_used_when_the_first_is_missing_empty_or_outside_any_mod()
    {
        var registry = NewRegistry();
        var folder = ModFolder("a");
        var mod = registry.Add("A", folder);
        var elsewhere = TouchFile(Make("sonst"), "y.txt");

        var result = ModDetector.Detect(registry, new string?[] { null, "", Path.Combine(Dir, "gibt-es-nicht"), elsewhere, folder }, Meta);

        Assert.Same(mod, result.Mod);
    }

    [Fact]
    public void The_first_matching_candidate_wins()
    {
        var registry = NewRegistry();
        var folderA = ModFolder("a");
        var folderB = ModFolder("b");
        var a = registry.Add("A", folderA);
        registry.Add("B", folderB);

        var result = ModDetector.Detect(registry, new[] { folderA, folderB }, Meta);

        Assert.Same(a, result.Mod);
    }

    [Fact]
    public void An_unregistered_mod_folder_is_reported_from_a_deep_file()
    {
        var registry = NewRegistry();
        var folder = ModFolder("neu");
        var file = TouchFile(folder, "common", "ideas", "x.txt");

        var result = ModDetector.Detect(registry, new[] { file }, Meta);

        Assert.Equal(ModDetectionKind.Unknown, result.Kind);
        Assert.Equal(folder, result.Folder);
        Assert.Null(result.Mod);
    }

    [Fact]
    public void An_unregistered_folder_at_a_higher_rank_beats_a_known_mod_at_a_lower_rank()
    {
        var registry = NewRegistry();
        var known = ModFolder("bekannt");
        registry.Add("Bekannt", known);
        var unknown = ModFolder("neu");

        var result = ModDetector.Detect(registry, new[] { TouchFile(unknown, "x.txt"), known }, Meta);

        Assert.Equal(ModDetectionKind.Unknown, result.Kind);
        Assert.Equal(unknown, result.Folder);
    }

    [Fact]
    public void A_file_in_an_ignored_mod_folder_does_not_fall_back_to_another_mod()
    {
        var registry = NewRegistry();
        var ignored = ModFolder("alt");
        registry.Ignore(ignored);
        var known = ModFolder("bekannt");
        registry.Add("Bekannt", known);

        var result = ModDetector.Detect(registry, new[] { TouchFile(ignored, "x.txt"), known }, Meta);

        Assert.Equal(ModDetectionKind.Ignored, result.Kind);
        Assert.Equal(ignored, result.Folder);
        Assert.Null(result.Mod);
    }

    [Fact]
    public void A_subscribed_workshop_mod_is_not_offered_and_does_not_fall_back_to_another_mod()
    {
        var registry = NewRegistry();
        var workshop = Make("steamapps", "workshop", "content", "236850", "1703319446");
        File.WriteAllText(Path.Combine(workshop, "descriptor.mod"), "name=\"Abo\"");
        var known = ModFolder("bekannt");
        registry.Add("Bekannt", known);

        var result = ModDetector.Detect(registry, new[] { TouchFile(workshop, "common", "ideas", "x.txt"), known }, ModMetadata.ClassicSiblingMetadataFile);

        Assert.Equal(ModDetectionKind.Ignored, result.Kind);
        Assert.Equal(workshop, result.Folder);
        Assert.False(registry.IsIgnored(workshop));
    }

    [Fact]
    public void A_registered_workshop_mod_is_still_known()
    {
        var registry = NewRegistry();
        var workshop = Make("steamapps", "workshop", "content", "3450310", "3400000001");
        Directory.CreateDirectory(Path.Combine(workshop, ".metadata"));
        File.WriteAllText(Path.Combine(workshop, ".metadata", "metadata.json"), "{ \"name\": \"Abo\" }");
        var mod = registry.Add("Abo", workshop);

        var result = ModDetector.Detect(registry, new[] { TouchFile(workshop, "x.txt") }, Meta);

        Assert.Same(mod, result.Mod);
    }

    [Fact]
    public void A_file_outside_any_mod_folder_still_falls_back_to_a_lower_candidate()
    {
        var registry = NewRegistry();
        var known = ModFolder("bekannt");
        var mod = registry.Add("Bekannt", known);
        var loose = TouchFile(Make("lose-dateien"), "x.txt");

        var result = ModDetector.Detect(registry, new[] { loose, known }, Meta);

        Assert.Same(mod, result.Mod);
    }

    [Fact]
    public void Detect_skips_excluded_roots()
    {
        var registry = NewRegistry();
        var vanilla = ModFolder("vanilla-game");
        var file = TouchFile(vanilla, "in_game", "common", "x.txt");

        var result = ModDetector.Detect(registry, new[] { file }, Meta, new[] { vanilla });

        Assert.Equal(ModDetectionKind.None, result.Kind);
    }

    [Fact]
    public void Detect_skips_missing_and_invalid_candidates()
    {
        var registry = NewRegistry();

        var result = ModDetector.Detect(registry, new string?[] { "a\0b", "   ", Path.Combine(Dir, "nichts") }, Meta);

        Assert.Equal(ModDetectionKind.None, result.Kind);
    }

    [Fact]
    public void Without_a_metadata_file_nothing_is_reported()
    {
        var registry = NewRegistry();
        var folder = Make("mods", "ohne-metadaten");
        var file = TouchFile(folder, "x.txt");

        Assert.Equal(ModDetectionKind.None, ModDetector.Detect(registry, new[] { file }, Meta).Kind);
        Assert.Equal(ModDetectionKind.None, ModDetector.Detect(registry, new[] { file }, string.Empty).Kind);
    }

    [Fact]
    public void Classic_format_finds_the_folder_by_sibling_mod_file_or_descriptor()
    {
        var registry = NewRegistry();
        var withSibling = Make("mod", "Idea Variation BT Addon");
        File.WriteAllText(Path.Combine(Dir, "mod", "Idea Variation BT Addon.mod"), "name=\"BT\"");
        var withDescriptor = Make("mod", "AGOT Flogis Buildings");
        File.WriteAllText(Path.Combine(withDescriptor, "descriptor.mod"), "name=\"AGOT\"");
        var without = Make("mod", "Nur Ordner");

        var bySibling = ModDetector.Detect(registry, new[] { TouchFile(withSibling, "common", "ideas", "x.txt") }, ModMetadata.ClassicSiblingMetadataFile);
        var byDescriptor = ModDetector.Detect(registry, new[] { TouchFile(withDescriptor, "events", "x.txt") }, ModMetadata.ClassicSiblingMetadataFile);
        var none = ModDetector.Detect(registry, new[] { TouchFile(without, "x.txt") }, ModMetadata.ClassicSiblingMetadataFile);

        Assert.Equal(ModDetectionKind.Unknown, bySibling.Kind);
        Assert.Equal(withSibling, bySibling.Folder);
        Assert.Equal(ModDetectionKind.Unknown, byDescriptor.Kind);
        Assert.Equal(withDescriptor, byDescriptor.Folder);
        Assert.Equal(ModDetectionKind.None, none.Kind);
    }
}
