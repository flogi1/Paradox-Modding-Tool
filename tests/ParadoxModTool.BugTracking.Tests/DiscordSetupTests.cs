using Xunit;

namespace ParadoxModTool.BugTracking.Tests;

public class DiscordSetupTests
{
    private const string Guild = "100";
    private const string Bot = "42";

    private static DiscordOverwrite Role(string id, ulong allow = 0, ulong deny = 0) => new(id, 0, allow, deny);

    [Fact]
    public void Rechte_Reihenfolge_EveryoneRolleMitglied()
    {
        var roles = new[] { new DiscordRole(Guild, DiscordPermissions.ViewChannel | DiscordPermissions.ReadMessageHistory), new DiscordRole("7", DiscordPermissions.ManageThreads) };

        var everyoneDenied = DiscordPermissions.Compute(Guild, Bot, roles, new[] { "7" }, new[] { Role(Guild, deny: DiscordPermissions.ViewChannel) });
        var roleAllows = DiscordPermissions.Compute(Guild, Bot, roles, new[] { "7" }, new[] { Role(Guild, deny: DiscordPermissions.ViewChannel), Role("7", allow: DiscordPermissions.ViewChannel) });
        var memberDenies = DiscordPermissions.Compute(Guild, Bot, roles, new[] { "7" }, new[] { Role("7", allow: DiscordPermissions.ViewChannel), new DiscordOverwrite(Bot, 1, 0, DiscordPermissions.ViewChannel) });

        Assert.Equal(new[] { DiscordPermissions.ViewChannel }, DiscordPermissions.Missing(everyoneDenied));
        Assert.Empty(DiscordPermissions.Missing(roleAllows));
        Assert.Equal(new[] { DiscordPermissions.ViewChannel }, DiscordPermissions.Missing(memberDenies));
        Assert.Equal(new[] { DiscordPermissions.ManageThreads }, DiscordPermissions.Missing(DiscordPermissions.Compute(Guild, Bot, roles, Array.Empty<string>(), Array.Empty<DiscordOverwrite>())));
    }

    [Fact]
    public void Rechte_AdministratorHatAlles()
    {
        var roles = new[] { new DiscordRole(Guild, 0), new DiscordRole("9", DiscordPermissions.Administrator) };

        var perms = DiscordPermissions.Compute(Guild, Bot, roles, new[] { "9" }, new[] { Role(Guild, deny: ulong.MaxValue) });

        Assert.Empty(DiscordPermissions.Missing(perms));
    }

    [Fact]
    public void Einladungslink_MitGenauDenNoetigenRechten()
    {
        Assert.Equal(17179935744UL, DiscordPermissions.Required);
        Assert.Equal("https://discord.com/oauth2/authorize?client_id=555&scope=bot&permissions=17179935744", DiscordPermissions.InviteUrl("555"));
    }

    [Fact]
    public async Task Pruefung_AllesDa()
    {
        var http = new FakeHttpHandler();
        http.On("GET", "users/@me", 200, """{"id":"42","username":"PMT-Bot"}""");
        http.On("GET", "applications/@me", 200, """{"id":"555","flags":524288}""");
        http.On("GET", "users/@me/guilds", 200, """[{"id":"100","name":"Meine Mod"}]""");

        var result = await DiscordSetup.CheckAsync(new DiscordClient(new HttpClient(http), "t"), default);

        Assert.True(result.TokenValid);
        Assert.Equal("PMT-Bot", result.BotName);
        Assert.True(result.IntentEnabled);
        Assert.Equal(DiscordPermissions.InviteUrl("555"), result.InviteUrl);
        Assert.Equal(new[] { "Meine Mod" }, result.Servers);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Pruefung_TokenUngueltig_UndIntentAus()
    {
        var bad = new FakeHttpHandler();
        bad.On("GET", "users/@me", 401, """{"message":"401: Unauthorized"}""");
        var noIntent = new FakeHttpHandler();
        noIntent.On("GET", "users/@me", 200, """{"id":"42","username":"PMT-Bot"}""");
        noIntent.On("GET", "applications/@me", 200, """{"id":"555","flags":0}""");
        noIntent.On("GET", "users/@me/guilds", 200, "[]");

        var invalid = await DiscordSetup.CheckAsync(new DiscordClient(new HttpClient(bad), "t"), default);
        var off = await DiscordSetup.CheckAsync(new DiscordClient(new HttpClient(noIntent), "t"), default);

        Assert.False(invalid.TokenValid);
        Assert.Null(invalid.Error);
        Assert.True(off.TokenValid);
        Assert.False(off.IntentEnabled);
        Assert.Empty(off.Servers);
    }

    [Fact]
    public async Task FehlendeRechte_UeberRollenUndChannel()
    {
        var http = new FakeHttpHandler();
        http.On("GET", "users/@me", 200, """{"id":"42","username":"PMT-Bot"}""");
        http.On("GET", "guilds/100/roles", 200, """[{"id":"100","permissions":"66560"}]""");
        http.On("GET", "guilds/100/members/42", 200, """{"roles":[]}""");
        var forum = new DiscordChannel("222", "100", string.Empty, "bugs", 15, Array.Empty<DiscordTag>(), Array.Empty<string>(), Array.Empty<DiscordOverwrite>(), false, string.Empty, string.Empty);

        var missing = await DiscordSetup.MissingPermissionsAsync(new DiscordClient(new HttpClient(http), "t"), forum, default);

        Assert.Equal(new[] { DiscordPermissions.ManageThreads }, missing);
    }
}
