namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>Findet Texturpfade wie <c>gfx/interface/x.dds</c> in Mod, Abhängigkeits-Mods und Vanilla und merkt sich ihre Größe.</summary>
public sealed class GuiTextureLocator
{
    private static readonly string[] Contexts = { "in_game", "main_menu", "loading_screen" };
    private readonly IReadOnlyList<string> _roots;
    private readonly Dictionary<string, (int Width, int Height)?> _sizes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public GuiTextureLocator(IEnumerable<string> contextRoots) => _roots = contextRoots.ToList();

    /// <summary>Kontextordner (…\in_game, …\main_menu, …\loading_screen) in Suchreihenfolge; Texturpfade sind relativ zu ihnen.</summary>
    public IReadOnlyList<string> Roots => _roots;

    /// <summary>Suchreihenfolge: aktuelle Mod, Abhängigkeits-Mods, dann game, jomini, clausewitz; darin je in_game, main_menu, loading_screen.</summary>
    public static IReadOnlyList<string> ContextRootsFor(string installDirectory, IEnumerable<string> dependencyMods, string? modRoot)
    {
        var bases = new List<string>();
        if (!string.IsNullOrEmpty(modRoot))
        {
            bases.Add(modRoot);
        }

        bases.AddRange(dependencyMods);
        bases.AddRange(new[] { "game", "jomini", "clausewitz" }.Select(folder => Path.Combine(installDirectory, folder)));
        return bases.SelectMany(b => Contexts.Select(c => Path.Combine(b, c))).Where(Directory.Exists).ToList();
    }

    public string? Locate(string texturePath)
    {
        var relative = texturePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        foreach (var root in _roots)
        {
            var full = Path.Combine(root, relative);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return null;
    }

    /// <summary>Größe einer Textur aus ihrem DDS-Header, gemerkt je Pfad. Parallel aufrufbar (Vorschau rechnet im Hintergrund).</summary>
    public (int Width, int Height)? SizeOf(string texturePath)
    {
        lock (_gate)
        {
            if (_sizes.TryGetValue(texturePath, out var cached))
            {
                return cached;
            }
        }

        (int Width, int Height)? size = Locate(texturePath) is { } full && DdsHeader.TryReadSize(full, out var width, out var height)
            ? (width, height)
            : null;
        lock (_gate)
        {
            _sizes[texturePath] = size;
        }

        return size;
    }
}
