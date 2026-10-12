using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class LocalWorkshopScannerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-local-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private void Item(string folderName, string? json, string library = "lib", bool bom = false)
    {
        var folder = Path.Combine(_dir, library, "steamapps", "workshop", "content", "3450310", folderName, ".metadata");
        Directory.CreateDirectory(folder);
        if (json is not null)
        {
            File.WriteAllText(Path.Combine(folder, "metadata.json"), (bom ? "﻿" : string.Empty) + json);
        }
    }

    private IReadOnlyList<WorkshopMatch> Find(string name, string? modId = null, params string[] libraries) =>
        LocalWorkshopScanner.Find(
            (libraries.Length == 0 ? new[] { "lib" } : libraries).Select(l => Path.Combine(_dir, l)),
            "3450310",
            ".metadata/metadata.json",
            name,
            modId);

    [Fact]
    public void Findet_ueberDenNamen_OhneGrossKleinschreibungUndMitBom()
    {
        Item("3599282262", "{ \"name\": \"The Idea Variation 2\", \"id\": \"Idea_Variation_2\" }", bom: true);
        Item("3600104346", "{ \"name\": \"No Hour Ticks\", \"id\": \"no.hour.ticks\" }");

        var matches = Find("the idea  variation 2");

        var match = Assert.Single(matches);
        Assert.Equal("3599282262", match.Id);
        Assert.Equal("The Idea Variation 2", match.Title);
        Assert.Contains("lokal", match.Source);
    }

    [Fact]
    public void Findet_ueberDieModId_AuchBeiAnderemNamen()
    {
        Item("111", "{ \"name\": \"Anderer Name\", \"id\": \"Idea_Variation_2\" }");

        Assert.Equal("111", Assert.Single(Find("Egal", "idea_variation_2")).Id);
        Assert.Empty(Find("Egal", ""));
        Assert.Empty(Find("Egal", null));
    }

    [Fact]
    public void Ueberspringt_NichtnumerischeOrdner_KaputteUndFehlendeMetadaten()
    {
        Item("abc", "{ \"name\": \"X\" }");
        Item("222", "{ kaputt");
        Item("333", null);

        Assert.Empty(Find("X"));
    }

    [Fact]
    public void Sucht_inAllenBibliotheken_UndLiefertJedeIdEinmal()
    {
        Item("444", "{ \"name\": \"X\" }", "libA");
        Item("555", "{ \"name\": \"X\" }", "libB");
        Item("444", "{ \"name\": \"X\" }", "libB");

        var matches = Find("X", null, "libA", "libB");

        Assert.Equal(new[] { "444", "555" }, matches.Select(m => m.Id).OrderBy(i => i, StringComparer.Ordinal));
    }

    [Fact]
    public void FehlenderInhaltsOrdner_ErgibtNichts()
    {
        Assert.Empty(Find("X", null, "gibtsnicht"));
    }

    [Fact]
    public void Klassisches_Format_liest_die_descriptor_mod_im_Workshop_Ordner()
    {
        void Classic(string id, string descriptor)
        {
            var folder = Path.Combine(_dir, "lib", "steamapps", "workshop", "content", "236850", id);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "descriptor.mod"), descriptor);
        }

        Classic("1703319446", "name=\"The Idea Variation Beyond Typus Addon\"\ntags={\n\t\"Addon\"\n}\nremote_file_id=\"1703319446\"");
        Classic("1385440355", "name=\"Anbennar: A Fantasy Total Conversion\"\n");

        var matches = LocalWorkshopScanner.Find(new[] { Path.Combine(_dir, "lib") }, "236850", "*.mod", "the idea variation beyond typus addon", null);

        var match = Assert.Single(matches);
        Assert.Equal("1703319446", match.Id);
        Assert.Equal("The Idea Variation Beyond Typus Addon", match.Title);
    }
}

public class WorkshopProfilePageTests
{
    private const string Page =
        "<div class=\"workshopItem\"><a href=\"https://steamcommunity.com/sharedfiles/filedetails/?id=3599735023\" class=\"ugc\" data-publishedfileid=\"3599735023\"></a>" +
        "<div class=\"workshopItemTitle ellipsis\">The Idea Variation 2</div></div>\n" +
        "<script>SharedFileBindMouseHover( \"sharedfile_3599735023\", false, {\"id\":\"3599735023\",\"title\":\"The Idea Variation 2 \\u2013 \\\"Beta\\\" &amp; more\",\"description\":\"x {y} z\",\"appid\":3450310} );</script>\n" +
        "<script>SharedFileBindMouseHover( \"sharedfile_3599737490\", false, {\"id\":\"3599737490\",\"title\":\"Generic Conquest CB\",\"description\":\"\",\"appid\":3450310} );</script>\n" +
        "<script>SharedFileBindMouseHover( \"sharedfile_3599735023\", false, {\"id\":\"3599735023\",\"title\":\"Doppelt\",\"description\":\"\",\"appid\":3450310} );</script>";

    [Fact]
    public void Parse_LiestIdUndTitelAusDemJson_DekodiertEscapesUndEntitaeten_UndEntferntDoppelte()
    {
        var items = WorkshopProfilePage.Parse(Page);

        Assert.Equal(new[] { "3599735023", "3599737490" }, items.Select(i => i.Id));
        Assert.Equal("The Idea Variation 2 – \"Beta\" & more", items[0].Title);
        Assert.Equal("Generic Conquest CB", items[1].Title);
        Assert.All(items, i => Assert.Contains("Profil", i.Source));
    }

    [Fact]
    public void Parse_OhneJsonBloecke_FaelltAufDieHtmlElementeZurueck()
    {
        var html = "<a data-publishedfileid=\"111\"></a><div class=\"workshopItemTitle ellipsis\">Erster &amp; Zweiter</div>" +
                   "<a data-publishedfileid=\"222\"></a><div class=\"workshopItemTitle ellipsis\">Dritter</div>";

        var items = WorkshopProfilePage.Parse(html);

        Assert.Equal(new[] { ("111", "Erster & Zweiter"), ("222", "Dritter") }, items.Select(i => (i.Id, i.Title)));
    }

    [Fact]
    public void Parse_LeereOderFremdeSeite_ErgibtNichts()
    {
        Assert.Empty(WorkshopProfilePage.Parse(""));
        Assert.Empty(WorkshopProfilePage.Parse("<html>Dieses Profil ist privat.</html>"));
    }

    [Fact]
    public void Url_EnthaeltSteamIdAppIdUndSeite()
    {
        Assert.Equal("https://steamcommunity.com/profiles/76561198006673200/myworkshopfiles/?appid=3450310&p=2", WorkshopProfilePage.Url("76561198006673200", "3450310", 2));
    }
}

public class WorkshopProfileClientTests
{
    private static string PageWith(params string[] ids) =>
        string.Concat(ids.Select(id => $"<script>SharedFileBindMouseHover( \"sharedfile_{id}\", false, {{\"id\":\"{id}\",\"title\":\"T{id}\",\"description\":\"\",\"appid\":1}} );</script>"));

    [Fact]
    public async Task ListAsync_HoltSeitenBisKeineNeuenEintraegeKommen()
    {
        var urls = new List<string>();
        Task<string> Fetch(string url, CancellationToken token)
        {
            urls.Add(url);
            return Task.FromResult(url.EndsWith("p=1") ? PageWith("1", "2") : url.EndsWith("p=2") ? PageWith("3") : PageWith("3"));
        }

        var items = await WorkshopProfileClient.ListAsync("76561198006673200", "3450310", Fetch, default);

        Assert.Equal(new[] { "1", "2", "3" }, items.Select(i => i.Id));
        Assert.Equal(3, urls.Count);
        Assert.EndsWith("p=1", urls[0]);
    }

    [Fact]
    public async Task ListAsync_HoertNachDerHoechstzahlAnSeitenAuf()
    {
        var calls = 0;
        Task<string> Fetch(string url, CancellationToken token)
        {
            calls++;
            return Task.FromResult(PageWith((calls * 10).ToString()));
        }

        var items = await WorkshopProfileClient.ListAsync("76561198006673200", "3450310", Fetch, default, maxPages: 3);

        Assert.Equal(3, calls);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task ListAsync_LeereErsteSeite_ErgibtNichts_UndFehlerWerdenDurchgereicht()
    {
        Assert.Empty(await WorkshopProfileClient.ListAsync("76561198006673200", "3450310", (_, _) => Task.FromResult("<html/>"), default));
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            WorkshopProfileClient.ListAsync("76561198006673200", "3450310", (_, _) => throw new HttpRequestException("kein Netz"), default));
    }

    [Fact]
    public async Task ListAsync_UngueltigeIds_WerfenArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => WorkshopProfileClient.ListAsync("abc", "3450310", (_, _) => Task.FromResult(""), default));
        await Assert.ThrowsAsync<ArgumentException>(() => WorkshopProfileClient.ListAsync("76561198006673200", "x", (_, _) => Task.FromResult(""), default));
    }
}

public class WorkshopMatcherTests
{
    private static WorkshopMatch M(string id, string title) => new(id, title, "test");

    [Fact]
    public void Exact_VergleichtOhneGrossKleinschreibungUndMitZusammengefasstenLeerzeichen()
    {
        var candidates = new[] { M("1", "The  Idea Variation 2"), M("2", "The Idea Variation Testversion (Beta)"), M("3", "the idea variation 2 ") };

        Assert.Equal(new[] { "1", "3" }, WorkshopMatcher.Exact(candidates, " THE IDEA VARIATION 2").Select(m => m.Id));
        Assert.Empty(WorkshopMatcher.Exact(candidates, "Anderes"));
        Assert.Empty(WorkshopMatcher.Exact(candidates, ""));
    }
}

public class WorkshopIdTextTests
{
    [Theory]
    [InlineData("3456789012", "3456789012")]
    [InlineData("  3456789012 ", "3456789012")]
    [InlineData("https://steamcommunity.com/sharedfiles/filedetails/?id=3456789012", "3456789012")]
    [InlineData("https://steamcommunity.com/sharedfiles/filedetails/?searchtext=&id=3456789012&x=1", "3456789012")]
    [InlineData("steamcommunity.com/workshop/filedetails/?id=42", "42")]
    [InlineData("0", "0")]
    public void Extract_LiefertDieZahl(string input, string expected)
    {
        Assert.Equal(expected, WorkshopIdText.Extract(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12ab")]
    [InlineData("https://example.org/page")]
    [InlineData("id=abc")]
    public void Extract_KeineZahlUndKeineWorkshopAdresse_IstNull(string input)
    {
        Assert.Null(WorkshopIdText.Extract(input));
    }
}
