using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Ladereihenfolge von Victoria 3, gemessen 2026-10-11 mit PMT Order Probe C: gui/zz_… lädt vor gui/shared/… (Dateien vor Unterordnern), und
/// gui/frontend/shared/buttons_icons.gui lädt vor …/buttonsa_pmt.gui ("_" vor Buchstaben, ASCII in Kleinschreibung).
/// </summary>
public class GameLoadOrderTests
{
    private static int Compare(string a, string b) => Math.Sign(GameLoadOrder.Instance.Compare(a, b));

    [Theory]
    [InlineData("gui/zz_pmt_order_c.gui", "gui/shared/button_icons.gui")]
    [InlineData("gui/frontend/shared/buttons_icons.gui", "gui/frontend/shared/buttonsa_pmt.gui")]
    [InlineData("gui/00_b.gui", "gui/shared/zz_a.gui")]
    [InlineData("gui/a/b.gui", "gui/a/x/c.gui")]
    [InlineData("gui/A.gui", "gui/b.gui")]
    public void First_path_loads_earlier(string earlier, string later)
    {
        Assert.Equal(-1, Compare(earlier, later));
        Assert.Equal(1, Compare(later, earlier));
    }

    [Fact]
    public void Same_path_in_other_case_or_with_backslashes_is_equal()
    {
        Assert.Equal(0, Compare(@"gui\Shared\X.gui", "gui/shared/x.gui"));
    }
}
