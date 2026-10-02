using Xunit;

namespace ParadoxModTool.Scopes.Tests;

public sealed class ScriptValueNamesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pmt-script-values-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Read_CollectsTopLevelNamesFromCommonAndInGame_SortedWithoutConstantsAndDuplicates()
    {
        Write("common/script_values/a.txt", "@c = 5\nzeta = { value = 1 }\nalpha = 2\n");
        Write("in_game/common/script_values/sub/b.txt", "alpha = 3\nbeta = { add = 1 }\n");
        Write("in_game/common/script_values/readme.txt", "ignored = 1\n");
        Write("in_game/common/scripted_triggers/t.txt", "not_a_value = { always = yes }\n");

        Assert.Equal(new[] { "alpha", "beta", "zeta" }, ScriptValueNames.Read(new[] { _root }));
    }

    [Fact]
    public void FromFiles_SkipsBrokenAndMissingFiles()
    {
        var good = Write("common/script_values/good.txt", "good_value = 1\n");
        var broken = Write("common/script_values/broken.txt", "half = { value = 1\nsecond = 2\n");

        var names = ScriptValueNames.FromFiles(new[] { good, broken, Path.Combine(_root, "missing.txt") });

        Assert.Contains("good_value", names);
        Assert.Contains("half", names);
    }

    [Fact]
    public void Read_WithoutFolder_IsEmpty()
    {
        Assert.Empty(ScriptValueNames.Read(new[] { Path.Combine(_root, "nothing") }));
    }
}
