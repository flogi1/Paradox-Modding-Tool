using System.Runtime.InteropServices;
using ParadoxModTool.Updates;
using Xunit;

namespace ParadoxModTool.Updates.Tests;

public class CommandLineTests
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int count);

    /// <summary>Zerlegt wie Windows beim Prozessstart; das erste Element (Programmname) fällt weg.</summary>
    private static string[] SplitLikeWindows(string arguments)
    {
        var pointer = CommandLineToArgvW("x.exe " + arguments, out var count);
        try
        {
            return Enumerable.Range(1, count - 1).Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, i * IntPtr.Size))!).ToArray();
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Fact]
    public void Join_uebersteht_Windows_Zerlegung()
    {
        string[] args = [@"C:\Program Files\PMT Ä\", "", "a\"b", @"C:\ohne", @"ende\\", "x y\\\"z"];
        Assert.Equal(args, SplitLikeWindows(CommandLine.Join(args)));
    }

    [Fact]
    public void Apply_Argumente_Rundlauf()
    {
        var original = new ApplyUpdateArguments(1, @"C:\Program Files\PMT Ä", 4711, new AppVersion(1, 1, 0), new AppVersion(1, 2, 0), "eu4", "en");
        var parsed = ApplyUpdateArguments.Parse(SplitLikeWindows(CommandLine.Join(original.ToArguments())));
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void Apply_Argumente_ignorieren_Unbekanntes_und_normalisieren_das_Ziel()
    {
        var parsed = ApplyUpdateArguments.Parse([
            "--apply-update", "--neu-in-v7", "wert", "--protocol", "1", "--flag", "--target", @"C:\PMT\",
            "--wait-pid", "12", "--from-version", "1.0.0", "--to-version", "1.1.0"]);

        Assert.NotNull(parsed);
        Assert.Equal(@"C:\PMT", parsed.Target);
        Assert.Null(parsed.GameId);
        Assert.Null(parsed.Language);
    }

    [Theory]
    [InlineData("--protocol", "0")]
    [InlineData("--target", "relativ")]
    [InlineData("--wait-pid", "x")]
    [InlineData("--to-version", "neu")]
    public void Apply_Argumente_lehnen_Ungueltiges_ab(string name, string value)
    {
        var args = new Dictionary<string, string>
        {
            ["--protocol"] = "1", ["--target"] = @"C:\PMT", ["--wait-pid"] = "12", ["--from-version"] = "1.0.0", ["--to-version"] = "1.1.0",
        };
        args[name] = value;
        var list = new List<string> { ApplyUpdateArguments.Switch };
        foreach (var (key, text) in args)
        {
            list.Add(key);
            list.Add(text);
        }

        Assert.Null(ApplyUpdateArguments.Parse(list));
        Assert.Null(ApplyUpdateArguments.Parse(["--anders", "--protocol", "1"]));
    }
    [Fact]
    public void Start_Argumente_fuer_die_normale_App()
    {
        var to = new AppVersion(1, 2, 0);
        var from = new AppVersion(1, 1, 0);

        var applied = UpdateStartArguments.ForApplied("eu4", to, from);
        Assert.Equal(new[] { "--game", "eu4", "--update-applied", "1.2.0", "--update-from", "1.1.0" }, applied);
        Assert.Equal(new UpdateStartInfo(to, from, null, null), UpdateStartArguments.Parse(applied));
        Assert.Equal(new UpdateStartInfo(null, null, to, null), UpdateStartArguments.Parse(UpdateStartArguments.ForFailed(null, to)));
        Assert.Equal(new UpdateStartInfo(null, null, null, to), UpdateStartArguments.Parse(UpdateStartArguments.ForCancelled("eu4", to)));

        var none = UpdateStartArguments.Parse(["--game", "eu4"]);
        Assert.False(none.IsUpdateStart);
        Assert.True(UpdateStartArguments.Parse(applied).IsUpdateStart);
    }

    [Fact]
    public void Update_Argumente_sind_fuer_den_Editor_Aufruf_unsichtbar()
    {
        var applied = UpdateStartArguments.ForApplied("eu4", new AppVersion(1, 2, 0), new AppVersion(1, 1, 0));
        Assert.Equal(new[] { "--game", "eu4" }, UpdateStartArguments.WithoutUpdateArguments(applied));

        string[] mixed = ["--update-failed", "1.2.0", "--game", "eu4", @"C:\mod\x.txt", "--update-cancelled"];
        Assert.Equal(new[] { "--game", "eu4", @"C:\mod\x.txt" }, UpdateStartArguments.WithoutUpdateArguments(mixed));
    }
}
