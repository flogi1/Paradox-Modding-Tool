using ParadoxModTool.Patches.Ai;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Import einer KI-Antwort vorbereiten (Spec 2026-10-12-patch-recipes-ai-guide, Abschnitt 2 "Ergebnis"): Spiel, Id, Vorlage, entfallene Regeln, Prüfung.</summary>
public sealed class RecipeImportTests
{
    private static PatchRecipe Answer(string id = "ai_id") => new()
    {
        Id = id,
        Name = "Kasernen",
        GameId = "eu4",
        Preset = "ck3-special-buildings",
        Columns = { new PatchColumn { Name = "castle" } },
        Rows = { new PatchRow { Key = "k1", Values = { ["castle"] = "x" } } },
        Rules = { new TemplateRule { Id = "template1", Name = "Vorlage", Target = "common/x.txt", Text = "{key} = { }" } },
    };

    private static PatchRecipe Open() => new()
    {
        Id = "open_id",
        Name = "Offen",
        GameId = "ck3",
        Preset = SpecialBuildingsPreset.Id,
        Rules =
        {
            new TemplateRule { Id = "template1", Target = "common/x.txt", Text = "x" },
            new TemplateRule { Id = "old_rule", Target = "common/y.txt", Text = "y" },
        },
    };

    [Fact]
    public void GameId_is_set_and_foreign_preset_cleared()
    {
        var plan = RecipeImport.Prepare(Answer(), "ck3", Array.Empty<string>(), null, null, null, null);

        Assert.Equal("ck3", plan.Recipe.GameId);
        Assert.Null(plan.Recipe.Preset);
        Assert.False(plan.IdReset);
        Assert.False(plan.IdConflict);
        Assert.Empty(plan.DroppedRules);
        Assert.Equal("ai_id", plan.Recipe.Id);
        Assert.Empty(plan.Issues);
        Assert.False(plan.HasErrors);
    }

    [Fact]
    public void Answer_object_is_not_changed()
    {
        var answer = Answer();

        RecipeImport.Prepare(answer, "ck3", Array.Empty<string>(), Open(), null, null, null);

        Assert.Equal("eu4", answer.GameId);
        Assert.Equal("ai_id", answer.Id);
        Assert.Equal("ck3-special-buildings", answer.Preset);
    }

    [Fact]
    public void Open_recipe_id_is_restored_and_preset_kept()
    {
        var plan = RecipeImport.Prepare(Answer(), "ck3", new[] { "open_id" }, Open(), null, null, null);

        Assert.Equal("open_id", plan.Recipe.Id);
        Assert.True(plan.IdReset);
        Assert.Equal(SpecialBuildingsPreset.Id, plan.Recipe.Preset);
        Assert.Equal("ck3", plan.Recipe.GameId);
        Assert.True(plan.IdConflict);
    }

    [Fact]
    public void Same_id_as_open_recipe_is_no_reset()
    {
        var plan = RecipeImport.Prepare(Answer("open_id"), "ck3", new[] { "open_id" }, Open(), null, null, null);

        Assert.Equal("open_id", plan.Recipe.Id);
        Assert.False(plan.IdReset);
    }

    [Fact]
    public void Open_recipe_without_preset_clears_the_answers_preset()
    {
        var open = Open();
        open.Preset = null;

        var plan = RecipeImport.Prepare(Answer(), "ck3", Array.Empty<string>(), open, null, null, null);

        Assert.Null(plan.Recipe.Preset);
    }

    [Fact]
    public void Existing_id_is_conflict()
    {
        var plan = RecipeImport.Prepare(Answer("taken"), "ck3", new[] { "other", "taken" }, null, null, null, null);

        Assert.True(plan.IdConflict);
        Assert.False(plan.IdReset);
    }

    [Fact]
    public void Dropped_rule_with_memory_is_listed()
    {
        var memory = new PatchMemory();
        memory.Units[PatchMemory.UnitId("old_rule", "k1")] = "text";
        var answer = Answer();

        var plan = RecipeImport.Prepare(answer, "ck3", new[] { "open_id" }, Open(), memory, null, null);

        Assert.Equal(new[] { "old_rule" }, plan.DroppedRules);
        // Entfallene Regeln fragt das Fenster mit einer Bestätigung ab, sie sind kein Prüffehler.
        Assert.False(plan.HasErrors);
    }

    [Fact]
    public void Dropped_rule_without_output_is_not_listed()
    {
        var plan = RecipeImport.Prepare(Answer(), "ck3", new[] { "open_id" }, Open(), new PatchMemory(), null, null);

        Assert.Empty(plan.DroppedRules);
    }

    [Fact]
    public void Issues_come_from_the_check_with_unknown_properties()
    {
        var answer = Answer();
        answer.Rules[0].Id = "bad id";

        var plan = RecipeImport.Prepare(answer, "ck3", Array.Empty<string>(), null, null, null, null, new[] { "$.colour" });

        Assert.True(plan.HasErrors);
        Assert.Contains(plan.Issues, i => i.Severity == CheckSeverity.Error && i.Where == "rule bad id");
        Assert.Contains(plan.Issues, i => i.Severity == CheckSeverity.Warning && i.Message.Contains("colour"));
    }

    [Fact]
    public void Check_sees_the_set_game_and_ids_after_reset()
    {
        // Die Prüfung läuft auf dem vorbereiteten Rezept: nach dem Zurücksetzen der Id gibt es keine "Id weicht ab"-Meldung.
        var plan = RecipeImport.Prepare(Answer(), "ck3", Array.Empty<string>(), Open(), null, null, null);

        Assert.DoesNotContain(plan.Issues, i => i.Where == "recipe");
    }

    private static PatchRecipe Other() => new()
    {
        Id = "other_id",
        Name = "Anderes",
        GameId = "ck3",
        Preset = SpecialResourcesPreset.Id,
        Rules =
        {
            new TemplateRule { Id = "template1", Target = "common/x.txt", Text = "x" },
            new TemplateRule { Id = "other_rule", Target = "common/z.txt", Text = "z" },
        },
    };

    [Fact]
    public void Hand_over_uses_the_recipe_named_by_the_answers_id()
    {
        // Ruling 12: Auftrag mit Rezept A gebaut, dann B gewählt; die Antwort trägt die Id von A: A ist das offene Rezept, B bleibt unberührt.
        var other = Other();
        PatchRecipe? Find(string id) => id == "other_id" ? other : id == "open_id" ? Open() : null;

        Assert.Same(other, RecipeImport.HandedOver(Answer("other_id"), Open(), Find));
    }

    [Fact]
    public void Hand_over_with_unknown_or_empty_id_uses_the_selected_recipe()
    {
        var selected = Open();
        PatchRecipe? Find(string id) => id == "other_id" ? Other() : null;

        Assert.Same(selected, RecipeImport.HandedOver(Answer("ai_id"), selected, Find));
        Assert.Same(selected, RecipeImport.HandedOver(Answer(string.Empty), selected, Find));
        Assert.Null(RecipeImport.HandedOver(Answer("other_id"), null, Find));
    }

    [Fact]
    public void Prepare_with_the_recipe_named_by_the_answer_keeps_its_id_preset_and_memory()
    {
        var other = Other();
        var memory = new PatchMemory();
        memory.Units[PatchMemory.UnitId("other_rule", "k1")] = "text";
        var open = RecipeImport.HandedOver(Answer("other_id"), Open(), id => id == "other_id" ? other : null);

        var plan = RecipeImport.Prepare(Answer("other_id"), "ck3", new[] { "open_id", "other_id" }, open, memory, null, null);

        Assert.Equal("other_id", plan.Recipe.Id);
        Assert.False(plan.IdReset);
        Assert.True(plan.IdConflict);
        Assert.Equal(SpecialResourcesPreset.Id, plan.Recipe.Preset);
        Assert.Equal(new[] { "other_rule" }, plan.DroppedRules);
    }

    [Fact]
    public void Answer_without_name_gets_its_id_as_name()
    {
        var answer = Answer();
        answer.Name = " ";

        Assert.Equal("ai_id", RecipeImport.Prepare(answer, "ck3", Array.Empty<string>(), null, null, null, null).Recipe.Name);
        // Mit zurückgesetzter Id: der Name folgt der endgültigen Id.
        Assert.Equal("open_id", RecipeImport.Prepare(answer, "ck3", Array.Empty<string>(), Open(), null, null, null).Recipe.Name);
    }

    public static IEnumerable<object[]> Presets()
    {
        yield return new object[] { SpecialBuildingsPreset.Create("Special Buildings", Array.Empty<string>()) };
        yield return new object[] { SpecialResourcesPreset.Create("Special Resources", Array.Empty<string>()) };
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void Presets_pass_the_check_and_the_hand_over(PatchRecipe preset)
    {
        // Die Vorlagen müssen sich unverändert mitgeben und zurück übernehmen lassen (auch mit leerer Iterator-Regel).
        Assert.DoesNotContain(RecipeChecker.Check(preset), i => i.Severity == CheckSeverity.Error);

        var plan = RecipeImport.Prepare(PatchRecipeStore.Clone(preset), "ck3", new[] { preset.Id }, preset, new PatchMemory(), null, null);

        Assert.False(plan.HasErrors, string.Join(" | ", plan.Issues.Select(i => $"{i.Severity} {i.Where}: {i.Message}")));
        Assert.False(plan.IdReset);
        Assert.Equal(preset.Preset, plan.Recipe.Preset);
    }

    [Fact]
    public void WithNewId_gives_free_id()
    {
        var recipe = Answer();

        var copy = RecipeImport.WithNewId(recipe, new[] { "kasernen", "kasernen_2" });

        Assert.Equal("kasernen_3", copy.Id);
        Assert.Equal("ai_id", recipe.Id);
        Assert.Equal("Kasernen", copy.Name);
        Assert.Single(copy.Rules);
    }
}
