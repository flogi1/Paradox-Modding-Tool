using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModLocksTests : TempDirTests
{
    private ModRegistry NewRegistry() => ModRegistry.Load(Make("game"));

    [Theory]
    [InlineData("common/a.txt", "common/a.txt")]
    [InlineData("common\\a.txt", "common/a.txt")]
    [InlineData("common/a.txt/", "common/a.txt")]
    [InlineData("./common//a.txt", "common/a.txt")]
    public void NormalizePath_LiefertSchraegstrichPfad(string input, string expected)
    {
        Assert.Equal(expected, ModLocks.NormalizePath(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../a.txt")]
    [InlineData("a/../../b.txt")]
    [InlineData("C:/x.txt")]
    [InlineData("/common/a.txt")]
    public void NormalizePath_UngueltigeEingabe_WirftInvalidOperation(string input)
    {
        Assert.Throws<InvalidOperationException>(() => ModLocks.NormalizePath(input));
    }

    [Fact]
    public void LockedBy_DateiEintrag_SperrtNurDieseDatei()
    {
        var mod = new ModInfo { Name = "A", LockedPaths = { "common/a.txt" } };

        Assert.Equal("common/a.txt", ModLocks.LockedBy(mod, "common/a.txt"));
        Assert.Null(ModLocks.LockedBy(mod, "common/b.txt"));
    }

    [Fact]
    public void LockedBy_OrdnerEintrag_SperrtAllesDarunter_NichtGleichnamigePraefixe()
    {
        var mod = new ModInfo { Name = "A", LockedPaths = { "common" } };

        Assert.Equal("common", ModLocks.LockedBy(mod, "common/sub/a.txt"));
        Assert.Equal("common", ModLocks.LockedBy(mod, "common"));
        Assert.Null(ModLocks.LockedBy(mod, "common2/a.txt"));
        Assert.Null(ModLocks.LockedBy(mod, "gfx/common/a.txt"));
    }

    [Fact]
    public void LockedBy_IgnoriertGrossKleinschreibungUndBackslash()
    {
        var mod = new ModInfo { Name = "A", LockedPaths = { "Common/A.txt" } };

        Assert.Equal("Common/A.txt", ModLocks.LockedBy(mod, "common\\a.TXT"));
    }

    [Fact]
    public void IsLocked_BeruecksichtigtDieSperreDerGanzenMod()
    {
        var mod = new ModInfo { Name = "A" };
        Assert.False(ModLocks.IsLocked(mod, "a.txt"));

        mod.IsLocked = true;

        Assert.True(ModLocks.IsLocked(mod, "a.txt"));
        Assert.Null(ModLocks.LockedBy(mod, "a.txt"));
    }

    [Fact]
    public void SetPathLocked_FuegtHinzuUndEntferntUndSpeichert()
    {
        var registry = NewRegistry();
        var mod = registry.Add("A", Make("a"));

        registry.SetPathLocked(mod.Id, "Common\\a.txt", true);
        registry.SetPathLocked(mod.Id, "common/A.txt", true);

        var back = ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(mod.Id)!;
        Assert.Equal(new[] { "Common/a.txt" }, back.LockedPaths.ToArray());

        registry.SetPathLocked(mod.Id, "COMMON/A.TXT", false);

        Assert.Empty(ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(mod.Id)!.LockedPaths);
    }

    [Fact]
    public void SetPathLocked_EntsperrenEinerDateiUnterGesperrtemOrdner_AendertNichts()
    {
        var registry = NewRegistry();
        var mod = registry.Add("A", Make("a"));
        registry.SetPathLocked(mod.Id, "common", true);

        registry.SetPathLocked(mod.Id, "common/a.txt", false);

        Assert.Equal(new[] { "common" }, mod.LockedPaths.ToArray());
    }

    [Fact]
    public void SetPathLocked_UngueltigerPfad_WirftInvalidOperation()
    {
        var registry = NewRegistry();
        var mod = registry.Add("A", Make("a"));

        Assert.Throws<InvalidOperationException>(() => registry.SetPathLocked(mod.Id, "../x", true));
        Assert.Empty(mod.LockedPaths);
    }

    [Fact]
    public void Laden_BereinigtUngueltigeUndDoppelteEintraege()
    {
        var game = Make("game");
        File.WriteAllText(Path.Combine(game, ModRegistry.FileName), """
            {"version":1,"mods":[{"id":"a","name":"A","paths":[],"lockedPaths":["common\\a.txt","Common/A.txt","","../x","/rooted.txt","gfx/"," "]}],"ignoredPaths":[]}
            """);

        var mod = ModRegistry.Load(game).Get("a")!;

        Assert.Equal(new[] { "common/a.txt", "gfx" }, mod.LockedPaths.ToArray());
    }

    [Fact]
    public void Reason_UnterscheidetGanzeModEigenenEintragUndOrdnerDarueber()
    {
        var mod = new ModInfo { Name = "A", LockedPaths = { "common", "gfx/a.dds" } };

        Assert.Equal(new LockReason(LockReasonKind.ThisPath, "common"), ModLocks.Reason(mod, "common"));
        Assert.Equal(new LockReason(LockReasonKind.ParentFolder, "common"), ModLocks.Reason(mod, "common/x.txt"));
        Assert.Equal(new LockReason(LockReasonKind.ThisPath, "gfx/a.dds"), ModLocks.Reason(mod, "gfx/a.dds"));
        Assert.Null(ModLocks.Reason(mod, "gfx/b.dds"));
        Assert.Null(ModLocks.Reason(mod, string.Empty));

        mod.IsLocked = true;

        Assert.Equal(new LockReason(LockReasonKind.WholeMod, null), ModLocks.Reason(mod, "gfx/b.dds"));
        Assert.Equal(new LockReason(LockReasonKind.WholeMod, null), ModLocks.Reason(mod, string.Empty));
    }

    [Fact]
    public void Describe_NenntDenGrundUndDieWirkung()
    {
        var whole = ModLocks.Describe(new LockReason(LockReasonKind.WholeMod, null), isDirectory: true);
        var file = ModLocks.Describe(new LockReason(LockReasonKind.ThisPath, "a.txt"), isDirectory: false);
        var folder = ModLocks.Describe(new LockReason(LockReasonKind.ThisPath, "common"), isDirectory: true);
        var parent = ModLocks.Describe(new LockReason(LockReasonKind.ParentFolder, "common"), isDirectory: false);

        Assert.StartsWith("Die ganze Mod ist gesperrt.", whole);
        Assert.StartsWith("Diese Datei ist gesperrt.", file);
        Assert.StartsWith("Dieser Ordner ist gesperrt.", folder);
        Assert.StartsWith("Der Ordner common darüber ist gesperrt.", parent);
        Assert.All(new[] { whole, file, folder, parent }, text => Assert.Contains("Der Editor bleibt unberührt.", text));
    }

    [Fact]
    public void CountUnder_ZaehltEintraegeImOrdnerRekursiv_NichtGleichnamigePraefixe()
    {
        var mod = new ModInfo { Name = "A", LockedPaths = { "common", "common/sub/a.txt", "common2/b.txt", "gfx/c.dds" } };

        Assert.Equal(2, ModLocks.CountUnder(mod, "common"));
        Assert.Equal(1, ModLocks.CountUnder(mod, "common/sub"));
        Assert.Equal(1, ModLocks.CountUnder(mod, "gfx"));
        Assert.Equal(4, ModLocks.CountUnder(mod, string.Empty));
        Assert.Equal(0, ModLocks.CountUnder(mod, "events"));
    }

    [Fact]
    public void ClearPathLocksUnder_EntferntNurDieEintraegeUnterDemOrdner_UndSpeichert()
    {
        var registry = NewRegistry();
        var mod = registry.Add("A", Make("a"));
        foreach (var path in new[] { "common", "common/sub/a.txt", "common2/b.txt", "gfx/c.dds" })
        {
            registry.SetPathLocked(mod.Id, path, true);
        }

        var removed = registry.ClearPathLocksUnder(mod.Id, "common");

        Assert.Equal(2, removed);
        var back = ModRegistry.Load(Path.GetDirectoryName(registry.FilePath)!).Get(mod.Id)!;
        Assert.Equal(new[] { "common2/b.txt", "gfx/c.dds" }, back.LockedPaths.ToArray());
    }

    [Fact]
    public void ClearPathLocksUnder_LeererPfad_EntferntAllePfadSperren_NichtDieSperreDerGanzenMod()
    {
        var registry = NewRegistry();
        var mod = registry.Add("A", Make("a"));
        registry.SetPathLocked(mod.Id, "a.txt", true);
        registry.SetPathLocked(mod.Id, "b/c.txt", true);
        registry.SetLocked(mod.Id, true);

        Assert.Equal(2, registry.ClearPathLocksUnder(mod.Id, string.Empty));

        Assert.Empty(mod.LockedPaths);
        Assert.True(mod.IsLocked);
    }

    [Fact]
    public void ClearPathLocksUnder_NichtsZuEntfernen_LiefertNull()
    {
        var registry = NewRegistry();
        var mod = registry.Add("A", Make("a"));

        Assert.Equal(0, registry.ClearPathLocksUnder(mod.Id, "common"));
    }
}
