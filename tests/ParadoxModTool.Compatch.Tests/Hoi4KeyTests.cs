using ParadoxModTool.ModCompat;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>
/// HOI IV: Ideen stehen in zwei Hüllen (<c>ideas = { country = { … } }</c>), Entscheidungen in frei benannten Kategorien. Der Patch schreibt alle
/// Hüllen wieder mit.
/// </summary>
public class Hoi4KeyTests : CompatchTestBase
{
    private CompatchInput Hoi4Input(bool certain) => new()
    {
        VanillaFolder = VanillaDir,
        ModAFolder = ADir,
        ModBFolder = BDir,
        Rules = new OverrideRules(new[]
        {
            new OverrideRule("common", OverrideBehavior.LastWins, certain),
            new OverrideRule("common/ideas", OverrideBehavior.PerKey, certain, new[] { "ideas/*" }),
            new OverrideRule("common/decisions", OverrideBehavior.PerKey, certain, new[] { "*" }),
        }),
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Same_idea_in_differently_named_files_is_written_inside_both_wrappers(bool certain)
    {
        PutVanilla("common/ideas/GER.txt", "ideas = {\n\tcountry = {\n\t\tGER_x = { picture = v cost = 1 }\n\t}\n}\n");
        PutA("common/ideas/a.txt", "ideas = {\n\tcountry = {\n\t\tGER_x = { picture = a cost = 1 }\n\t}\n}\n");
        PutB("common/ideas/b.txt", "ideas = {\n\tcountry = {\n\t\tGER_x = { picture = v cost = 2 }\n\t}\n}\n");
        var input = Hoi4Input(certain);

        var run = CompatchEngine.Run(input, PatchTarget.Compatch, null, null);

        AssertRoundTrip(input, run);
        Assert.Contains(EntryAddress.ForKey("common/ideas", "ideas > country > GER_x"), run.Resolved.Addresses.Keys);
        if (certain)
        {
            // Der Planer schreibt hier die ganze Gewinnerdatei neu (sie enthält nur diese Idee); der Text steht in beiden Hüllen.
            var text = run.Plan.Entries.Select(e => e.Text).Concat(run.Plan.Files.Select(f => f.Text ?? string.Empty)).Single().Replace("\r\n", "\n");
            Assert.StartsWith("ideas = {\n", text);
            Assert.Contains("\tcountry = {\n\t\tGER_x = {", text);
            Assert.Contains("picture = a", text);
            Assert.Contains("cost = 2", text);
        }
    }

    [Fact]
    public void New_idea_entry_is_written_inside_both_wrappers()
    {
        PutVanilla("common/ideas/GER.txt", "ideas = {\n\tcountry = {\n\t\tGER_x = { picture = v cost = 1 }\n\t\tGER_y = { picture = v }\n\t}\n}\n");
        PutA("common/ideas/a.txt", "ideas = {\n\tcountry = {\n\t\tGER_x = { picture = a cost = 1 }\n\t\tGER_a = { picture = a }\n\t}\n}\n");
        PutB("common/ideas/b.txt", "ideas = {\n\tcountry = {\n\t\tGER_x = { picture = v cost = 2 }\n\t\tGER_b = { picture = b }\n\t}\n}\n");
        var input = Hoi4Input(true);

        var run = CompatchEngine.Run(input, PatchTarget.Compatch, null, null);

        AssertRoundTrip(input, run);
        var texts = run.Plan.Entries.Select(e => e.Text).Concat(run.Plan.Files.Select(f => f.Text ?? string.Empty)).Select(t => t.Replace("\r\n", "\n")).ToList();
        Assert.Contains(texts, t => t.Contains("country = {") && t.Contains("GER_x = {") && t.Contains("cost = 2") && t.Contains("picture = a"));
        Assert.DoesNotContain(texts, t => t.StartsWith("ideas > ", StringComparison.Ordinal) || t.Contains("ideas > country"));
    }

    private CompatchInput Hoi4ProfileLikeInput() => new()
    {
        VanillaFolder = VanillaDir,
        ModAFolder = ADir,
        ModBFolder = BDir,
        Rules = new OverrideRules(new[]
        {
            new OverrideRule("common", OverrideBehavior.LastWins, true),
            new OverrideRule("common/ideas", OverrideBehavior.FirstWins, true, new[] { "ideas/*" }),
            new OverrideRule("common/technologies", OverrideBehavior.PerKey, true, new[] { "*" }),
        }),
    };

    private static IEnumerable<string> WrittenTexts(CompatchRun run) =>
        run.Plan.Entries.Select(e => e.Text).Concat(run.Plan.Files.Select(f => f.Text ?? string.Empty)).Select(t => t.Replace("\r\n", "\n"));

    [Fact]
    public void Law_flags_survive_a_first_wins_patch()
    {
        // Review 2026-10-16: Werte in einer Hülle (law = yes) sind keine Schlüssel; der Patch darf sie nicht verlieren.
        PutVanilla("common/ideas/zz_economic.txt", "ideas = {\n\teconomy = {\n\t\tlaw = yes\n\t\tuse_list_view = yes\n\t\tcivilian_economy = { cost = 1 picture = v }\n\t}\n}\n");
        PutA("common/ideas/a_laws.txt", "ideas = {\n\teconomy = {\n\t\tlaw = yes\n\t\tuse_list_view = yes\n\t\tcivilian_economy = { cost = 2 picture = v }\n\t}\n}\n");
        PutB("common/ideas/b_laws.txt", "ideas = {\n\teconomy = {\n\t\tlaw = yes\n\t\tuse_list_view = yes\n\t\tcivilian_economy = { cost = 1 picture = b }\n\t}\n}\n");
        var input = Hoi4ProfileLikeInput();

        var run = CompatchEngine.Run(input, PatchTarget.Compatch, null, null);

        // Die Hülle economy hat Werte (law = yes): der Kompatch zerlegt sie nicht in Schlüssel, sondern lässt sie ganz im Dateirest.
        AssertRoundTrip(input, run);
        Assert.DoesNotContain(EntryAddress.ForKey("common/ideas", "ideas > economy > civilian_economy"), run.Resolved.Addresses.Keys);
        Assert.All(WrittenTexts(run).Where(t => t.Contains("civilian_economy", StringComparison.Ordinal)), t =>
        {
            Assert.Contains("law = yes", t);
            Assert.Contains("use_list_view = yes", t);
        });
    }

    [Fact]
    public void Law_flags_survive_a_same_file_patch()
    {
        const string Path = "common/ideas/_economic.txt";
        PutVanilla(Path, "ideas = {\n\teconomy = {\n\t\tlaw = yes\n\t\tuse_list_view = yes\n\t\tcivilian_economy = { cost = 1 picture = v }\n\t}\n}\n");
        PutA(Path, "ideas = {\n\teconomy = {\n\t\tlaw = yes\n\t\tuse_list_view = yes\n\t\tcivilian_economy = { cost = 2 picture = v }\n\t}\n}\n");
        PutB(Path, "ideas = {\n\teconomy = {\n\t\tlaw = yes\n\t\tuse_list_view = yes\n\t\tcivilian_economy = { cost = 1 picture = v }\n\t\tmy_economy = { cost = 1 }\n\t}\n}\n");
        var input = Hoi4ProfileLikeInput();

        var run = CompatchEngine.Run(input, PatchTarget.Compatch, null, null);

        AssertRoundTrip(input, run);
        Assert.NotEmpty(WrittenTexts(run).Where(t => t.Contains("civilian_economy", StringComparison.Ordinal)));
        Assert.All(WrittenTexts(run).Where(t => t.Contains("civilian_economy", StringComparison.Ordinal)), t =>
        {
            Assert.Contains("law = yes", t);
            Assert.Contains("use_list_view = yes", t);
        });
    }

    [Fact]
    public void Constants_inside_a_technologies_wrapper_survive_a_patch()
    {
        PutVanilla("common/technologies/armor.txt", "technologies = {\n\t@1936 = 2\n\ttank_a = { research_cost = @1936 x = 1 }\n}\n");
        PutA("common/technologies/armor.txt", "@top = 1\ntechnologies = {\n\t@1936 = 2\n\ttank_a = { research_cost = @1936 x = 2 }\n}\n");
        PutB("common/technologies/armor.txt", "@top = 2\ntechnologies = {\n\t@1936 = 2\n\ttank_a = { research_cost = @1936 x = 1 y = 1 }\n}\n");
        var input = Hoi4ProfileLikeInput();

        var run = CompatchEngine.Run(input, PatchTarget.Compatch, null, null);

        AssertRoundTrip(input, run);
        Assert.NotEmpty(WrittenTexts(run).Where(t => t.Contains("research_cost = @1936", StringComparison.Ordinal)));
        Assert.All(WrittenTexts(run).Where(t => t.Contains("research_cost = @1936", StringComparison.Ordinal)), t =>
            Assert.Contains("@1936 = 2", t));
    }

    [Fact]
    public void Same_decision_in_differently_named_category_files_is_one_address()
    {
        PutVanilla("common/decisions/GER.txt", "GER_cat = {\n\tGER_dec = { cost = 1 }\n}\n");
        PutA("common/decisions/a.txt", "GER_cat = {\n\tGER_dec = { cost = 2 }\n}\n");
        PutB("common/decisions/b.txt", "GER_cat = {\n\tGER_dec = { cost = 3 }\n}\n");

        var keys = EffectiveEntryResolver.Resolve(Hoi4Input(false)).Addresses.Keys.Where(a => a.Kind == AddressKind.Key).ToList();

        Assert.Equal(new[] { EntryAddress.ForKey("common/decisions", "GER_cat > GER_dec") }, keys);
    }
}
