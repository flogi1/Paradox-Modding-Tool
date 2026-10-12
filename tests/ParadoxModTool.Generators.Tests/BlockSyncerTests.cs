using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Blöcke mit einem Schlüsselmuster abgleichen (IV2: Static Modifiers): fehlende dazu, überzählige weg, nur gerenderte Teilschlüssel ersetzen.</summary>
public sealed class BlockSyncerTests
{
    private const string Pattern = "national_idea_modifier_BYZ_*";

    [Fact]
    public void Adds_missing_and_removes_obsolete_blocks()
    {
        const string Existing = "national_idea_modifier_BYZ_1 = {\n\tx = 1\n}\n# alte Idee\nnational_idea_modifier_BYZ_2 = {\n\tx = 2\n}\n";
        const string Rendered = "national_idea_modifier_BYZ_1 = {\n\tx = 1\n}\nnational_idea_modifier_BYZ_3 = {\n\tx = 3\n}\n";

        var result = BlockSyncer.Apply(Existing, Pattern, Rendered, "\n");

        Assert.Equal("national_idea_modifier_BYZ_1 = {\n\tx = 1\n}\nnational_idea_modifier_BYZ_3 = {\n\tx = 3\n}\n", result.Text);
        Assert.Equal(new[] { "national_idea_modifier_BYZ_3" }, result.Added);
        Assert.Equal(new[] { "national_idea_modifier_BYZ_2" }, result.Removed);
        Assert.Empty(result.Replaced);
    }

    [Fact]
    public void Replaces_only_rendered_subkeys_and_keeps_game_data()
    {
        const string Existing = "national_idea_modifier_BYZ_1 = {\n\tgame_data = { category = country }\n\tx = 1\n\ty = 5\n}\n";

        var result = BlockSyncer.Apply(Existing, Pattern, "national_idea_modifier_BYZ_1 = {\n\tx = 2\n\tz = 3\n}\n", "\n");

        Assert.Equal("national_idea_modifier_BYZ_1 = {\n\tgame_data = { category = country }\n\tx = 2\n\ty = 5\n\tz = 3\n}\n", result.Text);
        Assert.Equal(new[] { "national_idea_modifier_BYZ_1" }, result.Replaced);
    }

    [Fact]
    public void Other_tag_with_common_prefix_is_untouched()
    {
        const string Existing = "national_idea_modifier_BYZ_1 = {\n\tx = 1\n}\n";

        var result = BlockSyncer.Apply(Existing, "national_idea_modifier_BY_*", "national_idea_modifier_BY_1 = {\n\tx = 9\n}\n", "\n");

        Assert.StartsWith(Existing, result.Text);
        Assert.Empty(result.Removed);
    }

    [Fact]
    public void Unmatched_text_and_comments_stay_byte_identical()
    {
        const string Existing = "# Kopf\r\nother = {  a = 1 }\r\n\r\nnational_idea_modifier_BYZ_1 = {\r\n\tx = 1\r\n}\r\n# Ende\r\n";

        var result = BlockSyncer.Apply(Existing, Pattern, "national_idea_modifier_BYZ_1 = {\n\tx = 1\n}\n", "\r\n");

        Assert.Equal(Existing, result.Text);
        Assert.Empty(result.Added);
        Assert.Empty(result.Replaced);
    }

    [Fact]
    public void Missing_file_gets_the_rendered_text()
    {
        var result = BlockSyncer.Apply(null, Pattern, "national_idea_modifier_BYZ_1 = {\n\tx = 1\n}\n", "\r\n");

        Assert.Equal("national_idea_modifier_BYZ_1 = {\r\n\tx = 1\r\n}\r\n", result.Text);
        Assert.Equal(new[] { "national_idea_modifier_BYZ_1" }, result.Added);
    }

    private static readonly string[] Keep = { "game_data" };

    private const string GameData = "\tgame_data = {\n\t    category = country\n    }\n";

    [Fact]
    public void Keep_replaces_all_but_game_data()
    {
        var existing = "national_idea_modifier_BYZ_1 = {\n" + GameData + "\ta = 1\n\tb = 2\n}\n";

        var result = BlockSyncer.Apply(existing, Pattern, "national_idea_modifier_BYZ_1 = {\n\tgame_data = { category = country }\n\ta = 5\n}\n", "\n", Keep);

        Assert.Equal("national_idea_modifier_BYZ_1 = {\n" + GameData + "\ta = 5\n}\n", result.Text);
        Assert.Equal(new[] { "national_idea_modifier_BYZ_1" }, result.Replaced);
    }

    [Fact]
    public void Keep_leaves_block_untouched_when_rendered_has_only_kept()
    {
        var existing = "national_idea_modifier_BYZ_1 = {\n" + GameData + "\thand_written = 1\n}\n";

        var result = BlockSyncer.Apply(existing, Pattern, "national_idea_modifier_BYZ_1 = {\n\tgame_data = { category = country }\n\t\n}\n", "\n", Keep);

        Assert.Equal(existing, result.Text);
        Assert.Empty(result.Replaced);
    }

    [Fact]
    public void Keep_replaces_duplicates_and_keeps_block_comments()
    {
        var existing = "# Kommentar zur Idee\nnational_idea_modifier_BYZ_1 = {\n" + GameData + "\tmodifier = a\n\tmodifier = b\n}\nother = 1\n";

        var result = BlockSyncer.Apply(existing, Pattern, "national_idea_modifier_BYZ_1 = {\n\tgame_data = { category = country }\n\tmodifier = c\n\tx = 1\n}\n", "\n", Keep);

        Assert.Equal("# Kommentar zur Idee\nnational_idea_modifier_BYZ_1 = {\n" + GameData + "\tmodifier = c\n\tx = 1\n}\nother = 1\n", result.Text);
    }

    [Fact]
    public void Keep_new_and_removed_blocks_as_before()
    {
        var existing = "national_idea_modifier_BYZ_1 = {\n" + GameData + "\ta = 1\n}\nnational_idea_modifier_BYZ_2 = {\n" + GameData + "}\n";
        const string Rendered = "national_idea_modifier_BYZ_1 = {\n\tgame_data = { category = country }\n}\nnational_idea_modifier_BYZ_3 = {\n\tgame_data = { category = country }\n\tc = 3\n}\n";

        var result = BlockSyncer.Apply(existing, Pattern, Rendered, "\n", Keep);

        Assert.Equal(
            "national_idea_modifier_BYZ_1 = {\n" + GameData + "\ta = 1\n}\nnational_idea_modifier_BYZ_3 = {\n\tgame_data = { category = country }\n\tc = 3\n}\n",
            result.Text);
        Assert.Equal(new[] { "national_idea_modifier_BYZ_2" }, result.Removed);
        Assert.Equal(new[] { "national_idea_modifier_BYZ_3" }, result.Added);
    }

    [Fact]
    public void Keep_with_crlf_and_one_line_block()
    {
        const string Existing = "national_idea_modifier_BYZ_1 = { a = 1 }\r\n";

        var result = BlockSyncer.Apply(Existing, Pattern, "national_idea_modifier_BYZ_1 = {\r\n\tb = 2\r\n}\r\n", "\r\n", Keep);

        Assert.Equal("national_idea_modifier_BYZ_1 = {\r\n\tb = 2\r\n}\r\n", result.Text);
    }
}
