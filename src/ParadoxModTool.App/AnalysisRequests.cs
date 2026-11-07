using System.IO;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.GfxAudit;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.App;

/// <summary>
/// Gemeinsame Anfrage-Fabriken für Flag-Tracker, Skript-Prüfung und Verweisbaum: Momentaufnahme der Host-Werte im UI-Thread; die
/// zurückgegebene Funktion läuft im Hintergrund und liest dort nur noch die installierten Mods und die Dateien.
/// </summary>
internal static class AnalysisRequests
{
    /// <summary>
    /// Anfrage für den Flag-Tracker. Playset-Name, aktive Mod und Befehlstabelle bestimmt der Aufrufer (er merkt sie sich für spätere
    /// Vergleiche); hier werden eigene Mods, Playset-Einträge, Spielordner und Profil im UI-Thread gelesen.
    /// </summary>
    public static Func<FlagTrackerRequest> Flags(FlagTrackerWindowHost host, bool isPlayset, string? playsetName, string? activeModDirectory, FlagCommandTable? table)
    {
        var mods = host.Mods;
        var own = mods.OwnMods();
        var playset = mods.PlaysetEntries(playsetName);
        var installDirectory = host.InstallDirectory();
        var contentRoot = host.ContentRoot();
        var profile = host.Profile;
        var storeDirectory = Path.Combine(host.GameDirectory, "flag-tracker");

        return () =>
        {
            var modSources = ErrorLogSources.BuildMods(own, playset, mods.InstalledMods());
            var vanillaRoots = ErrorLogSources.VanillaRoots(installDirectory, contentRoot);
            var active = activeModDirectory ?? "";
            var sources = isPlayset
                ? FlagSourceSet.ForPlayset(profile, modSources, active, vanillaRoots)
                : FlagSourceSet.ForAllInstalled(profile, modSources, active, vanillaRoots);
            var commandTable = table ?? throw new InvalidOperationException(FlagTrackerTexts.NotAvailable);
            var notes = new List<string>(3);
            if (isPlayset && playsetName is not null)
            {
                notes.Add(FlagTrackerTexts.PlaysetNote(playsetName));
            }

            if (activeModDirectory is null)
            {
                notes.Add(FlagTrackerTexts.NoActiveMod);
            }

            if (vanillaRoots.Count == 0)
            {
                notes.Add(FlagTrackerTexts.NoGameFolder);
            }

            return new FlagTrackerRequest(sources, commandTable, new FlagIndexStore(storeDirectory), notes);
        };
    }

    /// <summary>
    /// Anfrage für die Skript-Prüfung der gewählten Mod (<paramref name="chosenModDirectory"/>, "" = keine). Die eingebauten Befehle werden
    /// hier im UI-Thread aus der Script-Doku gelesen.
    /// </summary>
    public static Func<ScriptCheckRequest> Checks(ScriptCheckWindowHost host, string chosenModDirectory, string? playsetName)
        => Checks(host, chosenModDirectory, playsetName, ScriptCheckBuiltIns.FromDatabase(host.ScriptDocs()));

    /// <summary>
    /// Anfrage für die GFX-Prüfung der gewählten Mod (<paramref name="chosenModDirectory"/>, "" = keine); Aufbau wie <c>Checks</c>, Zwischenspeicher
    /// in <c>&lt;Spielordner&gt;/gfx-audit</c>. Die Momentaufnahme entsteht im UI-Thread, die zurückgegebene Funktion läuft im Hintergrund.
    /// </summary>
    public static Func<GfxAuditRequest> Gfx(GfxAuditWindowHost host, string chosenModDirectory, string? playsetName)
    {
        var mods = host.Mods;
        var own = mods.OwnMods();
        var playset = mods.PlaysetEntries(playsetName);
        var installDirectory = host.InstallDirectory();
        var contentRoot = host.ContentRoot();
        var profile = host.Profile;
        var rules = host.Rules;
        var storeDirectory = Path.Combine(host.GameDirectory, "gfx-audit");

        return () =>
        {
            var modSources = ErrorLogSources.BuildMods(own, playset, mods.InstalledMods());
            var vanillaRoots = ErrorLogSources.VanillaRoots(installDirectory, contentRoot);
            var notes = new List<string>(2);
            if (playsetName is not null)
            {
                notes.Add(FlagTrackerTexts.PlaysetNote(playsetName));
            }

            if (vanillaRoots.Count == 0)
            {
                notes.Add(GfxAuditTexts.NoGameFolder);
            }

            return new GfxAuditRequest(FlagSourceSet.ForPlayset(profile, modSources, chosenModDirectory, vanillaRoots), rules, new GfxAuditStore(storeDirectory), notes);
        };
    }

    /// <summary>
    /// Anfrage für die Leistungsprüfung eines fremden Ziels (Vanilla, Playset-Mod, installierte Mod): nur Leistungsregeln, alle Namen, eigener
    /// Zwischenspeicher in <c>&lt;Spielordner&gt;/script-check/performance</c> (verdrängt den der eigenen Mods nicht; hält die letzten 5 Ziele). Aufbau siehe
    /// <see cref="PerformanceTarget.Request"/>. Die Momentaufnahme entsteht im UI-Thread, die zurückgegebene Funktion läuft im Hintergrund.
    /// </summary>
    public static Func<ScriptCheckRequest> Performance(ScriptCheckWindowHost host, PerformanceTarget target, string? playsetName)
    {
        var mods = host.Mods;
        var own = mods.OwnMods();
        var playset = mods.PlaysetEntries(playsetName);
        var installDirectory = host.InstallDirectory();
        var contentRoot = host.ContentRoot();
        var profile = host.Profile;
        var rules = host.Rules;
        var storeDirectory = Path.Combine(host.GameDirectory, "script-check", "performance");

        return () =>
        {
            var modSources = ErrorLogSources.BuildMods(own, playset, mods.InstalledMods());
            var vanillaRoots = ErrorLogSources.VanillaRoots(installDirectory, contentRoot);
            return PerformanceTarget.Request(target, profile, modSources, playsetName, vanillaRoots, rules, new ScriptCheckStore(storeDirectory, maxFiles: 5));
        };
    }

    /// <summary>Wie <see cref="Checks(ScriptCheckWindowHost, string, string?)"/>, mit schon gelesenen eingebauten Befehlen.</summary>
    public static Func<ScriptCheckRequest> Checks(ScriptCheckWindowHost host, string chosenModDirectory, string? playsetName, IReadOnlySet<string> builtIns)
    {
        var mods = host.Mods;
        var own = mods.OwnMods();
        var playset = mods.PlaysetEntries(playsetName);
        var installDirectory = host.InstallDirectory();
        var contentRoot = host.ContentRoot();
        var profile = host.Profile;
        var rules = host.Rules;
        var storeDirectory = Path.Combine(host.GameDirectory, "script-check");

        return () =>
        {
            var modSources = ErrorLogSources.BuildMods(own, playset, mods.InstalledMods());
            var vanillaRoots = ErrorLogSources.VanillaRoots(installDirectory, contentRoot);
            var notes = new List<string>(2);
            if (playsetName is not null)
            {
                notes.Add(FlagTrackerTexts.PlaysetNote(playsetName));
            }

            if (vanillaRoots.Count == 0)
            {
                notes.Add(FlagTrackerTexts.NoGameFolder);
            }

            return new ScriptCheckRequest(FlagSourceSet.ForPlayset(profile, modSources, chosenModDirectory, vanillaRoots), rules, new ScriptCheckStore(storeDirectory), builtIns, notes);
        };
    }
}
