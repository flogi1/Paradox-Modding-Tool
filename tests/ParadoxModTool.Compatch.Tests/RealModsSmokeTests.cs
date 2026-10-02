using ParadoxModTool.GameProfiles;
using ParadoxModTool.ModCompat;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>
/// Rauchtest mit zwei echten Mods, nur lesend. Läuft nur, wenn die Umgebungsvariablen <c>PMT_COMPATCH_A</c> und <c>PMT_COMPATCH_B</c>
/// auf Mod-Ordner zeigen und EU V installiert ist; sonst kehrt er sofort zurück. Prüft Invarianten, keine festen Zahlen.
/// </summary>
public class RealModsSmokeTests
{
    private const string Eu5Game = @"C:\Program Files (x86)\Steam\steamapps\common\Europa Universalis V\game";

    private readonly ITestOutputHelper _output;

    public RealModsSmokeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Real_pair_runs_deterministically_and_writes_nothing()
    {
        var a = Environment.GetEnvironmentVariable("PMT_COMPATCH_A");
        var b = Environment.GetEnvironmentVariable("PMT_COMPATCH_B");
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || !Directory.Exists(a) || !Directory.Exists(b) || !Directory.Exists(Eu5Game))
        {
            return;
        }

        var profile = GameProfileLoader.LoadFromFile(CompatchTestBase.RepoFile("profiles/eu5.json"));
        var input = new CompatchInput
        {
            VanillaFolder = Eu5Game,
            ModAFolder = a,
            ModBFolder = b,
            Rules = OverrideRules.FromProfile(profile),
            KeyOperators = profile.KeyOperators,
        };
        var before = Stamp(a) + Stamp(b);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var resolved = EffectiveEntryResolver.Resolve(input);
        var tResolve = watch.Elapsed;
        var actions = CompatchMerger.Decide(resolved, input);
        var tDecide = watch.Elapsed;
        var applied = DecisionApplier.Apply(resolved, actions, null, input);
        var tApply = watch.Elapsed;
        PatchPlanner.Plan(resolved, applied, input, PatchTarget.Compatch);
        var tPlan = watch.Elapsed;
        _output.WriteLine($"Phasen: Lesen/Auflösen {tResolve.TotalSeconds:F1} s, Entscheiden {(tDecide - tResolve).TotalSeconds:F1} s, Anwenden {(tApply - tDecide).TotalSeconds:F1} s, Planen {(tPlan - tApply).TotalSeconds:F1} s");

        watch.Restart();
        var first = CompatchEngine.Run(input, PatchTarget.Compatch);
        var second = CompatchEngine.Run(input, PatchTarget.Compatch);

        _output.WriteLine($"Dauer (2 weitere Läufe, Zwischenspeicher je Lauf neu): {watch.Elapsed}, Adressen: {first.Resolved.Addresses.Count}, Aktionen: {first.Actions.Count}");
        foreach (var group in first.Actions.GroupBy(x => x.Stage))
        {
            _output.WriteLine($"  {group.Key}: {group.Count()}");
        }

        Assert.Equal(first.Actions.Select(x => x.Id), second.Actions.Select(x => x.Id));
        Assert.Equal(first.Actions.Count, first.Actions.Select(x => x.Id).Distinct().Count());
        Assert.All(first.Plan.Entries, op => Assert.False(Path.IsPathRooted(op.TargetFile) || op.TargetFile.Contains("..")));
        Assert.All(first.Plan.Files, op => Assert.False(Path.IsPathRooted(op.RelativePath) || op.RelativePath.Contains("..")));
        Assert.Empty(first.Applied.Errors);
        Assert.Equal(before, Stamp(a) + Stamp(b));
    }

    /// <summary>
    /// EU IV: Läuft nur mit <c>PMT_COMPATCH_EU4_A</c>/<c>PMT_COMPATCH_EU4_B</c>. Kein Schlüssel darf ein gemeinsamer Blockname sein
    /// (<c>country_event</c>, <c>country_decisions</c> …); sonst stünden verschieden benannte Dateien fälschlich in Konflikt.
    /// </summary>
    [Fact]
    public void Real_eu4_pair_has_no_keys_named_after_shared_blocks()
    {
        var a = Environment.GetEnvironmentVariable("PMT_COMPATCH_EU4_A");
        var b = Environment.GetEnvironmentVariable("PMT_COMPATCH_EU4_B");
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || !Directory.Exists(a) || !Directory.Exists(b) || !Directory.Exists(Eu4Game))
        {
            return;
        }

        var profile = GameProfileLoader.LoadFromFile(CompatchTestBase.RepoFile("profiles/eu4.json"));
        var input = new CompatchInput { VanillaFolder = Eu4Game, ModAFolder = a, ModBFolder = b, Rules = OverrideRules.FromProfile(profile) };
        var resolved = EffectiveEntryResolver.Resolve(input);
        var keys = resolved.Addresses.Keys.Where(k => k.Kind == AddressKind.Key).ToList();
        _output.WriteLine($"Schlüssel-Adressen: {keys.Count}");
        foreach (var group in keys.GroupBy(k => k.Scope).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            _output.WriteLine($"  {group.Key}: {group.Count()} ({string.Join(", ", group.Take(5).Select(k => k.Key))})");
        }

        var shared = new[] { "country_event", "province_event", "country_decisions", "guiTypes", "spriteTypes", "objectTypes", "defined_text", "custom_button", "custom_icon", "custom_text_box", "custom_window", "custom_shield" };
        Assert.DoesNotContain(keys, k => shared.Contains(k.Key));
    }

    private const string Eu4Game = @"D:\SteamLibrary\steamapps\common\Europa Universalis IV";

    private static string Stamp(string folder) => string.Join("|", Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
        .OrderBy(f => f, StringComparer.Ordinal)
        .Select(f => f + ":" + File.GetLastWriteTimeUtc(f).Ticks));
}
