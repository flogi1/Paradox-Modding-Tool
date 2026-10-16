using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.Patches.Ai;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

/// <summary>
/// KI-Anleitung für Patch-Rezepte (Spec 2026-10-12-patch-recipes-ai-guide, Abschnitt 1 und 7): Beispielrezept "harbors" samt Spiel- und Mod-Ordner,
/// erwartetes Ergebnis, Vollständigkeit von reference.md, Kopf und Antwortformat von SKILL.md.
/// </summary>
public sealed class AiRecipeGuideTests : IDisposable
{
    /// <summary>Auf "1" gesetzt, schreibt <see cref="Example_plan_gives_expected_files"/> das Ergebnis nach <c>examples/harbors-expected</c> (nur bewusst, nach Änderungen am Beispiel).</summary>
    private const string RegenerateVariable = "PMT_WRITE_HARBORS_EXPECTED";

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-aiguide-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "locales", "de.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("locales/de.json");
    }

    private static string GuideRoot => Path.Combine(RepoRoot(), "ai-guide", "patch-recipes");

    private static string Examples => Path.Combine(GuideRoot, "examples");

    private static string RecipeFile => Path.Combine(Examples, "harbors", "recipe.json");

    private static string GameRoot => Path.Combine(Examples, "harbors-game");

    private static string ModSource => Path.Combine(Examples, "harbors-mod");

    private static string ExpectedRoot => Path.Combine(Examples, "harbors-expected");

    /// <summary>Liest das Beispiel wie die Kommandozeile und der Import (tolerant, mit unbekannten Eigenschaften).</summary>
    private static RecipeAnswer LoadExample()
    {
        var answer = RecipeAnswerReader.FromJson(File.ReadAllText(RecipeFile));
        Assert.Empty(answer.Problems);
        Assert.NotNull(answer.Recipe);
        return answer;
    }

    private static List<string> RelativeFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

    /// <summary>Text ohne BOM (ReadAllText) und mit "\n" als Zeilenende: git mit autocrlf darf die Zeilenenden beim Auschecken ändern.</summary>
    private static string Normalized(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

    [Fact]
    public void Example_recipe_passes_check()
    {
        var answer = LoadExample();

        var issues = RecipeChecker.Check(answer.Recipe!, new RecipeCheckOptions(GameRoot, ModSource, UnknownProperties: answer.UnknownProperties));

        Assert.Empty(issues.Select(i => $"{i.Severity} {i.Where}: {i.Message}"));
    }

    [Fact]
    public void Example_plan_gives_expected_files()
    {
        // Rezeptordner außerhalb der Mod wie in PMT: das Gedächtnis landet dort, nicht im Ergebnis.
        var mod = Path.Combine(_temp, "mod");
        FolderCopy.CopyTree(ModSource, mod);
        var context = new PatchContext(mod, GameRoot, null, null, RecipesFolder: Path.Combine(_temp, "recipes"));
        var recipe = LoadExample().Recipe!;

        var first = PatchRunner.Plan(context, recipe);
        Assert.Empty(first.Skipped.Select(s => $"{s.RuleId} {s.RowKey}: {s.Reason}"));
        Assert.Empty(first.Warnings);
        Assert.DoesNotContain(first.Files, f => f.Status == PlannedFileStatus.Conflict);
        PatchPlanWriter.CreateAction(context, first).Do();

        if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
        {
            if (Directory.Exists(ExpectedRoot))
            {
                Directory.Delete(ExpectedRoot, recursive: true);
            }

            FolderCopy.CopyTree(mod, ExpectedRoot);
        }

        Assert.True(Directory.Exists(ExpectedRoot), "examples/harbors-expected fehlt; mit " + RegenerateVariable + "=1 einmal erzeugen und prüfen");
        Assert.Equal(RelativeFiles(ExpectedRoot), RelativeFiles(mod));
        foreach (var rel in RelativeFiles(ExpectedRoot))
        {
            Assert.True(Normalized(Path.Combine(ExpectedRoot, rel)) == Normalized(Path.Combine(mod, rel)), "Inhalt weicht ab: " + rel);
        }

        // Ein zweiter Lauf nach dem Anwenden ändert nichts.
        var second = PatchRunner.Plan(context, recipe);
        Assert.Empty(second.Skipped.Select(s => $"{s.RuleId} {s.RowKey}: {s.Reason}"));
        Assert.All(second.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }

    [Fact]
    public void Reference_names_every_property_and_enum_value()
    {
        var reference = File.ReadAllText(Path.Combine(GuideRoot, "reference.md"));
        var types = new[]
        {
            typeof(PatchRecipe), typeof(PatchColumn), typeof(DerivedLookup), typeof(PatchRow), typeof(LineRule), typeof(BlockCopyRule),
            typeof(TemplateRule), typeof(ListEntryRule), typeof(BlockEditRule), typeof(BlockEdit), typeof(PatchWatch), typeof(IgnoredWatchFile),
        };
        var missing = new List<string>();
        foreach (var type in types)
        {
            // Nur [JsonIgnore] ohne Bedingung (Always) fehlt im JSON; description (WhenWritingNull) gehört dazu.
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true } && p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always }))
            {
                var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                if (!reference.Contains("`" + name + "`", StringComparison.Ordinal))
                {
                    missing.Add(type.Name + "." + name);
                }
            }
        }

        foreach (var type in new[] { typeof(ColumnType), typeof(LookupSource), typeof(LookupTake), typeof(RuleScope), typeof(MatchKind), typeof(LineAction), typeof(BlockEditKind) })
        {
            foreach (var value in Enum.GetNames(type).Select(JsonNamingPolicy.CamelCase.ConvertName))
            {
                if (!reference.Contains("`" + value + "`", StringComparison.Ordinal))
                {
                    missing.Add(type.Name + "." + value);
                }
            }
        }

        foreach (var kind in new[] { "lines", "blockCopy", "template", "listEntry", "blockEdit" })
        {
            if (!reference.Contains("`" + kind + "`", StringComparison.Ordinal))
            {
                missing.Add("kind " + kind);
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void Skill_has_front_matter_answer_format_and_commands()
    {
        var skill = File.ReadAllText(Path.Combine(GuideRoot, "SKILL.md")).Replace("\r\n", "\n");

        Assert.StartsWith("---\nname: pmt-patch-recipes\ndescription: ", skill, StringComparison.Ordinal);
        Assert.Contains("<<<FILE recipe.json>>>", skill, StringComparison.Ordinal);
        Assert.Contains("<<<END>>>", skill, StringComparison.Ordinal);
        Assert.Contains("pmt-patch check", skill, StringComparison.Ordinal);
        Assert.Contains("pmt-patch plan", skill, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildText_with_real_guide_contains_example()
    {
        var request = new RecipeTaskRequest("Make harbors normal buildings.", "ck3", null, null, Array.Empty<string>(), Array.Empty<string>(), null, null, null);

        var text = RecipeTaskBuilder.BuildText(request, GuideRoot);

        Assert.Contains("<<<FILE examples/harbors/recipe.json>>>", text, StringComparison.Ordinal);
        Assert.Contains("<<<FILE examples/harbors-game/", text, StringComparison.Ordinal);
        Assert.Contains("<<<FILE examples/harbors-expected/", text, StringComparison.Ordinal);
        // Chat-KIs haben begrenzte Eingabe: die Anleitung selbst bleibt deutlich unter der Warngrenze von 500 KB.
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(text) < 100_000, "BuildText ist " + System.Text.Encoding.UTF8.GetByteCount(text) + " Bytes groß");
    }
}
