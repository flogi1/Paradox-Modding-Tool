using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>Datei- und Ordnerdialoge merken sich den letzten Ordner je Spiel und Zweck (Windows speichert ihn pro Kennung).</summary>
public class GameDialogMemoryTests
{
    [Fact]
    public void Same_game_and_purpose_give_the_same_id()
    {
        Assert.Equal(GameDialogMemory.For("eu4", "diff.fileA"), GameDialogMemory.For("eu4", "diff.fileA"));
    }

    [Fact]
    public void Different_games_give_different_ids()
    {
        Assert.NotEqual(GameDialogMemory.For("eu4", "diff.fileA"), GameDialogMemory.For("ck3", "diff.fileA"));
    }

    [Fact]
    public void Different_purposes_give_different_ids()
    {
        Assert.NotEqual(GameDialogMemory.For("eu4", "diff.fileA"), GameDialogMemory.For("eu4", "diff.fileB"));
    }

    [Fact]
    public void Game_id_ignores_case_and_a_missing_game_still_gives_an_id()
    {
        Assert.Equal(GameDialogMemory.For("EU4", "x"), GameDialogMemory.For("eu4", "x"));
        Assert.NotEqual(Guid.Empty, GameDialogMemory.For(null, "x"));
    }

    [Fact]
    public void Current_game_is_used_by_the_short_form()
    {
        var before = GameDialogMemory.CurrentGameId;
        try
        {
            GameDialogMemory.CurrentGameId = "eu5";
            Assert.Equal(GameDialogMemory.For("eu5", "x"), GameDialogMemory.For("x"));
        }
        finally
        {
            GameDialogMemory.CurrentGameId = before;
        }
    }
}
