using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class WorkshopDetailsClientTests
{
    [Fact]
    public async Task BuildRequest_FieldNames()
    {
        var content = WorkshopDetailsClient.BuildRequest(new[] { "1", "22" });

        var body = await content.ReadAsStringAsync();
        Assert.Equal("itemcount=2&publishedfileids%5B0%5D=1&publishedfileids%5B1%5D=22", body);
    }

    [Fact]
    public void Parse_OkHiddenNotFound()
    {
        const string json = """
            {"response":{"result":1,"resultcount":3,"publishedfiledetails":[
              {"publishedfileid":"111","result":1,"title":"Idea Variation","preview_url":"https://steamuserimages-a.akamaihd.net/ugc/1/ABC/",
               "subscriptions":48213,"favorited":2104,"views":391550,"time_updated":1757635200,"visibility":0},
              {"publishedfileid":"222","result":1,"title":"Hidden","subscriptions":5,"visibility":2},
              {"publishedfileid":"333","result":9}
            ]}}
            """;

        var details = WorkshopDetailsClient.Parse(json);

        Assert.Equal(3, details.Count);
        var ok = details[0];
        Assert.Equal("111", ok.Id);
        Assert.Equal(WorkshopPageState.Ok, ok.State);
        Assert.Equal("Idea Variation", ok.Title);
        Assert.Equal("https://steamuserimages-a.akamaihd.net/ugc/1/ABC/", ok.PreviewUrl);
        Assert.Equal(48213, ok.Subscriptions);
        Assert.Equal(2104, ok.Favorited);
        Assert.Equal(391550, ok.Views);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1757635200), ok.TimeUpdated);
        Assert.Equal(WorkshopPageState.Hidden, details[1].State);
        Assert.Equal(WorkshopPageState.NotFound, details[2].State);
        Assert.Null(details[2].Subscriptions);
    }

    [Fact]
    public void Parse_ToleratesStringNumbersAndMissingFields()
    {
        var details = WorkshopDetailsClient.Parse("""
            {"response":{"publishedfiledetails":[{"publishedfileid":"1","result":1,"subscriptions":"17","visibility":"0"}]}}
            """);

        Assert.Equal(17, details.Single().Subscriptions);
        Assert.Null(details.Single().Views);
        Assert.Equal(WorkshopPageState.Ok, details.Single().State);
        Assert.Empty(WorkshopDetailsClient.Parse("{broken"));
        Assert.Empty(WorkshopDetailsClient.Parse("{\"response\":{}}"));
    }
}
