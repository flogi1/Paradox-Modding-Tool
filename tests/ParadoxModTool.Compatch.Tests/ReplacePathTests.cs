using ParadoxModTool.ModCompat;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Vanilla-Dateien, die eine Mod mit <c>replace_path</c> ausblendet, lädt das Spiel nicht; der Kompatch vergleicht dort ohne Vanilla.</summary>
public class ReplacePathTests : CompatchTestBase
{
    private ResolvedEntries Resolve(params string[] hidden)
    {
        var input = Input(euV: false);
        return EffectiveEntryResolver.Resolve(new CompatchInput
        {
            VanillaFolder = input.VanillaFolder,
            ModAFolder = input.ModAFolder,
            ModBFolder = input.ModBFolder,
            Rules = input.Rules,
            KeyOperators = input.KeyOperators,
            HiddenVanillaFolders = hidden,
        });
    }

    private void Setup(string folder)
    {
        PutVanilla(folder + "/00_x.txt", "farm = { cost = 1 }\n");
        PutA(folder + "/a.txt", "farm = { cost = 2 }\n");
        PutB(folder + "/b.txt", "farm = { cost = 3 }\n");
    }

    [Fact]
    public void Hidden_vanilla_file_has_no_vanilla_side()
    {
        Setup("common/buildings");

        var info = Resolve("common/buildings").Addresses[EntryAddress.ForKey("common/buildings", "farm")];

        Assert.Empty(info.Vanilla.Entries);
        Assert.Contains("cost = 3", Text(info.NoPatch.Entries));
    }

    [Fact]
    public void Subfolder_of_hidden_folder_keeps_vanilla()
    {
        Setup("common/buildings/sub");

        var info = Resolve("common/buildings").Addresses[EntryAddress.ForKey("common/buildings", "farm")];

        Assert.Contains("cost = 1", Text(info.Vanilla.Entries));
    }

    private ResolvedEntries ResolveHiddenForA(params string[] hiddenForA)
    {
        var input = Input(euV: false);
        return EffectiveEntryResolver.Resolve(new CompatchInput
        {
            VanillaFolder = input.VanillaFolder,
            ModAFolder = input.ModAFolder,
            ModBFolder = input.ModBFolder,
            Rules = input.Rules,
            KeyOperators = input.KeyOperators,
            HiddenFoldersOfA = hiddenForA,
        });
    }

    [Fact]
    public void File_of_earlier_mod_hidden_by_later_mod_is_not_read()
    {
        // CK III (gemessen 2026-09-30): B lädt nach A und blendet den Ordner aus, also lädt das Spiel die Datei von A nicht.
        Setup("common/buildings");

        // Ohne die Datei von A ändert nur B den Eintrag: nichts zu entscheiden, die Adresse fehlt.
        Assert.False(ResolveHiddenForA("common/buildings").Addresses.ContainsKey(EntryAddress.ForKey("common/buildings", "farm")));
        Assert.True(ResolveHiddenForA().Addresses.ContainsKey(EntryAddress.ForKey("common/buildings", "farm")));
    }

    private static readonly string[] EuVRoots = { "in_game", "main_menu", "loading_screen" };

    private ResolvedEntries ResolveEuV(IReadOnlyCollection<string> hiddenVanilla, IReadOnlyCollection<string> hiddenForA)
    {
        var input = Input();
        return EffectiveEntryResolver.Resolve(new CompatchInput
        {
            VanillaFolder = input.VanillaFolder,
            ModAFolder = input.ModAFolder,
            ModBFolder = input.ModBFolder,
            Rules = input.Rules,
            KeyOperators = input.KeyOperators,
            HiddenVanillaFolders = hiddenVanilla,
            HiddenFoldersOfA = hiddenForA,
            ReplacePathRoots = EuVRoots,
        });
    }

    [Fact]
    public void Eu5_path_without_root_hides_vanilla()
    {
        // EU V (gemessen 2026-09-30): replace_paths nennt den Ordner ohne in_game/ davor.
        Setup("in_game/common/buildings");

        var info = ResolveEuV(new[] { "common/buildings" }, Array.Empty<string>()).Addresses[EntryAddress.ForKey("in_game/common/buildings", "farm")];

        Assert.Empty(info.Vanilla.Entries);
    }

    [Fact]
    public void Eu5_path_without_root_hides_earlier_mod_file()
    {
        Setup("in_game/common/buildings");

        Assert.False(ResolveEuV(Array.Empty<string>(), new[] { "common/buildings" }).Addresses.ContainsKey(EntryAddress.ForKey("in_game/common/buildings", "farm")));
    }

    [Fact]
    public void Without_hidden_folders_vanilla_is_read()
    {
        Setup("common/buildings");

        var info = Resolve().Addresses[EntryAddress.ForKey("common/buildings", "farm")];

        Assert.Contains("cost = 1", Text(info.Vanilla.Entries));
    }
}
