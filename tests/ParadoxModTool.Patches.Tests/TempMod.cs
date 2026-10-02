using System.Text;

namespace ParadoxModTool.Patches.Tests;

/// <summary>Temporärer Spielordner ("vanilla") und Mod-Ordner ("mod") für einen Test; wird beim Dispose gelöscht.</summary>
public sealed class TempMod : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-patches-" + Guid.NewGuid().ToString("N"));

    public TempMod()
    {
        Directory.CreateDirectory(VanillaRoot);
        Directory.CreateDirectory(ModRoot);
    }

    public string VanillaRoot => Path.Combine(_root, "vanilla");

    public string ModRoot => Path.Combine(_root, "mod");

    public void WriteVanilla(string relative, string text) => Write(VanillaRoot, relative, text, bom: true);

    public void WriteMod(string relative, string text, bool bom = true) => Write(ModRoot, relative, text, bom);

    public string ReadMod(string relative) => File.ReadAllText(ModPath(relative));

    public bool ModExists(string relative) => File.Exists(ModPath(relative));

    public string ModPath(string relative) => Path.Combine(ModRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static void Write(string root, string relative, string text, bool bom)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(bom));
    }
}
