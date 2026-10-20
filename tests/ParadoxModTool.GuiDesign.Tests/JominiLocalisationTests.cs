using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Texte der Jomini-Spiele je Sprache (Reihenfolge Vanilla, DLC, Abhängigkeiten, Mod; Ordner <c>replace</c>).</summary>
public sealed class JominiLocalisationTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-jominiloc-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private static readonly string[] NoContexts = Array.Empty<string>();

    /// <summary>Schreibt eine yml-Datei mit BOM und <c>l_&lt;sprache&gt;:</c>-Kopf; <paramref name="relative"/> ist der Pfad unter <see cref="_temp"/>.</summary>
    private void Yml(string relative, string language, params string[] lines)
    {
        var path = Path.Combine(_temp, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text = "l_" + language + ":\n" + string.Concat(lines.Select(line => " " + line + "\n"));
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private string Dir(string relative)
    {
        var path = Path.Combine(_temp, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Reads_the_language_and_falls_back_to_english()
    {
        Yml("install/game/localization/english/a_l_english.yml", "english", "A:0 \"English A\"", "B:0 \"English B\"");
        Yml("install/game/localization/german/a_l_german.yml", "german", "A:0 \"Deutsch A\"");

        var loc = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), null, NoContexts);

        Assert.Equal("Deutsch A", loc.Text("A", "l_german"));
        Assert.Null(loc.Text("B", "l_german"));
        Assert.Equal("Deutsch A", loc.TextOrEnglish("A", "l_german"));
        Assert.Equal("English B", loc.TextOrEnglish("B", "l_german"));
    }

    [Fact]
    public void Fresh_reads_saved_files_again_with_the_same_roots()
    {
        Yml("install/game/localization/english/a_l_english.yml", "english", "V:0 \"vanilla\"");
        Yml("dep/localization/english/d_l_english.yml", "english", "D:0 \"dep alt\"");
        Yml("mod/localization/english/m_l_english.yml", "english", "M:0 \"mod alt\"");
        var loc = JominiLocalisation.Load(Dir("install"), new[] { Dir("dep") }, Dir("mod"), NoContexts);
        Assert.Equal("mod alt", loc.Text("M", JominiLocalisation.English));

        Yml("dep/localization/english/d_l_english.yml", "english", "D:0 \"dep neu\"");
        Yml("mod/localization/english/m_l_english.yml", "english", "M:0 \"mod neu\"");
        var fresh = loc.Fresh();

        // Die alte Lokalisierung bleibt bei ihrem Stand; die frische liest Mod, Abhängigkeit und Vanilla neu.
        Assert.Equal("mod alt", loc.Text("M", JominiLocalisation.English));
        Assert.Equal("mod neu", fresh.Text("M", JominiLocalisation.English));
        Assert.Equal("dep neu", fresh.Text("D", JominiLocalisation.English));
        Assert.Equal("vanilla", fresh.Text("V", JominiLocalisation.English));
    }

    [Fact]
    public void Mod_beats_dependency_beats_dlc_beats_vanilla()
    {
        Yml("install/game/localization/english/a_l_english.yml", "english", "V:0 \"vanilla\"", "D:0 \"vanilla\"", "P:0 \"vanilla\"", "M:0 \"vanilla\"");
        Yml("install/game/dlc/dlc001/localization/english/a_l_english.yml", "english", "D:0 \"dlc\"", "P:0 \"dlc\"", "M:0 \"dlc\"");
        Yml("dep1/localization/english/a_l_english.yml", "english", "P:0 \"dep1\"", "M:0 \"dep1\"");
        Yml("dep2/localization/english/a_l_english.yml", "english", "P:0 \"dep2\"", "M:0 \"dep2\"");
        Yml("mod/localization/english/a_l_english.yml", "english", "M:0 \"mod\"");

        // Die erste Abhängigkeit gewinnt unter den Abhängigkeiten (wie beim Textur-Finder).
        var loc = JominiLocalisation.Load(Dir("install"), new[] { Dir("dep1"), Dir("dep2") }, Dir("mod"), NoContexts);

        Assert.Equal("vanilla", loc.Text("V", "l_english"));
        Assert.Equal("dlc", loc.Text("D", "l_english"));
        Assert.Equal("dep1", loc.Text("P", "l_english"));
        Assert.Equal("mod", loc.Text("M", "l_english"));
    }

    [Fact]
    public void Replace_folder_wins_within_a_root()
    {
        // "replace" sortiert vor "z", wird aber trotzdem zuletzt geladen.
        Yml("install/game/localization/english/replace/a_l_english.yml", "english", "K:0 \"replaced\"");
        Yml("install/game/localization/english/z_l_english.yml", "english", "K:0 \"normal\"", "L:0 \"only normal\"");

        var loc = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), null, NoContexts);

        Assert.Equal("replaced", loc.Text("K", "l_english"));
        Assert.Equal("only normal", loc.Text("L", "l_english"));
    }

    [Fact]
    public void Eu5_context_folders_are_read()
    {
        Yml("install/game/main_menu/localization/english/x_l_english.yml", "english", "MENU:0 \"Main menu\"");
        Yml("install/game/in_game/localization/english/x_l_english.yml", "english", "GAME:0 \"In game\"");
        Yml("install/game/loading_screen/localization/english/x_l_english.yml", "english", "LOAD:0 \"Loading\"");
        Yml("install/game/dlc/dlc001/in_game/localization/english/x_l_english.yml", "english", "DLCGAME:0 \"Dlc in game\"");

        var loc = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), null, new[] { "in_game", "main_menu", "loading_screen" });

        Assert.Equal("Main menu", loc.Text("MENU", "l_english"));
        Assert.Equal("In game", loc.Text("GAME", "l_english"));
        Assert.Equal("Loading", loc.Text("LOAD", "l_english"));
        Assert.Equal("Dlc in game", loc.Text("DLCGAME", "l_english"));
        Assert.Contains("l_english", loc.Languages);
    }

    [Fact]
    public void Escaped_newline_and_quote_are_unescaped()
    {
        Yml("install/game/localization/english/a_l_english.yml", "english", "K:0 \"a\\nb \\\"c\\\"\"");

        var loc = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), null, NoContexts);

        Assert.Equal("a" + "\n" + "b \"c\"", loc.Text("K", "l_english"));
    }

    [Fact]
    public void Languages_lists_vanilla_folders_english_first()
    {
        Yml("install/game/localization/german/a_l_german.yml", "german", "K:0 \"x\"");
        Yml("install/game/localization/french/a_l_french.yml", "french", "K:0 \"x\"");
        Yml("install/game/localization/english/a_l_english.yml", "english", "K:0 \"x\"");
        Yml("install/game/localization/replace/a_l_english.yml", "english", "K:0 \"x\"");
        Dir("install/game/localization/empty");
        // Sprachen der Mod zählen nicht: nur Vanilla.
        Yml("mod/localization/spanish/a_l_spanish.yml", "spanish", "K:0 \"x\"");

        var loc = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), Dir("mod"), NoContexts);

        Assert.Equal(new[] { "l_english", "l_french", "l_german" }, loc.Languages);
    }

    [Fact]
    public void Unknown_language_falls_back_to_english()
    {
        Yml("install/game/localization/english/a_l_english.yml", "english", "K:0 \"English value\"");

        var loc = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), null, NoContexts);

        Assert.Null(loc.Text("K", "l_klingon"));
        Assert.Equal("English value", loc.TextOrEnglish("K", "l_klingon"));
    }

    [Fact]
    public void Missing_install_gives_null_and_no_exception()
    {
        var loc = JominiLocalisation.Load(Path.Combine(_temp, "gibt-es-nicht"), new[] { Path.Combine(_temp, "auch-nicht") }, Path.Combine(_temp, "mod-nicht"), NoContexts);

        Assert.Null(loc.Text("K", "l_english"));
        Assert.Null(loc.TextOrEnglish("K", "l_german"));
        Assert.Empty(loc.Languages);
        Assert.Null(JominiLocalisation.Empty.Text("K", "l_english"));
        Assert.Empty(JominiLocalisation.Empty.Languages);
    }

    [Fact]
    public void Parallel_first_lookup_is_safe()
    {
        Yml("install/game/localization/english/a_l_english.yml", "english", "K:0 \"value\"");
        var loc = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), null, NoContexts);

        var results = Enumerable.Range(0, 16).AsParallel().Select(_ => loc.Text("K", "l_english")).ToList();

        Assert.All(results, result => Assert.Equal("value", result));
    }

    [Fact]
    public void Base_loads_before_its_contexts_in_context_order()
    {
        // Alphabetisch käme "in_game" vor "main_menu" und beide nach "localization"; geladen wird aber Basis, dann Kontexte in der Reihenfolge der Liste.
        Yml("install/game/localization/english/a_l_english.yml", "english", "K:0 \"base\"", "ONLYBASE:0 \"base\"", "J:0 \"base\"");
        Yml("install/game/main_menu/localization/english/a_l_english.yml", "english", "K:0 \"main_menu\"", "J:0 \"main_menu\"");
        Yml("install/game/in_game/localization/english/a_l_english.yml", "english", "K:0 \"in_game\"");

        var contexts = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), null, new[] { "main_menu", "in_game" });
        Assert.Equal("in_game", contexts.Text("K", "l_english"));
        Assert.Equal("main_menu", contexts.Text("J", "l_english"));
        Assert.Equal("base", contexts.Text("ONLYBASE", "l_english"));

        var reversed = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), null, new[] { "in_game", "main_menu" });
        Assert.Equal("main_menu", reversed.Text("K", "l_english"));
    }

    [Fact]
    public void Replace_files_of_a_root_load_after_all_its_bases()
    {
        Yml("install/game/localization/english/replace/a_l_english.yml", "english", "K:0 \"replace\"");
        Yml("install/game/in_game/localization/english/a_l_english.yml", "english", "K:0 \"in_game\"");

        var loc = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), null, new[] { "in_game" });

        Assert.Equal("replace", loc.Text("K", "l_english"));
    }

    [Fact]
    public void Broken_files_are_skipped_and_the_language_still_loads()
    {
        Yml("install/game/localization/english/a_l_english.yml", "english", "GOOD:0 \"good\"");
        Yml("install/game/localization/english/z_l_english.yml", "english", "GOOD2:0 \"good2\"");
        // Ungültige UTF-8-Bytes und eine gesperrte Datei dürfen nichts kaputt machen.
        var garbage = Path.Combine(_temp, "install", "game", "localization", "english", "m_l_english.yml");
        File.WriteAllBytes(garbage, new byte[] { 0xFF, 0xFE, 0x00, 0xD8, 0x80, 0x81, 0x22, 0x3A, 0x0A, 0xC3, 0x28 });
        var locked = Path.Combine(_temp, "install", "game", "localization", "english", "n_l_english.yml");
        File.WriteAllText(locked, "l_english:\n LOCKED:0 \"x\"\n");

        var loc = JominiLocalisation.Load(Dir("install"), Array.Empty<string>(), null, NoContexts);
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Equal("good", loc.Text("GOOD", "l_english"));
            Assert.Equal("good2", loc.Text("GOOD2", "l_english"));
        }

        Assert.Null(loc.Text("LOCKED", "l_english"));
    }
}
