using System.Text;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class SaveTextCollectorTests
{
    private static readonly IReadOnlyList<(long Start, long End)> NoExclude = Array.Empty<(long, long)>();

    private static IReadOnlyList<string> Collect(string text, params CollectRequest[] requests) =>
        SaveTextCollector.Collect(new MemoryStream(Encoding.UTF8.GetBytes(text)), requests, CancellationToken.None);

    [Fact]
    public void Collects_requested_ranges_in_one_forward_pass_including_nested_ones()
    {
        const string text = "aaa={ b=1 c={ d=2 } } eee=3";
        var outerStart = text.IndexOf("aaa", StringComparison.Ordinal);
        var outerEnd = text.IndexOf(" eee", StringComparison.Ordinal);
        var innerStart = text.IndexOf("c={", StringComparison.Ordinal);
        var innerEnd = text.IndexOf(" }", innerStart + 3, StringComparison.Ordinal) + 2;

        // Anfragen bewusst nicht nach Start sortiert; das Ergebnis folgt der Anfragereihenfolge.
        var result = Collect(
            text,
            new CollectRequest(innerStart, innerEnd, NoExclude),
            new CollectRequest(outerStart, outerEnd, NoExclude),
            new CollectRequest(outerEnd + 1, text.Length, NoExclude));

        Assert.Equal(new[] { "c={ d=2 }", "aaa={ b=1 c={ d=2 } }", "eee=3" }, result);
    }

    [Fact]
    public void Excluded_child_ranges_are_left_out_of_own_scalar_text()
    {
        const string text = "p={ a=1\n c={ x=2 }\n b=3 }";
        var childStart = text.IndexOf("c={", StringComparison.Ordinal);
        var childEnd = text.IndexOf(" }\n", childStart, StringComparison.Ordinal) + 3;

        var result = Collect(
            text,
            new CollectRequest(0, text.Length, new[] { ((long)childStart, (long)childEnd) }),
            new CollectRequest(childStart, childEnd, NoExclude));

        Assert.Equal("p={ a=1\n  b=3 }", result[0]);
        Assert.Equal("c={ x=2 }\n", result[1]);
    }

    [Fact]
    public void Overlapping_and_unsorted_exclusions_are_cut_out_once()
    {
        var result = Collect(
            "0123456789",
            new CollectRequest(0, 10, new[] { (6L, 8L), (2L, 5L), (4L, 7L) }));

        Assert.Equal("0189", result[0]);
    }

    [Fact]
    public void Ranges_crossing_the_one_megabyte_buffer_boundary_are_collected_whole_and_multibyte_characters_survive()
    {
        var filler = new string('x', (1 << 20) - 3);
        const string unit = "a={ n=\"äöü\" }";
        var text = filler + unit + new string('y', 10);
        long start = Encoding.UTF8.GetByteCount(filler);
        long end = start + Encoding.UTF8.GetByteCount(unit);

        // Ausschluss quer über die Puffergrenze: 1 Byte davor bis 3 Bytes dahinter.
        var result = Collect(
            text,
            new CollectRequest(start, end, NoExclude),
            new CollectRequest(start - 5, end, new[] { (start - 1, start + 3) }));

        Assert.Equal(unit, result[0]);
        Assert.Equal("xxxx" + unit[3..], result[1]);
    }

    [Fact]
    public void Empty_and_out_of_stream_ranges_give_empty_or_truncated_text()
    {
        var result = Collect(
            "abcdef",
            new CollectRequest(2, 2, NoExclude),
            new CollectRequest(4, 100, NoExclude),
            new CollectRequest(50, 60, NoExclude));

        Assert.Equal(new[] { string.Empty, "ef", string.Empty }, result);
    }

    [Fact]
    public void A_cancelled_token_stops_the_collection()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            SaveTextCollector.Collect(new MemoryStream(new byte[10]), new[] { new CollectRequest(0, 5, NoExclude) }, cts.Token));
    }
}
