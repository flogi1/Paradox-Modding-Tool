using System.Text.RegularExpressions;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Lokalisierung in gemeinsame Dateien (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 2): nur die passenden Schlüssel ersetzen.</summary>
public sealed class LocalizationMergerTests
{
    private static readonly Regex Keys = new("^iv2_x_18(_|$)", RegexOptions.CultureInvariant);

    [Fact]
    public void Replaces_only_matching_keys()
    {
        const string existing = "l_english:\n iv2_x_17: \"A\"\n iv2_x_18: \"B\"\n iv2_x_18_1: \"C\"\n iv2_x_180: \"D\"\n";

        var text = LocalizationMerger.Apply(existing, "english", Keys, " iv2_x_18: \"B2\"\n iv2_x_18_1: \"C2\"\n", "\n");

        Assert.Equal("l_english:\n iv2_x_17: \"A\"\n iv2_x_18: \"B2\"\n iv2_x_18_1: \"C2\"\n iv2_x_180: \"D\"\n", text);
    }

    [Fact]
    public void Other_lines_stay_byte_equal()
    {
        const string existing = "l_german:\r\n # Kommentar iv2_x_18\r\n iv2_x_17:0 \"A\"\r\n iv2_x_18:0 \"B\"\r\n iv2_y: \"Y\"   \r\n";

        var text = LocalizationMerger.Apply(existing, "german", Keys, " iv2_x_18:0 \"B2\"\r\n", "\r\n");

        Assert.Equal("l_german:\r\n # Kommentar iv2_x_18\r\n iv2_x_17:0 \"A\"\r\n iv2_x_18:0 \"B2\"\r\n iv2_y: \"Y\"   \r\n", text);
    }

    [Fact]
    public void Missing_file_gets_header()
    {
        var text = LocalizationMerger.Apply(null, "german", Keys, " iv2_x_18: \"B\"\n", "\n");

        Assert.Equal("l_german:\n iv2_x_18: \"B\"\n", text);
    }

    [Fact]
    public void Rendered_header_is_dropped_for_existing_file()
    {
        var text = LocalizationMerger.Apply("l_english:\n a: \"1\"\n", "english", Keys, "l_english:\n iv2_x_18: \"B\"\n", "\n");

        Assert.Equal("l_english:\n a: \"1\"\n iv2_x_18: \"B\"\n", text);
    }

    [Fact]
    public void No_match_appends_at_end()
    {
        var text = LocalizationMerger.Apply("l_english:\n a: \"1\"", "english", Keys, " iv2_x_18: \"B\"\n", "\n");

        Assert.Equal("l_english:\n a: \"1\"\n iv2_x_18: \"B\"\n", text);
    }

    [Fact]
    public void InsertAfter_puts_new_keys_after_last_matching_key()
    {
        var existing = "l_english:\n k_army_1: \"A\"\n k_army_2: \"B\"\n k_navy_1: \"N\"\n";
        var text = LocalizationMerger.Apply(existing, "english", new Regex("^k_army_3$"), " k_army_3: \"C\"\n", "\n", new Regex("^k_army_[0-9]+$"));
        Assert.Equal("l_english:\n k_army_1: \"A\"\n k_army_2: \"B\"\n k_army_3: \"C\"\n k_navy_1: \"N\"\n", text);
    }

    [Fact]
    public void InsertAfter_without_anchor_appends_as_before()
    {
        var existing = "l_english:\n k_navy_1: \"N\"\n";
        var text = LocalizationMerger.Apply(existing, "english", new Regex("^k_army_3$"), " k_army_3: \"C\"\n", "\n", new Regex("^k_army_[0-9]+$"));
        Assert.Equal("l_english:\n k_navy_1: \"N\"\n k_army_3: \"C\"\n", text);
    }

    [Fact]
    public void Insert_keeps_leading_blank_line()
    {
        var text = LocalizationMerger.Apply("l_english:\n k_1: \"a\"\n", "english", new Regex("^k_2$"), " \n k_2: \"b\"\n", "\n");

        Assert.Equal("l_english:\n k_1: \"a\"\n \n k_2: \"b\"\n", text);
    }

    [Fact]
    public void InsertAfter_keeps_leading_blank_line()
    {
        var text = LocalizationMerger.Apply("l_english:\n k_army_1: \"a\"\n k_navy_1: \"n\"\n", "english", new Regex("^k_army_2$"), " \n k_army_2: \"b\"\n", "\n", new Regex("^k_army_[0-9]+$"));

        Assert.Equal("l_english:\n k_army_1: \"a\"\n \n k_army_2: \"b\"\n k_navy_1: \"n\"\n", text);
    }

    [Fact]
    public void Hit_replacement_still_drops_leading_blank_lines()
    {
        var text = LocalizationMerger.Apply("l_english:\n k_1: \"a\"\n k_2: \"b\"\n", "english", new Regex("^k_2$"), " \n k_2: \"c\"\n", "\n");

        Assert.Equal("l_english:\n k_1: \"a\"\n k_2: \"c\"\n", text);
    }
}
