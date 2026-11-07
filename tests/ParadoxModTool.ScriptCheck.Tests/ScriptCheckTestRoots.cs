using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ScriptCheck.Tests;

/// <summary>Temporäre Wurzeln für Tests: Vanilla (<c>v</c>), eine andere Playset-Mod (<c>o</c>, „Other Mod“) und die gewählte Mod (<c>m</c>, „My Mod“).</summary>
internal sealed class ScriptCheckTestRoots : IDisposable
{
    public static readonly string ProfilesDir = Path.Combine(AppContext.BaseDirectory, "profiles");

    public ScriptCheckTestRoots()
    {
        Temp = Path.Combine(Path.GetTempPath(), "pmt-scriptcheck-" + Guid.NewGuid().ToString("N"));
        Vanilla = Path.Combine(Temp, "v");
        Other = Path.Combine(Temp, "o");
        Mod = Path.Combine(Temp, "m");
        Directory.CreateDirectory(Vanilla);
        Directory.CreateDirectory(Other);
        Directory.CreateDirectory(Mod);
    }

    public string Temp { get; }

    public string Vanilla { get; }

    public string Other { get; }

    public string Mod { get; }

    public static ScriptCheckRules Rules(string game = "ck3") =>
        ScriptCheckRules.ForProfile(RepoFiles.Profile(game), ProfilesDir) ?? throw new InvalidOperationException("Keine Regeln für " + game);

    public static string Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Playset aus der anderen Mod (Ladereihenfolge 0) und, falls gewünscht, der gewählten Mod (1) über Vanilla.</summary>
    public FlagSourceSet Sources(string game = "ck3", bool withActiveMod = true)
    {
        var mods = new List<ModSource> { new("Other Mod", Other, false, 0) };
        if (withActiveMod)
        {
            mods.Add(new ModSource("My Mod", Mod, true, 1));
        }

        return FlagSourceSet.ForPlayset(RepoFiles.Profile(game), mods, withActiveMod ? Mod : string.Empty, new[] { Vanilla });
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Temp, true);
        }
        catch (IOException)
        {
        }
    }
}
