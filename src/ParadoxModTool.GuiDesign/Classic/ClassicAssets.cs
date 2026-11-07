using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.Localization;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>Was die klassische Vorschau außer den <c>.gui</c>-Dateien braucht: Sprites und Schriften aus <c>.gfx</c>, Texturen, Bitmap-Schriften. Unveränderlich; spätere Aufgaben ergänzen weitere Teile.</summary>
public sealed class ClassicAssets
{
    private ClassicAssets(
        ClassicSpriteIndex sprites, ClassicTextureLocator textures, ClassicLocalisation localisation, Task<ScriptedGuiIndex>? scriptedGuis, IReadOnlyList<GuiSourceRoot> roots,
        Task<CustomGuiIndex>? customGuis, bool caseSensitive)
    {
        Sprites = sprites;
        Textures = textures;
        Localisation = localisation;
        Fonts = new ClassicFontLibrary(sprites, textures);
        ScriptedGuis = scriptedGuis;
        _roots = roots;
        CustomGuis = customGuis;
        _caseSensitive = caseSensitive;
    }

    private readonly IReadOnlyList<GuiSourceRoot> _roots;
    private readonly bool _caseSensitive;

    /// <summary>
    /// Nur EU IV (<see cref="GuiScriptWiring.CustomGui"/>): Einträge aus <c>common/custom_gui</c>, die Fenster und Elemente der <c>.gui</c>-Dateien und die
    /// aufgelösten unterstützten Fenster, im Hintergrund gebaut. Nicht auf dem UI-Thread abwarten (dort nur bei <see cref="Task.IsCompletedSuccessfully"/>
    /// lesen); die Aufgabe kann scheitern. HOI IV und ohne Skript: <see langword="null"/>.
    /// </summary>
    public Task<CustomGuiIndex>? CustomGuis { get; }

    /// <summary>
    /// Nur HOI IV (<see cref="GuiScriptWiring.ScriptedGuis"/>): Scripted GUIs und ihre Entscheidungskategorien, im Hintergrund gebaut. Nicht auf dem UI-Thread
    /// abwarten (dort nur bei <see cref="Task.IsCompletedSuccessfully"/> lesen); die Aufgabe kann scheitern. EU IV und ohne Skript: <see langword="null"/>.
    /// </summary>
    public Task<ScriptedGuiIndex>? ScriptedGuis { get; }

    public ClassicSpriteIndex Sprites { get; }

    public ClassicTextureLocator Textures { get; }

    /// <summary>Bitmap-Schriften nach Name (aus den <c>.gfx</c>-Dateien dieses Stands).</summary>
    public ClassicFontLibrary Fonts { get; }

    /// <summary>Englische Texte (<c>localisation/*_l_english.yml</c>), träge geladen beim ersten Zugriff.</summary>
    public ClassicLocalisation Localisation { get; }

    /// <summary>
    /// <paramref name="roots"/> in Ladereihenfolge (Vanilla, Abhängigkeiten, Mod, wie <see cref="GuiSourceSet.ClassicRootsFor"/>); die Textursuche läuft
    /// umgekehrt: Mod, Abhängigkeiten, dann Vanilla rückwärts (DLC-Ordner vor der Installation). <paramref name="parse"/> ersetzt das Lesen von der Platte
    /// (Standard: <see cref="GuiTextReader"/>), <paramref name="previous"/> spart das erneute Lesen unveränderter Dateien. Mit
    /// <paramref name="scriptedGuiWindows"/> (HOI IV) entsteht <see cref="ScriptedGuis"/> im Hintergrund gegen diese Fenster, mit
    /// <paramref name="customGuiWindows"/> (EU IV) ebenso <see cref="CustomGuis"/>.
    /// </summary>
    public static ClassicAssets Build(
        IReadOnlyList<GuiSourceRoot> roots,
        IReadOnlyList<GuiSourceFile> gfxFiles,
        bool spriteFirstWins,
        bool caseSensitive,
        Func<GuiSourceFile, GuiDocument>? parse = null,
        ClassicAssets? previous = null,
        Func<ClassicWindowIndexView>? scriptedGuiWindows = null,
        Func<ClassicWindowIndexView>? customGuiWindows = null)
    {
        var sprites = ClassicSpriteIndex.Build(
            gfxFiles, parse ?? ParseFromDisk, spriteFirstWins, caseSensitive, previous?.Sprites);
        var search = roots.Where(r => r.Layer == GuiLayer.Mod)
            .Concat(roots.Where(r => r.Layer == GuiLayer.Dependency))
            .Concat(roots.Where(r => r.Layer == GuiLayer.Vanilla).Reverse())
            .Select(r => r.Directory)
            .ToList();
        var rootList = roots.ToList();
        var scripted = scriptedGuiWindows is null ? null : Task.Run(() => ScriptedGuiIndex.Build(rootList, scriptedGuiWindows(), caseSensitive));
        var custom = customGuiWindows is null ? null : BuildCustomGuis(rootList, caseSensitive, customGuiWindows);
        return new ClassicAssets(sprites, new ClassicTextureLocator(search), ClassicLocalisation.Load(roots), scripted, rootList, custom, caseSensitive);
    }

    /// <summary>Neuer Stand, in dem die gespeicherte <c>.gfx</c>-Datei durch ihren neuen Text ersetzt ist. Texturen (samt gemerkten Größen) und Texte bleiben, denn eine <c>.gfx</c>-Datei ändert keine yml; fehlende oder geänderte Texturen sieht ohnehin der nächste Vorschaulauf (<see cref="ClassicTextureLocator.NewRun"/>).</summary>
    public ClassicAssets WithGfxFile(GuiSourceFile file, string text) =>
        new(Sprites.WithFile(file, GuiParser.Parse(text)), Textures, Localisation, ScriptedGuis, _roots, CustomGuis, _caseSensitive);

    /// <summary>
    /// Eine .gui-Datei wurde gespeichert. HOI IV: die schon gelesenen Scripted GUIs werden im Hintergrund gegen die neuen Fenster aufgelöst (keine Datei wird
    /// neu gelesen). EU IV: die Custom GUI wird im Hintergrund neu gebaut (die wenigen Dateien in <c>common/custom_gui</c> neu gelesen, damit eine
    /// gescheiterte frühere Aufgabe nicht weiterwirkt). Ohne beides bleibt alles, wie es ist.
    /// </summary>
    public ClassicAssets WithWindows(Func<ClassicWindowIndexView> windows) =>
        ScriptedGuis is null && CustomGuis is null
            ? this
            : new(
                Sprites, Textures, Localisation,
                ScriptedGuis is null ? null : ScriptedGuisWith(ScriptedGuis, windows),
                _roots,
                CustomGuis is null ? null : BuildCustomGuis(_roots, _caseSensitive, windows),
                _caseSensitive);

    /// <summary>
    /// HOI IV: eine Skriptdatei unter <c>common/scripted_guis</c> oder <c>common/decisions/categories</c> wurde gespeichert. Die Scripted GUIs werden im
    /// Hintergrund neu gelesen, gegen <paramref name="windows"/>. Ohne Scripted GUIs bleibt alles, wie es ist.
    /// </summary>
    public ClassicAssets WithScriptFile(Func<ClassicWindowIndexView> windows) =>
        ScriptedGuis is null
            ? this
            : new(Sprites, Textures, Localisation, Task.Run(() => ScriptedGuiIndex.Build(_roots, windows(), _caseSensitive)), _roots, CustomGuis, _caseSensitive);

    /// <summary>
    /// EU IV: eine Skriptdatei unter <c>common/custom_gui</c> wurde gespeichert. Die Custom GUI wird im Hintergrund neu gelesen, gegen
    /// <paramref name="windows"/>. Ohne Custom GUI bleibt alles, wie es ist.
    /// </summary>
    public ClassicAssets WithCustomGuiFile(Func<ClassicWindowIndexView> windows) =>
        CustomGuis is null
            ? this
            : new(Sprites, Textures, Localisation, ScriptedGuis, _roots, BuildCustomGuis(_roots, _caseSensitive, windows), _caseSensitive);

    /// <summary>
    /// HOI IV nach dem Speichern einer .gui-Datei: Ist der bisherige Index gebaut, werden nur seine Einträge gegen die neuen Fenster aufgelöst; ist er
    /// gescheitert oder abgebrochen, wird er neu gelesen (wie bei EU IV), damit ein früherer Fehler nicht weiterwirkt.
    /// </summary>
    private Task<ScriptedGuiIndex> ScriptedGuisWith(Task<ScriptedGuiIndex> previous, Func<ClassicWindowIndexView> windows) =>
        previous.ContinueWith(
                t => t.IsCompletedSuccessfully
                    ? Task.FromResult(t.Result.WithWindows(windows()))
                    : Task.Run(() => ScriptedGuiIndex.Build(_roots, windows(), _caseSensitive)),
                TaskScheduler.Default)
            .Unwrap();

    /// <summary>Einträge lesen, Fenster bauen und die mitgelieferte Fensterliste (in der Oberflächensprache) dagegen auflösen, alles im Hintergrund.</summary>
    private static Task<CustomGuiIndex> BuildCustomGuis(IReadOnlyList<GuiSourceRoot> roots, bool caseSensitive, Func<ClassicWindowIndexView> windows) =>
        Task.Run(() => CustomGuiIndex.Build(roots, caseSensitive)
            .WithWindows(windows(), CustomGuiWindows.Load(CustomGuiWindows.ShippedPath, UiText.Current.Language)));

    private static GuiDocument ParseFromDisk(GuiSourceFile file) => GuiParser.Parse(GuiTextReader.Read(file.FullPath).Text);
}
