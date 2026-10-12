using ParadoxModTool.Generators.Editing;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

public class ImageSuggestTests
{
    [Fact]
    public void Suggest_order_exact_prefix_contains()
    {
        var entries = new[] { "coa/XBYZ.dds", "coa/BYZ_old.dds", "coa/BYZ.dds", "coa/AAC.dds" };

        Assert.Equal("coa/BYZ.dds", ImageSuggest.ForKey(entries, "byz"));
        Assert.Equal("coa/BYZ_old.dds", ImageSuggest.ForKey(entries.Where(e => e != "coa/BYZ.dds").ToList(), "BYZ"));
        Assert.Equal("coa/XBYZ.dds", ImageSuggest.ForKey(new[] { "coa/XBYZ.dds" }, "BYZ"));
        Assert.Null(ImageSuggest.ForKey(entries, "QQQ"));
        Assert.Null(ImageSuggest.ForKey(entries, ""));
        Assert.Null(ImageSuggest.ForKey(entries, null));
    }
}
