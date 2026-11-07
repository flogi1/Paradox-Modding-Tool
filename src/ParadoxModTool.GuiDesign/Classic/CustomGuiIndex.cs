using System.Text.Json;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>Art eines Eintrags in <c>common/custom_gui</c> (EU IV): <c>custom_button</c>, <c>custom_text_box</c>, <c>custom_icon</c>, <c>custom_shield</c>, <c>custom_window</c>.</summary>
public enum CustomGuiKind
{
    Button,
    TextBox,
    Icon,
    Shield,
    Window,
}

/// <summary>
/// Ein Eintrag in <c>common/custom_gui/*.txt</c>: Art, Wert von <c>name</c> (das Element mit <c>scripted = yes</c> in einer <c>.gui</c>-Datei),
/// Datei und Zeile des Blocks. <paramref name="IsMod"/>: der Eintrag kommt aus der Mod selbst.
/// </summary>
public sealed record CustomGuiEntry(CustomGuiKind Kind, string Name, string FilePath, int Line, bool IsMod);

/// <summary>
/// Ein Fenster aus der Liste in <c>example.txt</c>, in dem EU IV Custom GUI liest: der Name wie in der Doku (<paramref name="DocName"/>, auch mit dem
/// Tippfehler <c>countrydiplimacyview</c>), die Vanilla-Datei (Dateiname ohne Ordner), ob alle obersten Fenster der Datei gemeint sind (<c>faction.gui</c>),
/// ROOT und FROM als Text in der gewählten Sprache.
/// </summary>
public sealed record SupportedWindowSpec(string DocName, string File, bool AllWindows, string Root, string From);

/// <summary>
/// Ein aufgelöstes unterstütztes Fenster: <paramref name="WindowName"/> ist der echte Name in der Datei, <paramref name="TopLevelWindow"/> das oberste
/// Fenster, in dem es steht (gleich <paramref name="WindowName"/>, wenn es selbst oberstes Fenster ist; <c>province_window</c> steht in
/// <c>provinceview</c>).
/// </summary>
public sealed record SupportedWindow(string DocName, string WindowName, string File, string Root, string From, string TopLevelWindow);

/// <summary>
/// Custom GUI von EU IV aus <c>common/custom_gui/*.txt</c> (Vanilla, Abhängigkeiten, Mod). Gelesen mit dem fehlertoleranten <see cref="GuiParser"/>; die
/// Blocknamen zählen ohne Rücksicht auf Groß-/Kleinschreibung, <c>name</c> nur als direkte Eigenschaft (nie aus <c>frame</c>). Eine Datei mit demselben
/// relativen Pfad in einer späteren Wurzel ersetzt die frühere; danach laden alle Dateien gemeinsam nach Dateinamen (<see cref="GameLoadOrder"/>). Welche
/// von zwei gleichnamigen Definitionen gilt, ist nicht gemessen: <see cref="Find"/> nimmt die zuerst geladene. Mit <see cref="WithWindows"/> kommen die
/// Fenster der <c>.gui</c>-Dateien und die aufgelösten unterstützten Fenster dazu. Unveränderlich.
/// </summary>
public sealed class CustomGuiIndex
{
    public const string Folder = "common/custom_gui";

    private static readonly (string Block, CustomGuiKind Kind)[] Blocks =
    {
        ("custom_button", CustomGuiKind.Button),
        ("custom_text_box", CustomGuiKind.TextBox),
        ("custom_icon", CustomGuiKind.Icon),
        ("custom_shield", CustomGuiKind.Shield),
        ("custom_window", CustomGuiKind.Window),
    };

    private readonly ILookup<string, CustomGuiEntry> _byName;

    private CustomGuiIndex(IReadOnlyList<CustomGuiEntry> entries, ClassicWindowIndexView windows, IReadOnlyList<SupportedWindow> supported)
    {
        Entries = entries;
        Windows = windows;
        Supported = supported;
        _byName = entries.ToLookup(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    public static CustomGuiIndex Empty { get; } = new(Array.Empty<CustomGuiEntry>(), ClassicWindowIndexView.Empty, Array.Empty<SupportedWindow>());

    /// <summary>Alle Einträge in Ladereihenfolge.</summary>
    public IReadOnlyList<CustomGuiEntry> Entries { get; }

    /// <summary>Die Fenster und Elemente der <c>.gui</c>-Dateien (leer bis <see cref="WithWindows"/>).</summary>
    public ClassicWindowIndexView Windows { get; }

    /// <summary>Die unterstützten Fenster, gegen <see cref="Windows"/> aufgelöst (leer bis <see cref="WithWindows"/>).</summary>
    public IReadOnlyList<SupportedWindow> Supported { get; }

    /// <summary>
    /// <paramref name="rootsInLoadOrder"/> wie <see cref="GuiSourceSet.ClassicRootsFor"/> (Vanilla mit DLC-Ordnern, Abhängigkeiten, Mod); die Ebene einer
    /// Wurzel bestimmt <see cref="CustomGuiEntry.IsMod"/>. <paramref name="caseSensitive"/>: Dateinamen der Ladereihenfolge mit Groß-/Kleinschreibung.
    /// </summary>
    public static CustomGuiIndex Build(IReadOnlyList<GuiSourceRoot> rootsInLoadOrder, bool caseSensitive = false)
    {
        var entries = new List<CustomGuiEntry>();
        foreach (var file in FilesOf(rootsInLoadOrder, caseSensitive))
        {
            var isMod = file.Layer == GuiLayer.Mod;
            foreach (var block in Parse(file.FullPath).Root.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock))
            {
                if (KindOf(block.Name) is not { } kind)
                {
                    continue;
                }

                var name = block.Children
                    .LastOrDefault(c => c.Kind == GuiNodeKind.Property && string.Equals(c.Name, "name", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    entries.Add(new CustomGuiEntry(kind, name, file.FullPath, block.Line, isMod));
                }
            }
        }

        return new CustomGuiIndex(entries, ClassicWindowIndexView.Empty, Array.Empty<SupportedWindow>());
    }

    /// <summary>Dieselben Einträge mit den Fenstern der <c>.gui</c>-Dateien und den dagegen aufgelösten unterstützten Fenstern.</summary>
    public CustomGuiIndex WithWindows(ClassicWindowIndexView windows, IReadOnlyList<SupportedWindowSpec> specs) =>
        new(Entries, windows, CustomGuiWindows.Resolve(windows, specs));

    /// <summary>Der zuerst geladene Eintrag dieses Namens (ohne Rücksicht auf Groß-/Kleinschreibung), sonst <see langword="null"/>.</summary>
    public CustomGuiEntry? Find(string name) => _byName[name].FirstOrDefault();

    /// <summary>Alle Einträge dieses Namens in Ladereihenfolge.</summary>
    public IReadOnlyList<CustomGuiEntry> FindAll(string name) => _byName[name].ToList();

    /// <summary>Der Blockname einer Art, z. B. <c>custom_button</c>.</summary>
    public static string BlockName(CustomGuiKind kind) => Blocks.First(b => b.Kind == kind).Block;

    private static CustomGuiKind? KindOf(string block)
    {
        foreach (var (name, kind) in Blocks)
        {
            if (string.Equals(name, block, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>
    /// Die <c>.txt</c>-Dateien direkt in <see cref="Folder"/> aller Wurzeln. Eine Datei mit demselben relativen Pfad in einer späteren Wurzel ersetzt die
    /// frühere; das Ergebnis ist nach Dateinamen in der Ladereihenfolge des Spiels sortiert, über alle Wurzeln hinweg.
    /// </summary>
    private static IReadOnlyList<GuiSourceFile> FilesOf(IReadOnlyList<GuiSourceRoot> roots, bool caseSensitive)
    {
        var files = new Dictionary<string, GuiSourceFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            var directory = Path.Combine(root.Directory, Folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory))
            {
                continue;
            }

            string[] paths;
            try
            {
                paths = Directory.GetFiles(directory, "*.txt", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var path in paths.Where(p => p.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
            {
                var relative = Folder + "/" + Path.GetFileName(path);
                files[relative] = new GuiSourceFile(path, relative, root.Layer, root.Label, 0, 0);
            }
        }

        return files.Values.OrderBy(f => f.RelativePath, GameLoadOrder.For(caseSensitive)).ToList();
    }

    private static GuiDocument Parse(string path)
    {
        try
        {
            return GuiParser.Parse(GuiTextReader.Read(path).Text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return GuiParser.Parse(string.Empty);
        }
    }
}

/// <summary>
/// Regeln der Custom GUI von EU IV: welche Elementtypen eine Art meint, und die Fenster, in denen das Spiel Custom GUI liest
/// (<c>profiles/guiexplain/eu4-custom-gui-windows.json</c>, aus <c>common/custom_gui/example.txt</c>).
/// </summary>
public static class CustomGuiWindows
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Die mitgelieferte Fensterliste neben dem Programm.</summary>
    public static string ShippedPath => Path.Combine(AppContext.BaseDirectory, "profiles", "guiexplain", "eu4-custom-gui-windows.json");

    /// <summary>
    /// Passt ein Element dieses Typs zur Art? <c>custom_button</c> ↔ <c>guiButtonType</c> (gemessen), <c>custom_text_box</c> ↔ <c>instantTextBoxType</c>,
    /// <c>custom_icon</c> ↔ <c>iconType</c>, <c>custom_window</c> ↔ <c>windowType</c> (Doku), <c>custom_shield</c> ↔ <c>guiButtonType</c> (Doku) oder
    /// <c>shieldtype</c> (Spezifikation; beides unsicher). Typnamen ohne Rücksicht auf Groß-/Kleinschreibung.
    /// </summary>
    public static bool Accepts(CustomGuiKind kind, string elementType) => kind switch
    {
        CustomGuiKind.Button => Is(elementType, "guiButtonType"),
        CustomGuiKind.TextBox => Is(elementType, "instantTextBoxType"),
        CustomGuiKind.Icon => Is(elementType, "iconType"),
        CustomGuiKind.Window => Is(elementType, "windowType"),
        CustomGuiKind.Shield => Is(elementType, "guiButtonType") || Is(elementType, "shieldtype"),
        _ => false,
    };

    /// <summary>Die Elementtypen, die zur Art passen, für Meldungen (z. B. <c>guiButtonType</c> oder <c>guiButtonType/shieldtype</c>).</summary>
    public static string AcceptedTypes(CustomGuiKind kind) => kind switch
    {
        CustomGuiKind.Button => "guiButtonType",
        CustomGuiKind.TextBox => "instantTextBoxType",
        CustomGuiKind.Icon => "iconType",
        CustomGuiKind.Window => "windowType",
        _ => "guiButtonType/shieldtype",
    };

    /// <summary>
    /// Ordnet jedem Doku-Namen die echten Fenster seiner Datei zu, in dieser Reihenfolge:
    /// <list type="number">
    /// <item>"alle Fenster der Datei" (<see cref="SupportedWindowSpec.AllWindows"/>): jedes oberste Fenster der Vanilla-Datei (Fenster einer Mod werden so nie unterstützt);</item>
    /// <item>ein oberstes Fenster mit genau diesem Namen (ohne Rücksicht auf Groß-/Kleinschreibung);</item>
    /// <item>ein Fenster dieses Namens tiefer in einem obersten Fenster der Datei (<c>province_window</c> in <c>provinceview</c>);</item>
    /// <item>für Tippfehler der Doku: das einzige oberste Fenster der Vanilla-Datei, dessen Name höchstens 2 Änderungen entfernt ist (Fenster einer Mod machen ihn nie mehrdeutig).</item>
    /// </list>
    /// Ein Doku-Name ohne Treffer fehlt im Ergebnis.
    /// </summary>
    public static IReadOnlyList<SupportedWindow> Resolve(ClassicWindowIndexView windows, IReadOnlyList<SupportedWindowSpec> specs)
    {
        var result = new List<SupportedWindow>();
        foreach (var spec in specs)
        {
            var topLevel = windows.TopLevelInFile(spec.File);
            if (spec.AllWindows)
            {
                result.AddRange(windows.TopLevelInFile(spec.File, vanillaOnly: true).Select(w => Supported(spec, ClassicWindows.NameOf(w), ClassicWindows.NameOf(w))));
                continue;
            }

            if (topLevel.FirstOrDefault(w => string.Equals(ClassicWindows.NameOf(w), spec.DocName, StringComparison.OrdinalIgnoreCase)) is { } exact)
            {
                result.Add(Supported(spec, ClassicWindows.NameOf(exact), ClassicWindows.NameOf(exact)));
                continue;
            }

            var nested = topLevel
                .SelectMany(w => w.Descendants()
                    .Where(n => n.Kind == GuiNodeKind.KeyedBlock && string.Equals(ClassicWindows.NameOf(n), spec.DocName, StringComparison.OrdinalIgnoreCase))
                    .Select(n => Supported(spec, ClassicWindows.NameOf(n), ClassicWindows.NameOf(w))))
                .ToList();
            if (nested.Count > 0)
            {
                result.AddRange(nested);
                continue;
            }

            var close = windows.TopLevelInFile(spec.File, vanillaOnly: true).Where(w => Distance(ClassicWindows.NameOf(w).ToLowerInvariant(), spec.DocName.ToLowerInvariant()) <= 2).ToList();
            if (close.Count == 1)
            {
                result.Add(Supported(spec, ClassicWindows.NameOf(close[0]), ClassicWindows.NameOf(close[0])));
            }
        }

        return result;
    }

    /// <summary>
    /// Das unterstützte Fenster, in dem <paramref name="element"/> liegt (ein echter Nachfahre, jede Tiefe), sonst <see langword="null"/>. Verglichen werden
    /// die Namen: das oberste Fenster des Elements und, für tiefer liegende Fenster, die Namen seiner Vorfahren darin.
    /// </summary>
    public static SupportedWindow? Enclosing(IReadOnlyList<SupportedWindow> supported, GuiNode element)
    {
        if (ClassicWindows.EnclosingOf(element) is not { } top || ReferenceEquals(top, element))
        {
            return null;
        }

        var topName = ClassicWindows.NameOf(top);
        var ancestors = element.Ancestors().TakeWhile(a => !ReferenceEquals(a, top)).Select(ClassicWindows.NameOf).Where(n => n.Length > 0).ToList();
        return supported.FirstOrDefault(s =>
            string.Equals(s.TopLevelWindow, topName, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(s.WindowName, topName, StringComparison.OrdinalIgnoreCase)
                || ancestors.Any(a => string.Equals(a, s.WindowName, StringComparison.OrdinalIgnoreCase))));
    }

    /// <summary>Liest die Fensterliste, Texte in <paramref name="language"/> (sonst Englisch, sonst Deutsch). Wirft nie: Fehler geben eine leere Liste.</summary>
    public static IReadOnlyList<SupportedWindowSpec> Load(string path, string language)
    {
        FileModel? file;
        try
        {
            file = JsonSerializer.Deserialize<FileModel>(File.ReadAllText(path), Options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Array.Empty<SupportedWindowSpec>();
        }

        return (file?.Windows ?? new List<WindowModel>())
            .Where(w => !string.IsNullOrWhiteSpace(w.Doc) && !string.IsNullOrWhiteSpace(w.File))
            .Select(w => new SupportedWindowSpec(w.Doc!.Trim(), w.File!.Trim(), w.AllWindows, Pick(w.Root, language), Pick(w.From, language)))
            .ToList();
    }

    private static SupportedWindow Supported(SupportedWindowSpec spec, string window, string topLevel) =>
        new(spec.DocName, window, spec.File, spec.Root, spec.From, topLevel);

    private static bool Is(string elementType, string expected) => string.Equals(elementType, expected, StringComparison.OrdinalIgnoreCase);

    private static string Pick(Dictionary<string, string>? texts, string language) =>
        texts is null ? string.Empty
        : texts.TryGetValue(language, out var exact) ? exact
        : texts.TryGetValue("en", out var english) ? english
        : texts.TryGetValue("de", out var german) ? german
        : texts.Values.FirstOrDefault() ?? string.Empty;

    /// <summary>Levenshtein-Abstand (Einfügen, Löschen, Ersetzen).</summary>
    private static int Distance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }

            previous = current;
        }

        return previous[b.Length];
    }

    private sealed class FileModel
    {
        public int Version { get; set; }

        public List<WindowModel>? Windows { get; set; }
    }

    private sealed class WindowModel
    {
        public string? Doc { get; set; }

        public string? File { get; set; }

        public bool AllWindows { get; set; }

        public Dictionary<string, string>? Root { get; set; }

        public Dictionary<string, string>? From { get; set; }
    }
}
