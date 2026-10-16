using ParadoxModTool.Updates;
using Xunit;

namespace ParadoxModTool.Updates.Tests;

/// <summary>Unerwartete Ausnahmen: alles ins Protokoll, Meldungen nur einzeln und begrenzt.</summary>
public sealed class UnexpectedErrorsTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 10, 14, 12, 0, 0);

    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private UnexpectedErrors Create() => new(new UpdateLog(_temp.Combine("error.log")));

    private static Exception Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message, new IOException("inner"));
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    [Fact]
    public void Every_exception_is_logged_with_source_inner_exception_and_stack()
    {
        var errors = Create();

        errors.Record(Thrown("boom"), "Dispatcher", Start);
        errors.Log(Thrown("background"), "AppDomain");

        var log = File.ReadAllText(errors.LogFile);
        Assert.Contains("Dispatcher: System.InvalidOperationException: boom", log);
        Assert.Contains("System.IO.IOException: inner", log);
        Assert.Contains(nameof(Thrown), log);
        Assert.Contains("AppDomain: System.InvalidOperationException: background", log);
    }

    [Fact]
    public void Only_one_message_at_a_time()
    {
        var errors = Create();

        Assert.True(errors.Record(Thrown("a"), "Dispatcher", Start));
        Assert.False(errors.Record(Thrown("b"), "Dispatcher", Start.AddSeconds(1)));
        errors.Closed();
        Assert.True(errors.Record(Thrown("c"), "Dispatcher", Start.AddSeconds(2)));
    }

    [Fact]
    public void Same_exception_is_shown_once_per_minute()
    {
        var errors = Create();
        var exception = Thrown("same");

        Assert.True(errors.Record(exception, "Dispatcher", Start));
        errors.Closed();
        Assert.False(errors.Record(exception, "Dispatcher", Start.AddSeconds(30)));
        Assert.True(errors.Record(exception, "Dispatcher", Start.AddMinutes(1)));
    }

    [Fact]
    public void At_most_three_messages_per_minute()
    {
        var errors = Create();

        for (var i = 0; i < UnexpectedErrors.MaxPerMinute; i++)
        {
            Assert.True(errors.Record(Thrown("e" + i), "Dispatcher", Start.AddSeconds(i)));
            errors.Closed();
        }

        Assert.False(errors.Record(Thrown("more"), "Dispatcher", Start.AddSeconds(10)));
        Assert.True(errors.Record(Thrown("later"), "Dispatcher", Start.AddSeconds(61)));
        Assert.Equal(5, File.ReadAllText(errors.LogFile).Split("Dispatcher: ").Length - 1);
    }
}
