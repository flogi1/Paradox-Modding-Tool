using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class OosMetadataTests
{
    [Fact]
    public void Parses_the_host_metadata_of_a_real_dump()
    {
        var m = OosMetadata.Parse(Dumps.HostMetadata11c0);

        Assert.Equal("11c0de175691b2bb_0", m.FolderName);
        Assert.Equal("11c0de175691b2bb", m.OosId16);
        Assert.Equal(1, m.LocalMachineId);
        Assert.Equal(2, m.OosMachineId);
        Assert.Equal("f2c15314d528e6065a86af708151248f23732b29", m.GameCommit);
        Assert.Equal("f57700e48809374e8418681b5cbd3ed1a27723fb", m.EngineCommit);
        Assert.Equal("ReleaseLto", m.BuildConfiguration);
        Assert.Equal("preparation", m.HotjoinVsPreparation);
        Assert.Equal(0, m.TimesDesynchronized);
        Assert.Equal("l_english", m.Language);
        Assert.Equal(new[] { "RANDOM_COUNT" }, m.AllChecksums);
        Assert.Equal(new[] { "RANDOM_COUNT" }, m.OosChecksums);
        Assert.Equal("flogi", m.PlayerName);
    }

    [Fact]
    public void Time_utc_keeps_the_text_after_the_colon_trimmed()
    {
        var m = OosMetadata.Parse(Dumps.HostMetadata11c0);

        Assert.Equal("21:47:15 0125-10-11", m.TimeUtc);
    }

    [Fact]
    public void Parses_windows_line_endings()
    {
        var m = OosMetadata.Parse(Dumps.HostMetadata11c0.Replace("\n", "\r\n"));

        Assert.Equal("11c0de175691b2bb", m.OosId16);
        Assert.Equal("flogi", m.PlayerName);
        Assert.Equal(new[] { "RANDOM_COUNT" }, m.OosChecksums);
    }

    [Fact]
    public void Missing_lines_give_nulls_and_empty_lists_not_exceptions()
    {
        var m = OosMetadata.Parse("local_machine_id:2");

        Assert.Equal(2, m.LocalMachineId);
        Assert.Equal("", m.FolderName);
        Assert.Equal("", m.OosId16);
        Assert.Null(m.PlayerName);
        Assert.Null(m.GameCommit);
        Assert.Null(m.TimeUtc);
        Assert.Empty(m.AllChecksums);
        Assert.Empty(m.OosChecksums);
    }

    [Fact]
    public void Several_checksum_categories_are_collected_in_order()
    {
        var m = OosMetadata.Parse("all_checksums:RANDOM_COUNT,CHARACTER\noos_checksums:\n0:RANDOM_COUNT\n1:CHARACTER\n");

        Assert.Equal(new[] { "RANDOM_COUNT", "CHARACTER" }, m.AllChecksums);
        Assert.Equal(new[] { "RANDOM_COUNT", "CHARACTER" }, m.OosChecksums);
    }
}
