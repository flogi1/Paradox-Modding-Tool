using System.Text;
using ParadoxModTool.Generators.Editing.Package;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Beispiel-Listen des Vorschlag-Dialogs: Dateien, oberste Blöcke einer Datei, englische Lokalisierungsschlüssel.</summary>
public sealed class PatternExamplesTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string Mod => _temp.PathOf("mod");

    [Fact]
    public void Files_are_relative_with_slashes_and_sorted()
    {
        _temp.Write("mod/b/x.txt", "a = { }");
        _temp.Write("mod/a.txt", "a = { }");

        Assert.Equal(new[] { "a.txt", "b/x.txt" }, PatternExamples.Files(Mod));
        Assert.Empty(PatternExamples.Files(_temp.PathOf("missing")));
    }

    [Fact]
    public void BlockKeys_lists_only_top_level_blocks()
    {
        _temp.Write("mod/m.txt", "m_1 = { inner = { } }\nvalue = 3\nm_2 = { }");

        Assert.Equal(new[] { "m_1", "m_2" }, PatternExamples.BlockKeys(Mod, "m.txt"));
        Assert.Empty(PatternExamples.BlockKeys(Mod, "missing.txt"));
        Assert.Empty(PatternExamples.BlockKeys(Mod, "../m.txt"));
    }

    [Fact]
    public void LocalizationKeys_reads_english_files()
    {
        _temp.Write("mod/main_menu/localization/english/a_l_english.yml", "l_english:\n k_1: \"A\"\n k_2:0 \"B\"\n", new UTF8Encoding(true));
        _temp.Write("mod/main_menu/localization/german/a_l_german.yml", "l_german:\n g_1: \"G\"\n", new UTF8Encoding(true));

        Assert.Equal(new[] { "k_1", "k_2" }, PatternExamples.LocalizationKeys(Mod));
    }
}
