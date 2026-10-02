using ParadoxModTool.ErrorLog;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class ReportLeadsTests
{
    private static readonly Func<ErrorEntry, LeadAttribution> NoAttribution = _ => new LeadAttribution(null, false, null);

    private static TimelineEntry Entry(string message, int machine = 1, ErrorEntry? error = null, string? date = "1425.2.4.7") =>
        new(machine, "Host", DumpFileKind.Error, Line(message), date, error, LeadKind.ScriptError, false);

    private static LogLine Line(string message) => new("20:06:59", "jomini_script_system.cpp", 248, message, 1);

    private static string MessageOf(string logMessage) =>
        Assert.Single(ReportLeads.From([Entry(logMessage)], NoAttribution)).Message;

    [Fact]
    public void A_script_error_block_keeps_the_error_lines_without_header_and_location()
    {
        Assert.Equal("Error: Undefined event target 'target'",
            MessageOf("Script system error!\nError: Undefined event target 'target'\nScript location: common/character_interactions/marry_noble.txt:92"));
    }

    [Fact]
    public void Several_remaining_lines_are_joined_with_a_slash()
    {
        Assert.Equal("Error: Event target link 'scope' returned an unset scope / called from effect marry",
            MessageOf("Script system error!\n  Error: Event target link 'scope' returned an unset scope\n  Script location: common/a.txt:1\ncalled from effect marry\n"));
    }

    [Fact]
    public void Without_other_lines_the_first_line_is_kept()
    {
        Assert.Equal("Script system error!", MessageOf("Script system error!"));
        Assert.Equal("Script system error!", MessageOf("Script system error!\nScript location: common/a.txt:1"));
    }

    [Fact]
    public void A_plain_message_stays_as_it_is()
    {
        Assert.Equal("Invalid command.", MessageOf("Invalid command."));
    }

    [Fact]
    public void Each_entry_becomes_one_lead_with_its_machine_and_the_attribution_of_its_error()
    {
        var error = new ErrorEntry
        {
            Message = "Undefined event target",
            Line = Line("Script system error!"),
            FilePath = "common/character_interactions/marry_noble.txt",
            FileLine = 92,
        };
        var attributed = new List<ErrorEntry>();

        var leads = ReportLeads.From(
            [Entry("Script system error!\nError: Undefined event target 'target'", 2, error), Entry("Invalid command.", 1, date: null)],
            e =>
            {
                attributed.Add(e);
                return new LeadAttribution("Flogi Fixes", true, @"C:\mods\flogi\common\character_interactions\marry_noble.txt");
            });

        Assert.Equal(2, leads.Count);
        Assert.Equal(new ReportLead([2], "1425.2.4.7", "Error: Undefined event target 'target'", "common/character_interactions/marry_noble.txt",
            @"C:\mods\flogi\common\character_interactions\marry_noble.txt", 92, "Flogi Fixes", true, LeadKind.ScriptError), leads[0]);
        Assert.Equal(new ReportLead([1], null, "Invalid command.", null, null, null, null, false, LeadKind.ScriptError), leads[1]);
        Assert.Same(error, Assert.Single(attributed));
    }

    [Fact]
    public void Leads_with_equal_machine_ids_in_different_lists_are_equal()
    {
        var first = new ReportLead(new List<int> { 1, 2 }, "1425.2.4.7", "m", "a.txt", null, 3, null, false, LeadKind.ScriptError);
        var second = new ReportLead(new[] { 1, 2 }, "1425.2.4.7", "m", "a.txt", null, 3, null, false, LeadKind.ScriptError);
        var other = first with { MachineIds = [2] };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, other);
    }
}
