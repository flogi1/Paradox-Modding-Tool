using System.Security.Cryptography;
using System.Text;

namespace ParadoxModTool.GuiDesign.Index;

public enum GuiLayer
{
    Vanilla = 0,
    Dependency = 1,
    Mod = 2,
}

/// <summary>Wurzelordner, aus dem .gui-Dateien gelesen werden: ein Vanilla-Ordner (clausewitz, jomini, game), eine Abhängigkeits-Mod oder die aktuelle Mod.</summary>
public sealed record GuiSourceRoot(string Directory, GuiLayer Layer, string Label);

/// <summary>Eine .gui-Datei. <c>RelativePath</c> ist relativ zur Wurzel mit "/" getrennt, z.B. <c>in_game/gui/x.gui</c>.</summary>
public sealed record GuiSourceFile(string FullPath, string RelativePath, GuiLayer Layer, string Label, long Size, long WriteTicks);

public static class GuiSourceSet
{
    /// <summary>Ladereihenfolge in Vanilla: Engine zuerst, Spiel zuletzt (Plan-Ruling 4).</summary>
    private static readonly string[] VanillaFolders = { "clausewitz", "jomini", "game" };

    public static IReadOnlyList<GuiSourceRoot> RootsFor(string installDirectory, IEnumerable<string> dependencyMods, string? modRoot)
    {
        var roots = VanillaFolders
            .Select(folder => new GuiSourceRoot(Path.Combine(installDirectory, folder), GuiLayer.Vanilla, folder))
            .ToList();
        roots.AddRange(dependencyMods.Select(dir => new GuiSourceRoot(dir, GuiLayer.Dependency, LabelOf(dir))));
        if (!string.IsNullOrEmpty(modRoot))
        {
            roots.Add(new GuiSourceRoot(modRoot, GuiLayer.Mod, LabelOf(modRoot)));
        }

        return roots;
    }

    /// <summary>
    /// Alle .gui-Dateien der Wurzeln in Ladereihenfolge (Wurzeln wie übergeben, darin alphabetisch). Ordner, deren Name mit "." beginnt,
    /// und in Mods der Ordner "snapshots" (PMT-Sicherungen) werden übersprungen.
    /// </summary>
    public static IReadOnlyList<GuiSourceFile> Collect(IEnumerable<GuiSourceRoot> roots)
    {
        var files = new List<GuiSourceFile>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root.Directory))
            {
                continue;
            }

            var found = new List<GuiSourceFile>();
            foreach (var path in Directory.EnumerateFiles(root.Directory, "*.gui", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root.Directory, path).Replace('\\', '/');
                var first = relative.Split('/')[0];
                if (first.StartsWith('.') || (root.Layer != GuiLayer.Vanilla && string.Equals(first, "snapshots", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var info = new FileInfo(path);
                found.Add(new GuiSourceFile(path, relative, root.Layer, root.Label, info.Length, info.LastWriteTimeUtc.Ticks));
            }

            files.AddRange(found.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase));
        }

        return files;
    }

    /// <summary>Fingerabdruck der Dateiliste: Ändert sich eine Datei (Größe, Zeit) oder kommt eine hinzu, ändert er sich.</summary>
    public static string ComputeFingerprint(IEnumerable<GuiSourceFile> files)
    {
        var lines = files
            .Select(f => $"{f.Layer}|{f.RelativePath}|{f.Size}|{f.WriteTicks}")
            .OrderBy(l => l, StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    private static string LabelOf(string directory) => Path.GetFileName(directory.TrimEnd('\\', '/'));
}
