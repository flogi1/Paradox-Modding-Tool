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
    /// Wurzeln eines klassischen Spiels (HOI IV, EU IV): die Vanilla-Ordner laut Profil in der genannten Reihenfolge (<c>""</c> = die Installation selbst,
    /// ein <c>/*</c> am Ende = jeder vorhandene direkte Unterordner in Namensreihenfolge), dann Abhängigkeits-Mods, zuletzt die Mod. Das Etikett eines
    /// Vanilla-Ordners ist sein Ordnername, für die Installation selbst <c>vanilla</c>.
    /// </summary>
    public static IReadOnlyList<GuiSourceRoot> ClassicRootsFor(
        string installDirectory, IReadOnlyList<string> vanillaRoots, IEnumerable<string> dependencyMods, string? modRoot)
    {
        var roots = new List<GuiSourceRoot>();
        foreach (var entry in vanillaRoots)
        {
            var spec = entry.Replace('\\', '/').Trim('/');
            if (spec.EndsWith('*'))
            {
                var parent = Path.Combine(installDirectory, spec.TrimEnd('*').Trim('/').Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(parent))
                {
                    continue;
                }

                foreach (var sub in Directory.GetDirectories(parent).OrderBy(d => Path.GetFileName(d), StringComparer.Ordinal))
                {
                    roots.Add(new GuiSourceRoot(sub, GuiLayer.Vanilla, LabelOf(sub)));
                }
            }
            else
            {
                var directory = spec.Length == 0 ? installDirectory : Path.Combine(installDirectory, spec.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(directory))
                {
                    roots.Add(new GuiSourceRoot(directory, GuiLayer.Vanilla, spec.Length == 0 ? "vanilla" : LabelOf(directory)));
                }
            }
        }

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
    public static IReadOnlyList<GuiSourceFile> Collect(IEnumerable<GuiSourceRoot> roots) => Collect(roots, ".gui", null);

    /// <summary>
    /// Wie <see cref="Collect(IEnumerable{GuiSourceRoot})"/> für eine beliebige Endung, mit <paramref name="underFolder"/> nur unterhalb von
    /// <c>&lt;Wurzel&gt;/&lt;underFolder&gt;/</c> (klassisch: <c>interface</c>). <c>RelativePath</c> bleibt relativ zur Wurzel (<c>interface/x.gui</c>).
    /// </summary>
    public static IReadOnlyList<GuiSourceFile> Collect(IEnumerable<GuiSourceRoot> roots, string extension, string? underFolder)
    {
        var files = new List<GuiSourceFile>();
        foreach (var root in roots)
        {
            var start = string.IsNullOrEmpty(underFolder) ? root.Directory : Path.Combine(root.Directory, underFolder);
            if (!Directory.Exists(start))
            {
                continue;
            }

            var found = new List<GuiSourceFile>();
            foreach (var path in Directory.EnumerateFiles(start, "*" + extension, SearchOption.AllDirectories))
            {
                if (!path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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

    /// <summary>
    /// Neue Liste, in der der Eintrag zur selben Datei (gleicher voller Pfad) an seiner Stelle ersetzt ist, damit die Ladereihenfolge gleicher Pfade
    /// erhalten bleibt; eine neue Datei kommt hinter den letzten Eintrag ihrer oder einer niedrigeren Ebene.
    /// </summary>
    public static List<T> ReplaceOrInsert<T>(IReadOnlyList<T> items, T item, Func<T, GuiSourceFile> fileOf)
    {
        var result = items.ToList();
        var file = fileOf(item);
        var at = result.FindIndex(e => string.Equals(fileOf(e).FullPath, file.FullPath, StringComparison.OrdinalIgnoreCase));
        if (at >= 0)
        {
            result[at] = item;
        }
        else
        {
            result.Insert(result.FindLastIndex(e => fileOf(e).Layer <= file.Layer) + 1, item);
        }

        return result;
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
