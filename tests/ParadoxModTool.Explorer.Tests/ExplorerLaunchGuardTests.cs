using Xunit;

namespace ParadoxModTool.Explorer.Tests;

/// <summary>
/// Der Windows-Explorer öffnet bei einem Pfad mit gemischten Trennzeichen (<c>…\Crusader Kings III/mod\Name</c>) still "Dokumente" (auch mit <c>/select,</c>).
/// Jeder Aufruf von <c>explorer.exe</c> im Quellcode muss den Pfad deshalb vorher mit <c>PathNormalization.ToWindowsPath</c> oder <c>Path.GetFullPath</c> bereinigen;
/// ein neuer Aufruf ohne Bereinigung fällt hier auf.
/// </summary>
public sealed class ExplorerLaunchGuardTests
{
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ParadoxModTool.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository-Wurzel nicht gefunden");
    }

    [Fact]
    public void Every_explorer_exe_call_cleans_the_path_first()
    {
        var separator = Path.DirectorySeparatorChar;
        var unguarded = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{separator}obj{separator}") || file.Contains($"{separator}bin{separator}"))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                if (!line.Contains("\"explorer.exe\"") || line.StartsWith("//") || line.StartsWith("///"))
                {
                    continue;
                }

                var window = string.Join('\n', lines.Skip(Math.Max(0, i - 20)).Take(21));
                if (!window.Contains("ToWindowsPath(") && !window.Contains("GetFullPath("))
                {
                    unguarded.Add($"{Path.GetFileName(file)}:{i + 1}");
                }
            }
        }

        Assert.Empty(unguarded);
    }
}
