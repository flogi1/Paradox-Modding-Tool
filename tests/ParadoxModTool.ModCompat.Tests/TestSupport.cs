namespace ParadoxModTool.ModCompat.Tests;

/// <summary>Basis für Tests mit einem eigenen, wieder gelöschten Temp-Ordner.</summary>
public abstract class TempDirTests : IDisposable
{
    protected TempDirTests()
    {
        Directory.CreateDirectory(Dir);
    }

    protected string Dir { get; } = Path.Combine(Path.GetTempPath(), "pmt-compat-" + Guid.NewGuid().ToString("N"));

    /// <summary>Legt einen Unterordner an und gibt seinen Pfad zurück.</summary>
    protected string Make(params string[] parts)
    {
        var path = Path.Combine(new[] { Dir }.Concat(parts).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Schreibt eine Datei unter <see cref="Dir"/> (Ordner werden angelegt) und gibt den vollen Pfad zurück.</summary>
    protected string Write(string relativePath, string content)
    {
        var path = Path.Combine(Dir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Sucht eine Datei des Repositorys (z. B. <c>profiles/eu5.json</c>), indem vom Testordner aus nach oben gegangen wird.</summary>
    public static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Nicht gefunden: " + relative);
    }

    public void Dispose()
    {
        if (Directory.Exists(Dir))
        {
            Directory.Delete(Dir, recursive: true);
        }
    }
}
