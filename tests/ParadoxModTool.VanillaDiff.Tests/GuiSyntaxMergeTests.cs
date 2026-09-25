using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

/// <summary>
/// Regressionstests für Nutzer-Bug (Task 14): GUI-Dateien nutzen "template name { ... }" bzw.
/// "type name = widget { ... }" - Konstrukte ohne Operator, die der Parser in Listenelemente
/// zerlegt. Früher waren sie für Diff und Merge unsichtbar: kein Unterschied wurde angezeigt und
/// alle Mod-Änderungen gingen beim Merge verloren.
/// </summary>
public class GuiSyntaxMergeTests
{
    private const string Vanilla =
        "template a_template {\n\ticon = { texture = \"x.dds\" }\n\tsize = { 10 10 }\n\tshared = yes\n}\n\n" +
        "Types MyTypes {\n\ttype my_button = button {\n\t\tonclick = \"[A]\"\n\t}\n}\n";

    [Fact]
    public void Merge_ModAenderungInTemplateRumpf_WirdUebernommen()
    {
        var mod = "template a_template {\n\ticon = { texture = \"mod.dds\" }\n\tsize = { 10 10 }\n\tshared = yes\n}\n\n" +
                  "Types MyTypes {\n\ttype my_button = button {\n\t\tonclick = \"[A]\"\n\t}\n}\n";

        var result = MergeEngine.Merge(ScriptDocument.Parse(Vanilla), ScriptDocument.Parse(Vanilla), ScriptDocument.Parse(mod));
        var text = ScriptWriter.Write(result.MergedRoot);

        Assert.False(result.HasConflicts);
        Assert.Contains("mod.dds", text);
        Assert.DoesNotContain("x.dds", text);
    }

    [Fact]
    public void Merge_ModHatZeilenHinzugefuegtUndEntfernt_BeidesWirdUebernommen()
    {
        var mod = "template a_template {\n\ticon = { texture = \"x.dds\" }\n\tsize = { 10 10 }\n\tonclick = \"[IV2]\"\n}\n\n" +
                  "Types MyTypes {\n\ttype my_button = button {\n\t\tonclick = \"[A]\"\n\t}\n}\n";

        var result = MergeEngine.Merge(ScriptDocument.Parse(Vanilla), ScriptDocument.Parse(Vanilla), ScriptDocument.Parse(mod));
        var text = ScriptWriter.Write(result.MergedRoot);

        Assert.False(result.HasConflicts);
        Assert.Contains("onclick = \"[IV2]\"", text);   // vom Mod hinzugefügt
        Assert.DoesNotContain("shared", text);          // vom Mod entfernt
    }

    [Fact]
    public void Merge_SpielUpdateAendertAndereStelle_ModAenderungBleibtErhalten()
    {
        var newVanilla = Vanilla.Replace("size = { 10 10 }", "size = { 20 20 }");
        var mod = Vanilla.Replace("x.dds", "mod.dds");

        var result = MergeEngine.Merge(ScriptDocument.Parse(Vanilla), ScriptDocument.Parse(newVanilla), ScriptDocument.Parse(mod));
        var text = ScriptWriter.Write(result.MergedRoot);

        Assert.False(result.HasConflicts);
        Assert.Contains("mod.dds", text);
        Assert.Contains("20", text);
        Assert.DoesNotContain("10", text);
    }

    [Fact]
    public void Merge_NeuerModRumpf_WirdMitKopfzeileEingefuegt()
    {
        var mod = Vanilla + "\ntemplate mod_only {\n\tfoo = bar\n}\n";

        var result = MergeEngine.Merge(ScriptDocument.Parse(Vanilla), ScriptDocument.Parse(Vanilla), ScriptDocument.Parse(mod));
        var text = ScriptWriter.Write(result.MergedRoot);

        Assert.Contains("template mod_only {", text);
        Assert.Contains("foo = bar", text);
    }

    [Fact]
    public void Merge_KonfliktInRumpf_LaesstSichPerModWahlAufloesen()
    {
        var newVanilla = Vanilla.Replace("shared = yes", "shared = no");
        var mod = Vanilla.Replace("shared = yes", "shared = maybe");

        var result = MergeEngine.Merge(ScriptDocument.Parse(Vanilla), ScriptDocument.Parse(newVanilla), ScriptDocument.Parse(mod));
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("template a_template.shared", conflict.Path);

        var patched = ConflictResolution.ApplyResolutions(result.MergedRoot, new[] { new ConflictResolutionChoice(conflict) });

        Assert.Contains("shared = maybe", ScriptWriter.Write(patched));
    }

    [Fact]
    public void Merge_ModEntferntStelleDieSpielUpdateVeraenderte_KonfliktMitEntfernenOption()
    {
        var newVanilla = Vanilla.Replace("shared = yes", "shared = no");
        var mod = Vanilla.Replace("\tshared = yes\n", string.Empty);

        var result = MergeEngine.Merge(ScriptDocument.Parse(Vanilla), ScriptDocument.Parse(newVanilla), ScriptDocument.Parse(mod));
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal(MergeConflictKind.RemoveForMod, conflict.Kind);
        Assert.Contains("shared = no", ScriptWriter.Write(result.MergedRoot));

        var patched = ConflictResolution.ApplyResolutions(result.MergedRoot, new[] { new ConflictResolutionChoice(conflict) });

        Assert.DoesNotContain("shared", ScriptWriter.Write(patched));
    }

    [Fact]
    public void Writer_SchreibtKopfzeileUndRumpfAufEinerZeile()
    {
        var text = ScriptWriter.Write(ScriptDocument.Parse(Vanilla));

        Assert.Contains("template a_template {", text);
        Assert.Contains("Types MyTypes {", text);
        Assert.Contains("type my_button = button {", text);
    }

    [Fact]
    public void Writer_MitOriginaltextUebernahme_BehaeltKopfzeileUndRumpfFormatierung()
    {
        var text = ScriptWriter.Write(ScriptDocument.Parse(Vanilla), preferSourceVerbatim: true);

        Assert.Contains("template a_template {", text);
        Assert.Contains("\ticon = { texture = \"x.dds\" }", text);
    }

    [Fact]
    public void Diff_AenderungInTemplateRumpf_WirdAlsModifiedMitKindernGemeldet()
    {
        var mod = Vanilla.Replace("x.dds", "mod.dds");

        var diffs = ScriptDiffer.Diff(ScriptDocument.Parse(Vanilla), ScriptDocument.Parse(mod));

        var modified = Assert.Single(diffs, d => d.Kind == DiffKind.Modified);
        Assert.NotNull(modified.Children);
        Assert.Contains(modified.Children!, c => c.Kind == DiffKind.Modified);
    }
}
