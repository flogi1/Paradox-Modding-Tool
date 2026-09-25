namespace ParadoxModTool.Mods.Tests;

/// <summary>Basis für Tests mit einem eigenen, wieder gelöschten Temp-Ordner.</summary>
public abstract class TempDirTests : IDisposable
{
    protected TempDirTests()
    {
        Directory.CreateDirectory(Dir);
    }

    protected string Dir { get; } = Path.Combine(Path.GetTempPath(), "pmt-mods-" + Guid.NewGuid().ToString("N"));

    /// <summary>Legt einen Unterordner an und gibt seinen Pfad zurück.</summary>
    protected string Make(params string[] parts)
    {
        var path = Path.Combine(new[] { Dir }.Concat(parts).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, recursive: true);
        }
    }
}
