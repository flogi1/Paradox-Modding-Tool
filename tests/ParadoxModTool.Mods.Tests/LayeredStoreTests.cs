using System.Text.Json;
using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class LayeredStoreTests : TempDirTests
{
    private sealed class Sample
    {
        public string Text { get; set; } = string.Empty;

        public bool Migrated { get; set; }
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private string GameFolder => Path.Combine(Dir, "game-templates");

    private string ModFolder => Path.Combine(Dir, "mod-templates");

    private LayeredStore<Sample> Store(bool withMod = true, Action<Sample>? afterLoad = null) =>
        new(layer => layer == StoreLayer.Game ? GameFolder : withMod ? ModFolder : null, Json, afterLoad, "Eintrag");

    [Fact]
    public void Save_and_load_work_per_layer()
    {
        var store = Store();

        store.Save("x", StoreLayer.Mod, new Sample { Text = "mod" });

        Assert.Null(store.Load("x", StoreLayer.Game));
        Assert.Equal("mod", store.Load("x", StoreLayer.Mod)!.Text);
        Assert.True(File.Exists(Path.Combine(ModFolder, "x.json")));
        Assert.False(File.Exists(Path.Combine(GameFolder, "x.json")));
    }

    [Fact]
    public void List_shows_a_name_once_per_layer_sorted_by_name()
    {
        var store = Store();
        store.Save("b", StoreLayer.Game, new Sample());
        store.Save("A", StoreLayer.Game, new Sample());
        store.Save("A", StoreLayer.Mod, new Sample());

        var entries = store.List();

        Assert.Equal(
            new[] { new LayeredEntry("A", StoreLayer.Game), new LayeredEntry("A", StoreLayer.Mod), new LayeredEntry("b", StoreLayer.Game) },
            entries);
        Assert.Equal(new[] { "A", "b" }, store.List(StoreLayer.Game));
        Assert.Equal(new[] { "A" }, store.List(StoreLayer.Mod));
    }

    [Fact]
    public void Resolve_prefers_the_most_specific_layer_and_falls_back()
    {
        var store = Store();
        store.Save("both", StoreLayer.Game, new Sample { Text = "spiel" });
        store.Save("both", StoreLayer.Mod, new Sample { Text = "mod" });
        store.Save("only-game", StoreLayer.Game, new Sample { Text = "nur spiel" });

        var both = store.Resolve("both");
        var onlyGame = store.Resolve("only-game");

        Assert.Equal(("mod", StoreLayer.Mod), (both!.Value.Value.Text, both.Value.Layer));
        Assert.Equal(("nur spiel", StoreLayer.Game), (onlyGame!.Value.Value.Text, onlyGame.Value.Layer));
        Assert.Null(store.Resolve("gibt-es-nicht"));
    }

    [Fact]
    public void Delete_removes_only_the_named_layer()
    {
        var store = Store();
        store.Save("x", StoreLayer.Game, new Sample());
        store.Save("x", StoreLayer.Mod, new Sample());

        store.Delete("x", StoreLayer.Mod);
        store.Delete("gibt-es-nicht", StoreLayer.Mod);

        Assert.True(store.Exists("x", StoreLayer.Game));
        Assert.False(store.Exists("x", StoreLayer.Mod));
    }

    [Fact]
    public void A_missing_folder_or_a_layer_without_folder_lists_nothing_and_loads_null()
    {
        var store = Store(withMod: false);

        Assert.Empty(store.List());
        Assert.Null(store.Load("x", StoreLayer.Mod));
        Assert.False(store.Exists("x", StoreLayer.Mod));
        store.Delete("x", StoreLayer.Mod);
    }

    [Fact]
    public void Saving_to_a_layer_without_folder_throws()
    {
        var store = Store(withMod: false);

        Assert.Throws<InvalidOperationException>(() => store.Save("x", StoreLayer.Mod, new Sample()));
    }

    [Fact]
    public void Names_starting_with_an_underscore_are_hidden_from_the_list_but_can_be_used()
    {
        var store = Store();

        store.Save("_last", StoreLayer.Game, new Sample { Text = "zuletzt" });

        Assert.Empty(store.List());
        Assert.Equal("zuletzt", store.Load("_last", StoreLayer.Game)!.Text);
    }

    [Fact]
    public void A_broken_file_loads_as_null_and_stays_in_the_list()
    {
        var store = Store();
        Directory.CreateDirectory(GameFolder);
        File.WriteAllText(Path.Combine(GameFolder, "kaputt.json"), "{ nein");

        Assert.Null(store.Load("kaputt", StoreLayer.Game));
        Assert.Equal(new[] { "kaputt" }, store.List(StoreLayer.Game));
    }

    [Fact]
    public void The_after_load_hook_runs_for_every_loaded_value()
    {
        var store = Store(afterLoad: s => s.Migrated = true);
        store.Save("x", StoreLayer.Mod, new Sample());

        Assert.True(store.Load("x", StoreLayer.Mod)!.Migrated);
        Assert.True(store.Resolve("x")!.Value.Value.Migrated);
    }

    [Fact]
    public void Unsafe_names_are_cleaned()
    {
        var store = Store();

        store.Save("a/b:c", StoreLayer.Game, new Sample());

        Assert.Equal(new[] { "a_b_c" }, store.List(StoreLayer.Game));
        Assert.NotNull(store.Load("a/b:c", StoreLayer.Game));
        Assert.Equal("Eintrag", StoreNames.Safe("  . ", "Eintrag"));
    }
}
