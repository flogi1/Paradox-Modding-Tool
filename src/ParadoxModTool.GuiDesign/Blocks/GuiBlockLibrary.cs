using System.Globalization;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.Storage;

namespace ParadoxModTool.GuiDesign.Blocks;

/// <summary>
/// Bausteine aus drei Ebenen eines Spiels: mitgeliefert (<c>profiles\guiblocks\&lt;spiel-id&gt;</c>), eigene für das Spiel und eigene für die aktive Mod.
/// Bei gleichem Namen gilt Mod vor Spiel vor mitgeliefert; dazu kommt immer der eingebaute Baustein "Leere Datei" (Plan-Ruling 1). Die Bibliothek ist
/// ein Stand beim Laden; nach Speichern oder Löschen liefert <see cref="Reload"/> den neuen.
/// </summary>
public sealed class GuiBlockLibrary
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly StringComparer SortComparer = StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase: true);

    private readonly string? _shipped;
    private readonly string? _game;
    private readonly string? _mod;
    private readonly List<GuiBlock> _all = new();
    private readonly Dictionary<GuiBlock, List<GuiBlock>> _hidden = new();

    public GuiBlockLibrary(string? shippedDirectory, string? gameDirectory, string? modDirectory)
    {
        _shipped = shippedDirectory;
        _game = gameDirectory;
        _mod = modDirectory;
        _all.AddRange(Load(_mod, GuiBlockLayer.Mod));
        _all.AddRange(Load(_game, GuiBlockLayer.Game));
        _all.AddRange(Load(_shipped, GuiBlockLayer.Shipped));
        _all.Add(EmptyFile());

        var effective = new List<GuiBlock>();
        foreach (var group in _all.GroupBy(b => b.Name, NameComparer))
        {
            var winner = group.First();
            effective.Add(winner);
            _hidden[winner] = group.Skip(1).ToList();
        }

        Effective = effective
            .OrderBy(b => GroupRank(b.Group))
            .ThenBy(b => b.Group, SortComparer)
            .ThenBy(b => b.Name, SortComparer)
            .ToList();
    }

    /// <summary>Gruppen der Grundsammlung in ihrer Reihenfolge; andere Gruppen folgen alphabetisch.</summary>
    public static IReadOnlyList<string> StandardGroups { get; } = new[] { "Fenster", "Bedienelemente", "Anzeige" };

    /// <summary>Je Name der wirksame Baustein, auch fehlerhafte, sortiert nach Gruppe und Name.</summary>
    public IReadOnlyList<GuiBlock> Effective { get; }

    /// <summary>Gleichnamige Bausteine, die <paramref name="block"/> verdeckt (höhere Ebene zuerst).</summary>
    public IReadOnlyList<GuiBlock> HiddenBy(GuiBlock block) => _hidden.TryGetValue(block, out var hidden) ? hidden : Array.Empty<GuiBlock>();

    public GuiBlock? Find(string name) => Effective.FirstOrDefault(b => NameComparer.Equals(b.Name, name));

    public string? DirectoryOf(GuiBlockLayer layer) => layer switch
    {
        GuiBlockLayer.Shipped => _shipped,
        GuiBlockLayer.Game => _game,
        _ => _mod,
    };

    public bool Exists(string name, GuiBlockLayer layer) =>
        _all.Any(b => b.Layer == layer && !b.IsBuiltIn && NameComparer.Equals(b.Name, name));

    /// <summary>
    /// Schreibt einen eigenen Baustein atomar. Gibt es in der Ebene schon eine Datei mit diesem Bausteinnamen, wird sie überschrieben, sonst entsteht
    /// <see cref="GuiBlockNames.FileNameFor"/>. Wirft für die mitgelieferte Ebene und wenn die Ebene keinen Ordner hat (keine aktive Mod).
    /// </summary>
    public string Save(string text, string name, GuiBlockLayer layer)
    {
        var directory = WritableDirectory(layer);
        var path = _all.FirstOrDefault(b => b.Layer == layer && !b.IsBuiltIn && NameComparer.Equals(b.Name, name))?.SourcePath
            ?? Path.Combine(directory, GuiBlockNames.FileNameFor(name));
        Directory.CreateDirectory(directory);
        SharedFile.WriteAllTextAtomic(path, text);
        return path;
    }

    public void Delete(GuiBlock block)
    {
        if (block.IsBuiltIn || block.Layer == GuiBlockLayer.Shipped)
        {
            throw new InvalidOperationException("Shipped and built-in blocks cannot be deleted.");
        }

        File.Delete(block.SourcePath!);
    }

    /// <summary>"Als eigene Kopie bearbeiten": Text des Bausteins unter gleichem Namen in eine eigene Ebene.</summary>
    public string CopyToLayer(GuiBlock block, GuiBlockLayer layer)
    {
        var text = block.IsBuiltIn ? $"# block: {block.Name}\n# group: {block.Group}\n" : File.ReadAllText(block.SourcePath!);
        return Save(text, block.Name, layer);
    }

    public GuiBlockLibrary Reload() => new(_shipped, _game, _mod);

    /// <summary>Namen aus <c>requires</c>, die im Index weder als <c>type</c> noch als <c>template</c> vorkommen; ohne Index keine (Plan-Ruling 9).</summary>
    public static IReadOnlyList<string> MissingRequirements(GuiBlock block, GuiIndex? index) =>
        index is null
            ? Array.Empty<string>()
            : block.Requires
                .Where(name => index.Find(name, GuiDefinitionKind.Type) is null && index.Find(name, GuiDefinitionKind.Template) is null)
                .ToList();

    private string WritableDirectory(GuiBlockLayer layer)
    {
        if (layer == GuiBlockLayer.Shipped)
        {
            throw new InvalidOperationException("Shipped blocks are read-only.");
        }

        return DirectoryOf(layer) ?? throw new InvalidOperationException($"No folder for layer {layer}.");
    }

    private static int GroupRank(string group)
    {
        for (var i = 0; i < StandardGroups.Count; i++)
        {
            if (NameComparer.Equals(StandardGroups[i], group))
            {
                return i;
            }
        }

        return StandardGroups.Count;
    }

    /// <summary>Liest alle <c>*.guiblock</c> eines Ordners (nicht rekursiv, nach Dateiname); ein doppelter Name in derselben Ebene ist ein Fehler.</summary>
    private static List<GuiBlock> Load(string? directory, GuiBlockLayer layer)
    {
        var blocks = new List<GuiBlock>();
        if (directory is null)
        {
            return blocks;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*" + GuiBlockNames.Extension).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
                : Array.Empty<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return blocks;
        }

        var names = new HashSet<string>(NameComparer);
        foreach (var file in files)
        {
            var block = Read(file, layer);
            if (!names.Add(block.Name))
            {
                block = block.WithErrors(new[] { new GuiBlockError(0, GuiBlockErrorKind.DuplicateName, block.Name) });
            }

            blocks.Add(block);
        }

        return blocks;
    }

    private static GuiBlock Read(string path, GuiBlockLayer layer)
    {
        try
        {
            return GuiBlockParser.Parse(File.ReadAllText(path), layer, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new GuiBlock(
                Path.GetFileNameWithoutExtension(path),
                GuiTexts.BlocksDefaultGroup,
                string.Empty,
                Array.Empty<GuiBlockParameter>(),
                Array.Empty<GuiBlockRepeat>(),
                Array.Empty<string>(),
                Array.Empty<GuiBlockPart>(),
                layer,
                path,
                new[] { new GuiBlockError(0, GuiBlockErrorKind.Unreadable, ex.Message) });
        }
    }

    private static GuiBlock EmptyFile() => new(
        GuiTexts.BlocksEmptyFileName,
        StandardGroups[0],
        GuiTexts.BlocksEmptyFileDescription,
        Array.Empty<GuiBlockParameter>(),
        Array.Empty<GuiBlockRepeat>(),
        Array.Empty<string>(),
        Array.Empty<GuiBlockPart>(),
        GuiBlockLayer.Shipped,
        null,
        Array.Empty<GuiBlockError>());
}
