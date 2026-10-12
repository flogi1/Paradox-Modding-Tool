using System.Text.Json.Nodes;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>KI-Anleitung für Generator-Pakete (Spec 2026-10-05-generator-packages-ai-guide): Beispielpaket, bekannte Schlüssel, Anleitungstexte.</summary>
public sealed class AiGuideTests : IDisposable
{
    private static readonly string[] LanguageCodes = { "de", "en", "fr", "es", "ru", "pl", "pt-br", "zh-hans" };

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    public static string GuideRoot => Path.Combine(Iv2Fixture.RepoRoot, "ai-guide", "generator-packages");

    public static string ExamplePackage => Path.Combine(GuideRoot, "examples", "stadtwappen");

    public static string ExampleMod => Path.Combine(GuideRoot, "examples", "stadtwappen-mod");

    /// <summary>Kopie eines Ordners im Temp-Ordner (Tests ändern nie die Dateien im Repo).</summary>
    public static string CopyTo(TempFolder temp, string source, string name)
    {
        var target = temp.PathOf(name);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }

        return target;
    }

    [Fact]
    public void Example_package_loads()
    {
        var folder = CopyTo(_temp, ExamplePackage, "stadtwappen");

        var package = GeneratorPackageLoader.Load(folder);

        Assert.Equal("stadtwappen", package.Id);
        Assert.Empty(PackageChecker.Check(PackageDocument.Load(folder), new PackageFiles(folder), "en"));
    }

    /// <summary>Der Satz HAM der Beispiel-Mod: zwei Ideen, Englisch und Deutsch.</summary>
    public static GeneratorState ExampleState(GeneratorPackage package)
    {
        var state = GeneratorState.CreateEmpty(package, new[] { "english", "german" });
        state.Key = "HAM";
        state.Head.Values["tag"] = "HAM";
        var level = package.Root!;
        var ideas = new List<FormNode>();
        foreach (var (name, german, desc, germanDesc, modifiers) in new[]
        {
            ("Hanseatic pride", "Hansestolz", "Our merchants sail every sea.", "Unsere Kaufleute befahren jedes Meer.", "global_trade_efficiency = 0.1\nnaval_morale = 0.05"),
            ("Harbour gate", "Hafentor", "The gate guards the city.", "Das Tor schützt die Stadt.", "fort_defense = 0.1"),
        })
        {
            var node = FormNode.Create(level.Fields, level.Child, package.Constants);
            node.Values["name"] = name;
            node.Values["desc"] = desc;
            node.Values["modifiers"] = modifiers;
            node.Translations["name"] = new Dictionary<string, string> { ["german"] = german };
            node.Translations["desc"] = new Dictionary<string, string> { ["german"] = germanDesc };
            ideas.Add(node);
        }

        state.Head.Children["ideas"] = ideas;
        return state;
    }

    [Fact]
    public void Example_writes_numbered_blocks_and_reads_them_back()
    {
        var package = GeneratorPackageLoader.Load(CopyTo(_temp, ExamplePackage, "stadtwappen"));
        var mod = _temp.PathOf("mod");
        SlicePackage.Write(GeneratorPlanner.Plan(package, ExampleState(package), new GeneratorTarget(mod, null, new[] { "english", "german" })));

        Assert.Contains("sw_modifier_HAM_2 = {\n\tfort_defense = 0.1\n}", File.ReadAllText(Path.Combine(mod, "in_game/common/static_modifiers/sw_HAM.txt")).Replace("\r\n", "\n"));
        var read = GeneratorStateReader.Read(package, mod, "HAM", new[] { "english", "german" })!.State;
        var ideas = read.Head.Children["ideas"];
        Assert.Equal(2, ideas.Count);
        Assert.Equal("Harbour gate", ideas[1].Values["name"]);
        Assert.Equal("Hafentor", ideas[1].Translations["name"]["german"]);
        Assert.Equal("global_trade_efficiency = 0.1\nnaval_morale = 0.05", ideas[0].Values["modifiers"]);
    }

    [Fact]
    public void Reference_mentions_every_known_key()
    {
        var reference = File.ReadAllText(Path.Combine(GuideRoot, "reference.md"));

        var missing = GeneratorPackageLoader.KnownKeys.Where(k => !reference.Contains("`" + k + "`", StringComparison.Ordinal)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Skill_has_front_matter_and_answer_format()
    {
        var skill = File.ReadAllText(Path.Combine(GuideRoot, "SKILL.md")).Replace("\r\n", "\n");

        Assert.StartsWith("---\nname: pmt-generator-packages\ndescription: ", skill);
        Assert.Contains("<<<FILE package.json>>>", skill);
        Assert.Contains("<<<END>>>", skill);
        Assert.Contains("pmt-generator roundtrip", skill);
    }

    [Fact]
    public void KnownKeys_cover_used_keys()
    {
        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in new[] { Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2", "package.json"), Path.Combine(ExamplePackage, "package.json") })
        {
            Collect(JsonNode.Parse(File.ReadAllText(file))!, null, used);
        }

        var missing = used.Except(GeneratorPackageLoader.KnownKeys).ToList();
        Assert.Empty(missing);
    }

    /// <summary>Eigenschaftsnamen ohne frei benannte Teile (Konstanten-Zeilen, Sprachtexte, Zuordnungen und Feldlisten mit Feldkennungen).</summary>
    private static void Collect(JsonNode node, string? parentKey, ISet<string> names)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var item in array.OfType<JsonNode>())
                {
                    Collect(item, parentKey, names);
                }

                break;
            case JsonObject obj when obj.Count > 0 && obj.All(p => LanguageCodes.Contains(p.Key)):
                break;
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    names.Add(key);
                    var freeNames = key is "constants" or "fills" || key == "fields" && value is JsonObject;
                    if (key == "assets" && value is JsonObject assets)
                    {
                        // Asset-Kennungen sind frei; nur ihre Angaben (path, origin) zählen.
                        foreach (var (_, asset) in assets)
                        {
                            if (asset is not null)
                            {
                                Collect(asset, key, names);
                            }
                        }
                    }
                    else if (value is not null && !freeNames)
                    {
                        Collect(value, key, names);
                    }
                }

                break;
        }
    }
}
