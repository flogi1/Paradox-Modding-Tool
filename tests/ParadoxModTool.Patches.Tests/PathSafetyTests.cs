using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class PathSafetyTests
{
    [Theory]
    [InlineData("common/a.txt")]
    [InlineData("common\\a.txt")]
    [InlineData("common/*.txt")]
    [InlineData("common/...txt")]
    public void Safe_relative_paths(string path) => Assert.True(PathSafety.IsSafeRelative(path));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    [InlineData("../a.txt")]
    [InlineData("common/../../a.txt")]
    [InlineData("common\\..\\a.txt")]
    [InlineData("C:\\a.txt")]
    [InlineData("/a.txt")]
    [InlineData("\\a.txt")]
    public void Unsafe_relative_paths(string? path) => Assert.False(PathSafety.IsSafeRelative(path));

    [Fact]
    public void Glob_checks_every_part()
    {
        Assert.True(PathSafety.IsSafeGlob("a/*.txt; b/*.txt"));
        Assert.False(PathSafety.IsSafeGlob("a/*.txt;../b.txt"));
        Assert.False(PathSafety.IsSafeGlob("a/*.txt; /b.txt"));
        Assert.False(PathSafety.IsSafeGlob(null));
    }

    [Fact]
    public void Glob_ignores_empty_parts()
    {
        // Ein leerer Teil (z. B. ein ";" am Ende) liest nichts und macht das Muster nicht unsicher; ohne jeden Teil fehlt das Muster.
        Assert.True(PathSafety.IsSafeGlob("a/*.txt;"));
        Assert.True(PathSafety.IsSafeGlob("a/*.txt; ;b/*.txt"));
        Assert.False(PathSafety.IsSafeGlob("a/*.txt;;../b.txt"));
        Assert.False(PathSafety.IsSafeGlob(";"));
        Assert.False(PathSafety.IsSafeGlob(" ; "));
        Assert.False(PathSafety.IsSafeGlob(string.Empty));
        Assert.Empty(RecipePaths.UnsafePaths(new LineRule { Id = "l", Files = "common/*.txt;" }));
        Assert.Empty(RecipePaths.UnsafePaths(new LineRule { Id = "l", Files = " ; " }));
        Assert.Equal(new[] { ";../a.txt" }, RecipePaths.UnsafePaths(new LineRule { Id = "l", Files = ";../a.txt" }));
    }

    [Fact]
    public void IsInside_needs_the_path_below_the_root()
    {
        using var mod = new TempMod();
        Assert.True(PathSafety.IsInside(mod.ModRoot, "common/a.txt"));
        Assert.True(PathSafety.IsInside(mod.ModRoot + Path.DirectorySeparatorChar, "common/a.txt"));
        Assert.False(PathSafety.IsInside(mod.ModRoot, "../x.txt"));
        Assert.False(PathSafety.IsInside(mod.ModRoot, "../mod2/x.txt"));
        Assert.False(PathSafety.IsInside(mod.ModRoot, "."));
    }

    [Fact]
    public void Glob_enumeration_ignores_parent_patterns()
    {
        using var mod = new TempMod();
        var parent = Directory.GetParent(mod.ModRoot)!.FullName;
        File.WriteAllText(Path.Combine(parent, "bait.txt"), "x");
        mod.WriteMod("inside.txt", "x");

        Assert.Empty(GlobMatcher.Enumerate(mod.ModRoot, "../*.txt"));
        Assert.Equal(new[] { "inside.txt" }, GlobMatcher.Enumerate(mod.ModRoot, "../*.txt; *.txt"));
    }

    [Fact]
    public void Workspace_refuses_paths_outside_the_roots()
    {
        using var mod = new TempMod();
        var workspace = new PatchWorkspace(mod.ModRoot, mod.VanillaRoot);

        Assert.Throws<UnsafePathException>(() => workspace.Get("../outside.txt"));
        Assert.Throws<UnsafePathException>(() => workspace.Set("a/../../outside.txt", "x"));
        Assert.Throws<UnsafePathException>(() => workspace.ReadVanilla("..\\outside.txt"));
        Assert.Throws<UnsafePathException>(() => workspace.CreateFromVanilla("C:/x.txt"));
        Assert.Null(workspace.Get("/inside.txt"));
    }

    [Fact]
    public void Rule_paths_are_collected_raw()
    {
        Assert.Empty(RecipePaths.UnsafePaths(new LineRule { Id = "l", Files = "a/*.txt" }));
        Assert.Equal(new[] { "../a.txt" }, RecipePaths.UnsafePaths(new LineRule { Id = "l", Files = "../a.txt" }));
        Assert.Equal(new[] { "/b.txt" }, RecipePaths.UnsafePaths(new BlockCopyRule { Id = "c", SourceFiles = "a/*.txt", Target = "/b.txt" }));
        Assert.Equal(new[] { "..\\a" }, RecipePaths.UnsafePaths(new TemplateRule { Id = "t", Target = "..\\a" }));
        Assert.Equal(new[] { "C:/a" }, RecipePaths.UnsafePaths(new ListEntryRule { Id = "e", Target = "C:/a" }));
        Assert.Equal(new[] { "x/../../a" }, RecipePaths.UnsafePaths(new BlockEditRule { Id = "b", Files = "x/../../a" }));
    }

    [Fact]
    public void Empty_paths_are_missing_not_unsafe()
    {
        Assert.Empty(RecipePaths.UnsafePaths(new LineRule { Id = "l", Files = string.Empty }));
        Assert.Empty(RecipePaths.UnsafePaths(new BlockEditRule { Id = "b", Files = null! }));
        Assert.Empty(RecipePaths.UnsafeLookups(new PatchRecipe { Columns = { new PatchColumn { Name = "c", Lookup = new DerivedLookup { Files = " " } } } }));
    }

    [Theory]
    [InlineData("common/x.txt")]
    [InlineData("common\\sub dir\\x_l_english.yml")]
    [InlineData("x.txt")]
    [InlineData("")]
    [InlineData(null)]
    public void Single_file_targets_pass(string? target) => Assert.False(RecipePaths.IsNotSingleFile(target));

    [Theory]
    [InlineData("x.txt;")]
    [InlineData("a.txt;b.txt")]
    [InlineData("*.txt")]
    [InlineData("a?.txt")]
    [InlineData("a/x.txt ")]
    [InlineData(" a/x.txt")]
    [InlineData("a /x.txt")]
    [InlineData("a/x.")]
    [InlineData("a//x.txt")]
    [InlineData("a/")]
    [InlineData("a\\")]
    [InlineData("a/\"x\".txt")]
    [InlineData("a/<x>.txt")]
    [InlineData("a/x|y.txt")]
    [InlineData("a/x\ty.txt")]
    public void Targets_that_are_no_single_file_are_found(string target) => Assert.True(RecipePaths.IsNotSingleFile(target));

    [Fact]
    public void Target_is_read_from_the_rule_kinds_that_write_one()
    {
        Assert.Equal("t.txt", RecipePaths.TargetOf(new BlockCopyRule { Id = "c", Target = "t.txt" }));
        Assert.Equal("t.txt", RecipePaths.TargetOf(new TemplateRule { Id = "t", Target = "t.txt" }));
        Assert.Equal("t.txt", RecipePaths.TargetOf(new ListEntryRule { Id = "e", Target = "t.txt" }));
        Assert.Null(RecipePaths.TargetOf(new LineRule { Id = "l", Files = "t.txt" }));
        Assert.Null(RecipePaths.TargetOf(new BlockEditRule { Id = "b", Files = "t.txt" }));
    }

    [Fact]
    public void Unsafe_lookups_are_listed_with_column()
    {
        var recipe = new PatchRecipe
        {
            Columns =
            {
                new PatchColumn { Name = "own" },
                new PatchColumn { Name = "good", Lookup = new DerivedLookup { Files = "history/*.txt", Match = "a = {key}" } },
                new PatchColumn { Name = "bad", Lookup = new DerivedLookup { Files = "../*.txt", Match = "a = {key}" } },
            },
        };

        Assert.Equal(new[] { ("bad", "../*.txt") }, RecipePaths.UnsafeLookups(recipe));
    }
}
