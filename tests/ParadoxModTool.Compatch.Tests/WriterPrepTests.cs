using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class WriterPrepTests : CompatchTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    [Theory]
    [InlineData("in_game/common/building_types/zz_pmt_compatch.txt", true)]
    [InlineData("in_game/common/building_types/zzz_pmt_compatch.txt", true)]
    [InlineData("in_game/events/00_pmt_compatch.txt", true)]
    [InlineData("in_game/gui/zz_pmt_compatch.gui", true)]
    [InlineData("main_menu/localization/english/replace/zz_pmt_compatch_l_english.yml", true)]
    [InlineData("in_game/common/building_types/pmt_compatch.txt", false)]
    [InlineData("in_game/common/building_types/zz_pmt_compatch_extra.txt", false)]
    public void Own_patch_file_names_are_recognized(string path, bool own) =>
        Assert.Equal(own, OwnPatchFiles.IsOwn(path));

    [Fact]
    public void Base_mod_own_patch_files_are_not_read()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { a = 1 }\n");
        PutA(Buildings + "zz_pmt_compatch.txt", "INJECT:farm = { b = 1 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { b = 1 }\n");
        var input = new CompatchInput
        {
            VanillaFolder = VanillaDir, ModAFolder = ADir, ModBFolder = BDir, Rules = EuVRules(), KeyOperators = EuVOps,
            ExcludeOwnPatchFilesOf = MergeSide.A,
        };

        var resolved = EffectiveEntryResolver.Resolve(input);

        Assert.Contains(resolved.Skipped, s => s.Reason == SkipReason.OwnPatch);
        var farm = resolved.Addresses[EntryAddress.ForKey("in_game/common/building_types", "farm")];
        Assert.DoesNotContain("b = 1", Text(farm.A.Entries));
    }

    [Fact]
    public void Inline_text_has_no_types_wrapper()
    {
        // A ändert size, B ändert tooltip: das Ergebnis ist weder A noch B, also braucht es einen Patch-Eintrag.
        PutVanilla("in_game/gui/v.gui", "types v_types {\n\ttype my_button = button { size = { 10 10 } tooltip = \"x\" }\n}\n");
        PutA("in_game/gui/a.gui", "types a_types {\n\ttype my_button = button { size = { 20 20 } tooltip = \"x\" }\n}\n");
        PutB("in_game/gui/b.gui", "types b_types {\n\ttype my_button = button { size = { 10 10 } tooltip = \"y\" }\n}\n");

        var run = CompatchEngine.Run(Input(), PatchTarget.Compatch);

        var op = Assert.Single(run.Plan.Entries);
        Assert.StartsWith("types pmt_compatch {", op.Text.Split('\n').First(l => !l.StartsWith('#')));
        Assert.DoesNotContain("types pmt_compatch", op.InlineText);
        Assert.Contains("type my_button", op.InlineText);
        Assert.EndsWith("\n", op.InlineText);
    }

    [Fact]
    public void Replacing_key_in_place_does_not_duplicate_leading_comment()
    {
        // EU-IV-Regeln: ohne Präfixe und unsicher, also ist "ganze Datei" die einzige Form.
        PutVanilla("common/buildings/00_x.txt", "farm = { cost = 1 }\nmine = { x = 1 }\n");
        PutA("common/buildings/00_x.txt", "# Kommentar zur Farm\nfarm = { cost = 2 }\nmine = { x = 1 }\n");
        PutB("common/buildings/00_x.txt", "# Kommentar zur Farm\nfarm = { cost = 1 }\nmine = { x = 2 }\n");

        var run = CompatchEngine.Run(Input(euV: false), PatchTarget.Compatch);

        var file = Assert.Single(run.Plan.Files);
        Assert.False(file.Normalized);
        Assert.Equal(1, file.Text!.Split("# Kommentar zur Farm").Length - 1);
        Assert.Contains("cost = 2", file.Text);
        Assert.Contains("x = 2", file.Text);
    }
}
