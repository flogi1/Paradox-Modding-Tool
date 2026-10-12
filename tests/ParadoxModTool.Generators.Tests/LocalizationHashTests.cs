using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

public class LocalizationHashTests
{
    private const string GameKey = "TWS_THE";
    private const string ClashKey = "WE_PERFORM_iv2_ga_select_national_idea_army_30_3_ACTION_TITLE";
    private const string LocPath = "in_game/localization/english/iv2_l_english.yml";

    [Theory]
    [InlineData("TWS_THE", 243924145u)]
    [InlineData("WE_PERFORM_iv2_ga_select_national_idea_army_30_3_ACTION_TITLE", 243924145u)]
    [InlineData("", 0u)]
    public void Murmur3_matches_game(string key, uint hash) => Assert.Equal(hash, LocalizationHash.Murmur3(key));

    [Fact]
    public void Index_ignores_same_key_in_other_files()
    {
        using var game = new TempFolder();
        using var mod = new TempFolder();
        game.Write("in_game/localization/english/a_l_english.yml", "l_english:\n a_key: \"x\"\n");
        game.Write("main_menu/localization/german/a_l_german.yml", "l_german:\n a_key: \"y\"\n");
        mod.Write("in_game/localization/english/b_l_english.yml", "l_english:\n a_key: \"z\"\n");

        var index = LocalizationKeyIndex.Build(new[] { game.Root, mod.Root });

        Assert.Empty(index.Others("a_key"));
    }

    [Fact]
    public void Index_lists_other_keys_with_same_hash_from_all_languages()
    {
        using var game = new TempFolder();
        game.Write("main_menu/localization/german/a_l_german.yml", $"l_german:\n {GameKey}: \"x\"\n");

        var index = LocalizationKeyIndex.Build(new[] { game.Root });

        Assert.Equal(new[] { GameKey }, index.Others(ClashKey));
    }

    [Fact]
    public void Find_reports_new_key_colliding_with_game_key()
    {
        using var game = new TempFolder();
        game.Write("in_game/localization/english/a_l_english.yml", $"l_english:\n {GameKey}: \"x\"\n");
        var index = LocalizationKeyIndex.Build(new[] { game.Root });
        var plan = PlanWith(PlannedYml(LocPath, original: "l_english:\n", newText: $"l_english:\n {ClashKey}: \"t\"\n"));

        var found = HashCollisions.Find(plan, index);

        Assert.Equal(new[] { new HashCollision(ClashKey, GameKey, 243924145u) }, found);
    }

    [Fact]
    public void Find_ignores_keys_already_in_the_file()
    {
        using var game = new TempFolder();
        game.Write("in_game/localization/english/a_l_english.yml", $"l_english:\n {GameKey}: \"x\"\n");
        var index = LocalizationKeyIndex.Build(new[] { game.Root });
        var text = $"l_english:\n {ClashKey}: \"t\"\n";
        var plan = PlanWith(PlannedYml(LocPath, original: text, newText: text));

        Assert.Empty(HashCollisions.Find(plan, index));
    }

    [Fact]
    public void Find_uses_mod_base_text_when_original_is_missing()
    {
        using var game = new TempFolder();
        game.Write("in_game/localization/english/a_l_english.yml", $"l_english:\n {GameKey}: \"x\"\n");
        var index = LocalizationKeyIndex.Build(new[] { game.Root });
        var text = $"l_english:\n {ClashKey}: \"t\"\n";
        var plan = PlanWith(PlannedYml(LocPath, original: null, newText: text, modBase: text));

        Assert.Empty(HashCollisions.Find(plan, index));
    }

    [Fact]
    public void Find_reports_collision_between_two_new_keys_once()
    {
        var index = LocalizationKeyIndex.Build(Array.Empty<string>());
        var english = PlannedYml(LocPath, original: null, newText: $"l_english:\n {GameKey}: \"a\"\n {ClashKey}: \"b\"\n");
        var german = PlannedYml("in_game/localization/german/iv2_l_german.yml", original: null, newText: $"l_german:\n {GameKey}: \"a\"\n {ClashKey}: \"b\"\n");

        var found = HashCollisions.Find(PlanWith(english, german), index);

        Assert.Equal(new[] { new HashCollision(ClashKey, GameKey, 243924145u) }, found);
    }

    [Fact]
    public void Find_skips_files_that_are_not_yml()
    {
        var index = LocalizationKeyIndex.Build(Array.Empty<string>());
        var plan = PlanWith(PlannedYml("common/ideas/x.txt", original: null, newText: $" {GameKey}: \"a\"\n {ClashKey}: \"b\"\n"));

        Assert.Empty(HashCollisions.Find(plan, index));
    }

    [Fact]
    public void NewKeys_lists_each_new_key_once_and_skips_known_and_non_yml()
    {
        var english = PlannedYml(LocPath, original: "l_english:\n old: \"o\"\n", newText: "l_english:\n old: \"o\"\n a: \"x\"\n b: \"y\"\n");
        var german = PlannedYml("in_game/localization/german/iv2_l_german.yml", original: null, newText: "l_german:\n a: \"x\"\n c: \"z\"\n");
        var script = PlannedYml("common/ideas/x.txt", original: null, newText: " d: \"a\"\n");

        Assert.Equal(new[] { "a", "b", "c" }, HashCollisions.NewKeys(PlanWith(english, german, script)));
        Assert.Empty(HashCollisions.NewKeys(PlanWith(PlannedYml(LocPath, original: "l_english:\n a: \"x\"\n", newText: "l_english:\n a: \"y\"\n"))));
    }

    [Fact]
    public void Find_by_new_keys_equals_find_by_plan()
    {
        using var game = new TempFolder();
        game.Write("in_game/localization/english/a_l_english.yml", $"l_english:\n {GameKey}: \"x\"\n");
        var index = LocalizationKeyIndex.Build(new[] { game.Root });
        var plan = PlanWith(PlannedYml(LocPath, original: "l_english:\n", newText: $"l_english:\n {ClashKey}: \"t\"\n"));

        Assert.Equal(HashCollisions.Find(plan, index), HashCollisions.Find(HashCollisions.NewKeys(plan), index));
        Assert.Single(HashCollisions.Find(new[] { ClashKey }, index));
    }

    [Fact]
    public void Combined_index_finds_keys_of_all_parts_once()
    {
        using var game = new TempFolder();
        using var mod = new TempFolder();
        game.Write("in_game/localization/english/a_l_english.yml", $"l_english:\n {GameKey}: \"x\"\n");
        mod.Write("in_game/localization/english/b_l_english.yml", $"l_english:\n {GameKey}: \"y\"\n");
        var gamePart = LocalizationKeyIndex.Build(new[] { game.Root });
        var modPart = LocalizationKeyIndex.Build(new[] { mod.Root });

        Assert.Equal(new[] { GameKey }, LocalizationKeyIndex.Combine(new[] { gamePart, modPart }).Others(ClashKey));
        Assert.Empty(LocalizationKeyIndex.Combine(new[] { LocalizationKeyIndex.Build(Array.Empty<string>()) }).Others(ClashKey));
    }

    [Fact]
    public void Index_reads_hidden_files_in_subfolders() // as Directory.GetFiles did before skipping inaccessible folders
    {
        using var game = new TempFolder();
        var path = game.Write("in_game/localization/english/sub/a_l_english.yml", $"l_english:\n {GameKey}: \"x\"\n");
        File.SetAttributes(path, FileAttributes.Hidden);

        Assert.Equal(new[] { GameKey }, LocalizationKeyIndex.Build(new[] { game.Root }).Others(ClashKey));
    }

    [Fact]
    public void Build_throws_when_token_is_cancelled()
    {
        using var game = new TempFolder();
        game.Write("in_game/localization/english/a_l_english.yml", $"l_english:\n {GameKey}: \"x\"\n");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => LocalizationKeyIndex.Build(new[] { game.Root }, ct: cancellation.Token));
    }

    [Fact]
    public void Build_reports_progress_per_root_and_gives_the_same_index()
    {
        using var game = new TempFolder();
        using var mod = new TempFolder();
        game.Write("in_game/localization/english/a_l_english.yml", $"l_english:\n {GameKey}: \"x\"\n");
        mod.Write("main_menu/localization/german/b_l_german.yml", "l_german:\n a_key: \"y\"\n");
        var progress = new CollectingProgress();

        var index = LocalizationKeyIndex.Build(new[] { game.Root, mod.Root }, progress, CancellationToken.None);

        Assert.Equal(new[] { new KeyIndexProgress(game.Root, 1, 2), new KeyIndexProgress(mod.Root, 2, 2) }, progress.Reports);
        Assert.Equal(LocalizationKeyIndex.Build(new[] { game.Root, mod.Root }).Others(ClashKey), index.Others(ClashKey));
        Assert.Equal(new[] { GameKey }, index.Others(ClashKey));
    }

    /// <summary>Sammelt die Meldungen synchron (<see cref="Progress{T}"/> meldet über den Threadpool und käme zu spät).</summary>
    private sealed class CollectingProgress : IProgress<KeyIndexProgress>
    {
        public List<KeyIndexProgress> Reports { get; } = new();

        public void Report(KeyIndexProgress value) => Reports.Add(value);
    }

    private static GeneratorPlannedFile PlannedYml(string path, string? original, string newText, string? modBase = null) =>
        new(path, original is null ? PlannedFileStatus.New : PlannedFileStatus.Changed, original, newText, null, null, true, "utf-8", Array.Empty<string>(), Array.Empty<string>(), 0, modBase);

    private static GeneratorPlan PlanWith(params GeneratorPlannedFile[] files) => new()
    {
        PackageId = "p",
        Key = "k",
        WriteRoot = "w",
        Files = files,
        Warnings = Array.Empty<string>(),
    };
}
