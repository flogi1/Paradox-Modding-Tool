using System.Text.Json;
using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class PlaysetTextsTests
{
    private static readonly string[] Languages = { "de", "en", "fr", "es", "ru", "pl", "pt-br", "zh-hans" };

    private static string Locales()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ParadoxModTool.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "locales");
    }

    private static Dictionary<string, string> Load(string language) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(Locales(), language + ".json")).Replace("\"_machineTranslated\": true,", string.Empty), new JsonSerializerOptions { AllowTrailingCommas = true })!;

    public static IEnumerable<object[]> Statuses() =>
        Enum.GetValues<PlaysetStatus>().Where(s => s != PlaysetStatus.Ok).Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Statuses))]
    public void Every_status_has_a_message_in_every_language_and_the_german_fallback_matches(PlaysetStatus status)
    {
        var message = PlaysetTexts.Describe(new PlaysetResult(status, "DETAIL"));

        Assert.False(string.IsNullOrWhiteSpace(message));
        var key = "playset.status." + char.ToLowerInvariant(status.ToString()[0]) + status.ToString()[1..];
        foreach (var language in Languages)
        {
            Assert.True(Load(language).ContainsKey(key), $"{language}: {key}");
        }

        Assert.Equal(string.Format(Load("de")[key], "DETAIL"), message);
    }
}
