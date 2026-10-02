using Xunit;

namespace ParadoxModTool.Modifiers.Tests;

public sealed class ScriptValueIndexTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-script-values-" + Guid.NewGuid().ToString("N"));

    private string Game => Path.Combine(_root, "game");

    private string Mod => Path.Combine(_root, "mod");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private ScriptValueIndex Build() => ScriptValueIndex.Build(new[] { Game }, new[] { Mod });

    [Fact]
    public void ReadsPlainNumbersAndNegativeNumbers()
    {
        Write(Game, "common/script_values/00.txt", "﻿# Kommentar\nsmall = 0.1\nmalus = -20\n");

        var index = Build();

        Assert.Equal(0.1m, index.Find("small"));
        Assert.Equal(-20m, index.Find("malus"));
    }

    [Fact]
    public void ReadsBlockWithOnlyValue() => Assert.Equal("5", ScriptValueIndex.Read("a = { value = 5 }")["a"]);

    [Fact]
    public void FormulasStayUnresolved()
    {
        Write(Game, "common/script_values/00.txt", "f = {\n\tvalue = 5\n\tadd = 3\n}\ng = { if = { limit = { always = yes } add = 1 } }\n");

        var index = Build();

        Assert.Null(index.Find("f"));
        Assert.Null(index.Find("g"));
    }

    [Fact]
    public void FollowsReferencesToOtherValuesAndConstants()
    {
        Write(Game, "common/script_values/00.txt", "@base = 0.25\nb = @base\nc = b\n");

        var index = Build();

        Assert.Equal(0.25m, index.Find("@base"));
        Assert.Equal(0.25m, index.Find("b"));
        Assert.Equal(0.25m, index.Find("c"));
    }

    [Fact]
    public void ReadsScriptedVariablesAndInGameFolder()
    {
        Write(Game, "common/scripted_variables/a.txt", "@x = 3\n");
        Write(Game, "in_game/common/script_values/b.txt", "y = 4\n");

        var index = Build();

        Assert.Equal(3m, index.Find("@x"));
        Assert.Equal(4m, index.Find("y"));
    }

    [Fact]
    public void CircularReferencesAndUnknownNamesGiveNothing()
    {
        Write(Game, "common/script_values/00.txt", "a = b\nb = a\nc = nirgends\n");

        var index = Build();

        Assert.Null(index.Find("a"));
        Assert.Null(index.Find("c"));
        Assert.Null(index.Find("unbekannt"));
    }

    [Fact]
    public void ModOverridesGameEvenWithAFormula()
    {
        Write(Game, "common/script_values/00.txt", "a = 1\nb = 2\nc = 3\n");
        Write(Mod, "common/script_values/zz.txt", "a = 10\nb = { value = 2 add = 1 }\n");

        var index = Build();

        Assert.Equal(10m, index.Find("a"));
        Assert.Null(index.Find("b"));
        Assert.Equal(3m, index.Find("c"));
    }

    [Fact]
    public void LocalConstantsOfTheOpenFileComeFirst()
    {
        Write(Game, "common/script_values/00.txt", "@price = 1\nother = 2\n");
        var local = ScriptValueIndex.Read("@price = 0.5\n@own = 3\nuses = { }\nrefs = @own\n");

        var index = Build();

        Assert.Equal(0.5m, index.Find("@price", local));
        Assert.Equal(3m, index.Find("@own", local));
        Assert.Equal(3m, index.Find("refs", local));
        Assert.Equal(2m, index.Find("other", local));
        Assert.Equal(1m, index.Find("@price"));
        Assert.Null(index.Find("@own"));
    }

    [Fact]
    public void LocalFormulaHidesTheGlobalValue()
    {
        Write(Game, "common/script_values/00.txt", "a = 1\n");
        var local = ScriptValueIndex.Read("a = { value = 1 add = 1 }");

        Assert.Null(Build().Find("a", local));
    }

    [Fact]
    public void EvaluatesInlineFormulasLikeTheRealCk3Chain()
    {
        Write(Game, "common/script_values/00_building_values.txt",
            "@normal_siege_value_mult_base = 0.10\n@normal_siege_value_mult_addition_per_tier = 0.05\n"
            + "@normal_maa_siege_value_tier_1 = @[normal_siege_value_mult_base]\n"
            + "@normal_maa_siege_value_tier_2 = @[normal_maa_siege_value_tier_1 + normal_siege_value_mult_addition_per_tier]\n"
            + "normal_maa_siege_value_tier_1 = @normal_maa_siege_value_tier_1\n"
            + "normal_maa_siege_value_tier_2 = @normal_maa_siege_value_tier_2\n");

        var index = Build();

        Assert.Equal(0.10m, index.Find("normal_maa_siege_value_tier_1"));
        Assert.Equal(0.15m, index.Find("normal_maa_siege_value_tier_2"));
    }

    [Fact]
    public void InlineFormulaWithUnknownNameStaysUnresolvedAndCircularOnesEnd()
    {
        Write(Game, "common/script_values/00.txt", "@a = @[ nirgends * 2 ]\n@b = @[ c ]\n@c = @[ b ]\n");

        var index = Build();

        Assert.Null(index.Find("@a"));
        Assert.Null(index.Find("@b"));
    }

    [Fact]
    public void MissingFoldersGiveEmptyIndex() => Assert.Equal(0, Build().Count);

    [Fact]
    public void SubfoldersAreRead()
    {
        Write(Game, "common/script_values/sub/deep.txt", "d = 7\n");

        Assert.Equal(7m, Build().Find("d"));
    }

    [Fact]
    public void UnclosedBlockGivesNoValue() => Assert.Null(ScriptValueIndex.Read("a = { value = 1 b = 2")["a"]);
}
