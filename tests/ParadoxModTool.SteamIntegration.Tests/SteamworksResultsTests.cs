using System.Buffers.Binary;
using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class SteamworksResultsTests
{
    private static byte[] Create(int result, ulong id, bool flag, bool pack8)
    {
        var buffer = new byte[pack8 ? 24 : 16];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, result);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(pack8 ? 8 : 4), id);
        buffer[pack8 ? 16 : 12] = flag ? (byte)1 : (byte)0;
        return buffer;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateItem_BeideAnordnungen_LiefernIdUndFlag(bool pack8)
    {
        var parsed = SteamworksResults.ParseCreateItem(Create(1, 3456789012UL, true, pack8));

        Assert.NotNull(parsed);
        Assert.Equal(1, parsed!.Result);
        Assert.Equal(3456789012UL, parsed.PublishedFileId);
        Assert.True(parsed.NeedsLegalAgreement);
    }

    [Fact]
    public void CreateItem_MitGroßemPuffer_UndFlagAus()
    {
        var big = new byte[SteamworksResults.BufferSize];
        Create(1, 3456789012UL, false, pack8: true).CopyTo(big, 0);

        var parsed = SteamworksResults.ParseCreateItem(big);

        Assert.Equal(3456789012UL, parsed!.PublishedFileId);
        Assert.False(parsed.NeedsLegalAgreement);
    }

    [Fact]
    public void CreateItem_FehlercodeOhneId_LiefertDenCode()
    {
        var parsed = SteamworksResults.ParseCreateItem(Create(15, 0, false, pack8: false));

        Assert.Equal(15, parsed!.Result);
        Assert.Equal(0UL, parsed.PublishedFileId);
    }

    [Fact]
    public void CreateItem_ErfolgMitUnplausiblerId_IstNull()
    {
        var buffer = Create(1, 0, false, pack8: false);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(4), ulong.MaxValue);
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(8), ulong.MaxValue);

        Assert.Null(SteamworksResults.ParseCreateItem(buffer));
    }

    [Fact]
    public void CreateItem_ZuKurzerPuffer_IstNull()
    {
        Assert.Null(SteamworksResults.ParseCreateItem(new byte[3]));
        Assert.Null(SteamworksResults.ParseCreateItem(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void SubmitItem_LiestCodeFlagUndId()
    {
        var buffer = new byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);
        buffer[4] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(8), 3456789012UL);

        var parsed = SteamworksResults.ParseSubmitItem(buffer);

        Assert.Equal(1, parsed!.Result);
        Assert.True(parsed.NeedsLegalAgreement);
        Assert.Equal(3456789012UL, parsed.PublishedFileId);
    }

    [Fact]
    public void SubmitItem_KurzerPuffer_OhneId_UndZuKurz()
    {
        var shortBuffer = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(shortBuffer, 15);

        var parsed = SteamworksResults.ParseSubmitItem(shortBuffer);

        Assert.Equal(15, parsed!.Result);
        Assert.Equal(0UL, parsed.PublishedFileId);
        Assert.Null(SteamworksResults.ParseSubmitItem(new byte[2]));
    }

    [Theory]
    [InlineData(15, "verweigert")]
    [InlineData(16, "rechtzeitig")]
    [InlineData(25, "Limit")]
    [InlineData(3, "Verbindung")]
    [InlineData(9999, "9999")]
    public void Describe_UebersetztFehlercodes(int code, string expected)
    {
        Assert.Contains(expected, SteamResultText.Describe(code));
    }

    [Fact]
    public void Accessor_HoechsteVersionGewinnt_UndFehlendIstNull()
    {
        var exports = new HashSet<string> { "SteamAPI_SteamUGC_v014", "SteamAPI_SteamUGC_v016", "SteamAPI_SteamUser_v021" };

        Assert.Equal("SteamAPI_SteamUGC_v016", AccessorSearch.Find("SteamAPI_SteamUGC", exports.Contains));
        Assert.Equal("SteamAPI_SteamUser_v021", AccessorSearch.Find("SteamAPI_SteamUser", exports.Contains));
        Assert.Null(AccessorSearch.Find("SteamAPI_SteamUtils", exports.Contains));
    }
}
