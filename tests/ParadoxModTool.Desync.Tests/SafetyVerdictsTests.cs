using Xunit;

namespace ParadoxModTool.Desync.Tests;

public sealed class SafetyVerdictsTests
{
    [Fact]
    public void Missing_file_is_empty()
    {
        using var tree = new TempTree();
        var verdicts = new SafetyVerdicts(tree.PathOf("desync/safety-verdicts.json"));

        Assert.True(verdicts.Load());
        Assert.Null(verdicts.Get("console_other", "add_gold"));
    }

    [Fact]
    public void Round_trip()
    {
        using var tree = new TempTree();
        var path = tree.PathOf("desync/safety-verdicts.json");
        var verdicts = new SafetyVerdicts(path);
        verdicts.Set("console_other", "add_gold", SafetyVerdict.Harmless);
        verdicts.Set("script_key", "random_list", SafetyVerdict.Risky);
        verdicts.Save();

        var loaded = new SafetyVerdicts(path);
        Assert.True(loaded.Load());
        Assert.Equal(SafetyVerdict.Harmless, loaded.Get("console_other", "add_gold"));
        Assert.Equal(SafetyVerdict.Harmless, loaded.Get("Console_Other", "ADD_GOLD"));
        Assert.Equal(SafetyVerdict.Risky, loaded.Get("script_key", "random_list"));
        Assert.Null(loaded.Get("script_key", "other"));

        var text = File.ReadAllText(path);
        Assert.Contains("\"version\": 1", text);
        Assert.Contains("\"console_other|add_gold\": \"harmless\"", text);
        Assert.Contains("\"script_key|random_list\": \"risky\"", text);
        Assert.True(text.IndexOf("console_other|add_gold", StringComparison.Ordinal) < text.IndexOf("script_key|random_list", StringComparison.Ordinal));
    }

    [Fact]
    public void Apply_overrides_both_ways()
    {
        using var tree = new TempTree();
        var verdicts = new SafetyVerdicts(tree.PathOf("v.json"));
        verdicts.Set("a", "x", SafetyVerdict.Harmless);
        verdicts.Set("a", "y", SafetyVerdict.Risky);

        Assert.Equal(SafetySeverity.Allowed, verdicts.Apply("a", "x", SafetySeverity.Risk));
        Assert.Equal(SafetySeverity.Risk, verdicts.Apply("a", "y", SafetySeverity.Allowed));
        Assert.Equal(SafetySeverity.Check, verdicts.Apply("a", "z", SafetySeverity.Check));
    }

    [Fact]
    public void Set_null_resets()
    {
        using var tree = new TempTree();
        var path = tree.PathOf("v.json");
        var verdicts = new SafetyVerdicts(path);
        verdicts.Set("a", "x", SafetyVerdict.Harmless);
        verdicts.Save();

        verdicts.Set("A", "X", null);
        Assert.Null(verdicts.Get("a", "x"));
        verdicts.Save();

        var loaded = new SafetyVerdicts(path);
        Assert.True(loaded.Load());
        Assert.Null(loaded.Get("a", "x"));
    }

    [Fact]
    public void Broken_file_kept_as_bak()
    {
        using var tree = new TempTree(("v.json", "{nope"));
        var path = tree.PathOf("v.json");
        var verdicts = new SafetyVerdicts(path);
        verdicts.Set("a", "x", SafetyVerdict.Risky);

        Assert.False(verdicts.Load());
        Assert.Equal("{nope", File.ReadAllText(path + ".bak"));
        Assert.False(File.Exists(path));
        Assert.Null(verdicts.Get("a", "x"));
    }

    [Fact]
    public void Broken_file_overwrites_older_bak()
    {
        using var tree = new TempTree(("v.json", "[1]"), ("v.json.bak", "old"));
        var path = tree.PathOf("v.json");
        var verdicts = new SafetyVerdicts(path);

        Assert.False(verdicts.Load());
        Assert.Equal("[1]", File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public void Unknown_verdict_values_are_skipped()
    {
        using var tree = new TempTree(("v.json", "{ \"version\": 1, \"verdicts\": { \"a|x\": \"maybe\", \"a|y\": \"harmless\", \"a|z\": 5 } }"));
        var verdicts = new SafetyVerdicts(tree.PathOf("v.json"));

        Assert.True(verdicts.Load());
        Assert.Null(verdicts.Get("a", "x"));
        Assert.Equal(SafetyVerdict.Harmless, verdicts.Get("a", "y"));
        Assert.Null(verdicts.Get("a", "z"));
    }

    [Fact]
    public void Save_creates_directory_and_leaves_no_temp_files()
    {
        using var tree = new TempTree();
        var path = tree.PathOf("deep/er/safety-verdicts.json");
        var verdicts = new SafetyVerdicts(path);
        verdicts.Set("a", "x", SafetyVerdict.Harmless);

        verdicts.Save();

        Assert.True(File.Exists(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(Path.GetDirectoryName(path)!));
    }
}
