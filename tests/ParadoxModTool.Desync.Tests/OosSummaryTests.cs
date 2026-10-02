using System.Text;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class OosSummaryTests
{
    private const string Env = "[jominiapplication.cpp:571]: ";
    private const string FullId = "11c0de175691b2bbbb7330e63084ac66";
    private const string Steam = "C:/Program Files (x86)/Steam/steamapps";

    /// <summary>Metadatenblock wie im echten OOS-Save, samt Flaggen-Text mit eingerückten Zeilen.</summary>
    private static string SaveText(string date = "1425.2.4.8", string dlcs = "\"Ancient Monument Pack\" \"Shared DLC Data\"",
        long randomCount = 68262752) =>
        "metadata={\n" +
        "\tmultiplayer=yes\n" +
        $"\tdate={date}\n" +
        "\tplaythrough_id=\"fabde222-8d22-4baf-9d1d-0b4741cd400f\"\n" +
        "\tsave_label=\"Autosave\"\n" +
        "\tversion=\"1.0.3\"\n" +
        "\tincompatible=yes\n" +
        "\tflag=\"POR={\n" +
        "\tpattern=\\\"pattern_solid.dds\\\"\n" +
        "\tcolor1=white\n" +
        "\tdate=9999.9.9.9\n" +
        "\tversion=\"0.0.1\"\n" +
        "}\"\n" +
        "\tcode_version_info={\n" +
        "\t\tgame_code_info={\n" +
        "\t\t\tcode_hash_short=\"f2c15314d\"\n" +
        "\t\t}\n" +
        "\t}\n" +
        $"\tenabled_dlcs={{ {dlcs} }}\n" +
        "\tcompatibility={\n" +
        "\t\tversion=1\n" +
        "\t}\n" +
        "}\n" +
        "start_of_day=1425.2.4\n" +
        "current_age=age_2_renaissance\n" +
        "speed=2\n" +
        "random_seed=293541372\n" +
        $"random_count={randomCount}\n" +
        "variables={\n" +
        "\tdata={ {\n" +
        "\t\t\tflag=marbled_emperor\n";

    private static string DebugLog(string frames = "OOSFrame: 50663\nStartFrame: 50653") =>
        $"[21:07:03][dlc_reloadable.cpp:333]: Mod Better Tactical Map Mode () version 1.0.* successfully matched game version 1.0.3.\n" +
        $"[21:07:07][virtualfilesystem_physfs.cpp:596]: Mounted Data: {Steam}/workshop/content/3450310/3599740945/main_menu\n" +
        $"[21:07:10][virtualfilesystem_physfs.cpp:596]: Mounted Data: {Steam}/workshop/content/3450310/3599740945/main_menu\n" +
        $"[21:07:10][virtualfilesystem_physfs.cpp:596]: Mounted Data: {Steam}/workshop/content/3450310/3599740945/in_game\n" +
        string.Join("\n", frames.Split('\n').Select(f => "[22:47:15][synchronizationmanager.cpp:454]: " + f)) + "\n" +
        "[22:47:15][game_synchronization_manager.cpp:544]: WriteOOSDumpDerived started\n";

    private static string ServerLog(string role, int machine) =>
        $"[22:47:12]{Env}{role}_{machine} Tick: 1425.2.3.22\n" +
        $"[22:47:13]{Env}{role}_{machine} Tick: 1425.2.4.8\n" +
        $"[22:47:13]{Env}OOS Detected! OOS ID: {FullId}\n";

    private static (DumpFileKind, string)[] Files(string role, int machine, string saveText, string? debugLog = null) =>
    [
        (DumpFileKind.Debug, debugLog ?? DebugLog()),
        (DumpFileKind.DedicatedServer, ServerLog(role, machine)),
        (DumpFileKind.OosSave, saveText),
    ];

    private static (OosPair Pair, List<OosDump> Dumps) Open(TempTree tree, params string[] folders)
    {
        var infos = OosDumpCatalog.Scan([tree.Root], []);
        var pair = Assert.Single(OosDumpCatalog.Pair(infos));
        var dumps = folders.Select(f => OosDump.Open(tree.PathOf(f))).OrderBy(d => d.MachineId).ToList();
        return (pair, dumps);
    }

    private static OosSummary BuildFor(TempTree tree, params string[] folders)
    {
        var (pair, dumps) = Open(tree, folders);
        try
        {
            return OosSummaryBuilder.Build(pair, dumps);
        }
        finally
        {
            dumps.ForEach(d => d.Dispose());
        }
    }

    private static TempTree TwoMachines(
        string? hostSave = null, string? clientSave = null,
        string? clientMetadata = null, string? hostDebug = null, string? clientDebug = null)
    {
        var tree = new TempTree();
        Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0, Files("Host", 1, hostSave ?? SaveText(), hostDebug));
        Dumps.Folder(tree, "client", 2, clientMetadata ?? Dumps.ClientMetadata11c0,
            Files("Client", 2, clientSave ?? SaveText(), clientDebug));
        return tree;
    }

    [Fact]
    public void A_matched_mod_takes_its_workshop_source_from_the_mount_lines()
    {
        var mods = OosSummaryBuilder.ModsFromDebugLog(DebugLog().Split('\n'));

        var mod = Assert.Single(mods);
        Assert.Equal("Better Tactical Map Mode", mod.Name);
        Assert.Equal(ModSourceKind.Workshop, mod.Kind);
        Assert.Equal("3599740945", mod.WorkshopId);
        Assert.Equal(Steam + "/workshop/content/3450310/3599740945", mod.Root);
    }

    [Fact]
    public void Without_a_mod_block_only_matched_mods_are_listed_and_a_mount_gives_their_local_folder()
    {
        var lines = new[]
        {
            "[21:07:03][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Program Files (x86)/Steam/steamapps/common/Europa Universalis V/game/mod",
            "[21:07:03][dlc_reloadable.cpp:333]: Mod Flogi Rules () version 1.0.* successfully matched game version 1.0.3.",
            "[21:07:07][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/flogi_rules/main_menu",
            "[21:07:07][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/unnamed_mod/main_menu",
            "[21:07:07][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/flogi_rules/in_game",
        };

        var mods = OosSummaryBuilder.ModsFromDebugLog(lines);

        // Ein Einhängen allein macht keinen Mod: unnamed_mod wurde nie "successfully matched".
        var mod = Assert.Single(mods);
        Assert.Equal("Flogi Rules", mod.Name);
        Assert.Equal(ModSourceKind.Local, mod.Kind);
        Assert.Null(mod.WorkshopId);
        Assert.Equal("flogi_rules", mod.Folder);
        Assert.EndsWith("/mod/flogi_rules", mod.Root);
    }

    // Auszüge aus den echten Debug-Logs des Paares 11c0 (Pfade anonymisiert). Der Host lädt die Playset-Mods nach einem leeren
    // DLC/Mod-Block; der Client wechselt das Playset und hängt dabei jeden installierten Mod ein, aktiv ist nur einer.
    private static readonly string HostModLog = """
        [21:07:03][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Program Files (x86)/Steam/steamapps/common/Europa Universalis V/game/dlc
        [21:07:03][dlc_reloadable.cpp:616]: DLC:
        Mod:

        [21:07:03][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Program Files (x86)/Steam/steamapps/common/Europa Universalis V/game/mod
        [21:07:03][dlc_reloadable.cpp:378]: Switching to playset index 1, Name: Initial playset
        [21:07:03][pdx_json_doc_serialize.cpp:98]: Skipped member: `picture : "thumbnail.png"`
        [21:07:03][dlc_reloadable.cpp:333]: Mod Better Tactical Map Mode () version 1.0.* successfully matched game version 1.0.3.
        [21:07:03][virtualfilesystem_physfs.cpp:604]: No subdirs mounted for game dir from candidates: C:/Program Files (x86)/Steam/steamapps/common/Europa Universalis V/game/dlc/D000_shared
        [21:07:07][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Program Files (x86)/Steam/steamapps/workshop/content/3450310/3599740945/main_menu
        [21:07:10][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Program Files (x86)/Steam/steamapps/workshop/content/3450310/3599740945/main_menu
        [21:07:10][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Program Files (x86)/Steam/steamapps/workshop/content/3450310/3599740945/in_game
        """.ReplaceLineEndings("\n");

    private static readonly string ClientModLog = """
        [21:05:07][virtualfilesystem_physfs.cpp:596]: Mounted Data: D:/SteamLibrary/steamapps/common/Europa Universalis V/game/dlc
        [21:05:07][dlc_reloadable.cpp:616]: DLC:
        Mod:

        [21:05:07][virtualfilesystem_physfs.cpp:596]: Mounted Data: D:/SteamLibrary/steamapps/common/Europa Universalis V/game/mod
        [21:05:07][dlc_reloadable.cpp:378]: Switching to playset index 3, Name: Map Mod
        [21:05:07][dlc_reloadable.cpp:333]: Mod Better Tactical Map Mode () version 1.0.* successfully matched game version 1.0.3.
        [21:05:08][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Better Tactical Map Mode/main_menu
        [21:05:09][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Better Tactical Map Mode/in_game
        [21:06:26][virtualfilesystem_physfs.cpp:596]: Mounted Data: D:/SteamLibrary/steamapps/common/Europa Universalis V/game/dlc
        [21:06:27][dlc_reloadable.cpp:616]: DLC:
        Ancient Monument Pack|D015_ancient_monuments_pack/D015_ancient_monuments_pack.dlc.json|Enabled
        Shared DLC Data|D000_shared/D000_shared.dlc.json|Enabled
        Sacred Sites|D017_sacred_sites_pack/D017_sacred_sites_pack.dlc.json|Enabled
        Mod:
        Better Tactical Map Mode|C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Better Tactical Map Mode/|Enabled

        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: D:/SteamLibrary/steamapps/common/Europa Universalis V/game/in_game
        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Better Tactical Map Mode/main_menu
        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Better Tactical Map Mode/in_game
        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Flogis Quick Fixes/in_game
        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Generic Conquest CB/main_menu
        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Generic Conquest CB/in_game
        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Idea Variation 2 Manpower Addon/in_game
        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/The Idea Variation 2/main_menu
        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/The Idea Variation 2/in_game
        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: D:/SteamLibrary/steamapps/common/Europa Universalis V/game/mod
        [21:06:27][virtualfilesystem_physfs.cpp:596]: Mounted Data: D:/SteamLibrary/steamapps/common/Europa Universalis V/game/dlc
        [21:06:27][dlc_reloadable.cpp:616]: DLC:
        Ancient Monument Pack|D015_ancient_monuments_pack/D015_ancient_monuments_pack.dlc.json|Enabled
        Shared DLC Data|D000_shared/D000_shared.dlc.json|Enabled
        Sacred Sites|D017_sacred_sites_pack/D017_sacred_sites_pack.dlc.json|Enabled
        Mod:
        Better Tactical Map Mode|C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Better Tactical Map Mode/|Enabled

        [21:06:39][pdx_transition_task.cpp:392]: Running OnTransitionStateChanged callback for state 3, transition: MainMenu->Game
        """.ReplaceLineEndings("\n");

    private static string WithFrames(string modLog) => modLog + "\n" + DebugLog();

    [Fact]
    public void The_real_host_log_lists_the_matched_mod_of_the_last_playset_with_its_workshop_id()
    {
        var mod = Assert.Single(OosSummaryBuilder.ModsFromDebugLog(HostModLog.Split('\n')));

        Assert.Equal("Better Tactical Map Mode", mod.Name);
        Assert.Equal(ModSourceKind.Workshop, mod.Kind);
        Assert.Equal("3599740945", mod.WorkshopId);
    }

    [Fact]
    public void The_real_client_log_lists_only_the_enabled_mods_of_the_last_mod_block_not_every_mounted_mod()
    {
        var mod = Assert.Single(OosSummaryBuilder.ModsFromDebugLog(ClientModLog.Split('\n')));

        Assert.Equal("Better Tactical Map Mode", mod.Name);
        Assert.Equal(ModSourceKind.Local, mod.Kind);
        Assert.Equal("Better Tactical Map Mode", mod.Folder);
        Assert.Null(mod.WorkshopId);
    }

    [Fact]
    public void The_real_11c0_pair_has_no_mod_warning_and_each_machine_shows_its_mod_source()
    {
        using var tree = TwoMachines(hostDebug: WithFrames(HostModLog), clientDebug: WithFrames(ClientModLog));

        var summary = BuildFor(tree, "host", "client");

        Assert.DoesNotContain(summary.Warnings, w => w.Kind == SummaryWarningKind.DifferentMods);
        Assert.Equal("Better Tactical Map Mode (Workshop 3599740945)", DesyncTexts.ModLabel(Assert.Single(summary.Machines[0].Mods)));
        Assert.Equal("Better Tactical Map Mode (lokaler Ordner)", DesyncTexts.ModLabel(Assert.Single(summary.Machines[1].Mods)));
    }

    [Fact]
    public void Different_enabled_mod_names_give_a_mod_warning_with_the_names()
    {
        var clientLog = ClientModLog.Replace(
            "Better Tactical Map Mode/|Enabled\n\n[21:06:39]",
            "Better Tactical Map Mode/|Enabled\nGeneric Conquest CB|C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/Generic Conquest CB/|Enabled\n\n[21:06:39]");
        Assert.NotEqual(ClientModLog, clientLog);
        using var tree = TwoMachines(hostDebug: WithFrames(HostModLog), clientDebug: WithFrames(clientLog));

        var summary = BuildFor(tree, "host", "client");

        var warning = Assert.Single(summary.Warnings, w => w.Kind == SummaryWarningKind.DifferentMods);
        Assert.Equal("Generic Conquest CB", warning.Detail);
        Assert.Equal(["Better Tactical Map Mode", "Generic Conquest CB"], summary.Machines[1].Mods.Select(m => m.Name));
    }

    [Fact]
    public void Disabled_mods_of_the_mod_block_are_not_listed()
    {
        var lines = new[]
        {
            "[21:06:27][dlc_reloadable.cpp:616]: DLC:",
            "Shared DLC Data|D000_shared/D000_shared.dlc.json|Enabled",
            "Mod:",
            "Old Mod|C:/Users/Player/Documents/Paradox Interactive/Europa Universalis V/mod/old_mod/|Disabled",
            "Workshop Mod|C:/Program Files (x86)/Steam/steamapps/workshop/content/3450310/42/|Enabled",
            "",
        };

        var mod = Assert.Single(OosSummaryBuilder.ModsFromDebugLog(lines));

        Assert.Equal("Workshop Mod", mod.Name);
        Assert.Equal(ModSourceKind.Workshop, mod.Kind);
        Assert.Equal("42", mod.WorkshopId);
    }

    [Fact]
    public void A_mod_name_starting_with_a_bracket_in_the_middle_of_the_mod_block_does_not_end_the_block()
    {
        var lines = new[]
        {
            "[21:06:27][dlc_reloadable.cpp:616]: DLC:",
            "Mod:",
            "Better Tactical Map Mode|C:/mods/mod/better_tactical_map_mode/|Enabled",
            "[EU5] Better Borders|C:/mods/mod/better_borders/|Enabled",
            "Third Mod|C:/mods/mod/third_mod/|Enabled",
            "",
        };

        var mods = OosSummaryBuilder.ModsFromDebugLog(lines);

        Assert.Equal(["Better Tactical Map Mode", "[EU5] Better Borders", "Third Mod"], mods.Select(m => m.Name));
    }

    [Fact]
    public void A_mod_name_starting_with_a_bracket_as_the_first_mod_line_is_listed()
    {
        var lines = new[]
        {
            "[21:06:27][dlc_reloadable.cpp:616]: DLC:",
            "Mod:",
            "[EU5] Better Borders|C:/mods/mod/better_borders/|Enabled",
            "Third Mod|C:/mods/mod/third_mod/|Enabled",
            "",
        };

        var mods = OosSummaryBuilder.ModsFromDebugLog(lines);

        Assert.Equal(["[EU5] Better Borders", "Third Mod"], mods.Select(m => m.Name));
    }

    [Fact]
    public void A_timestamped_log_line_still_ends_the_mod_block_without_a_blank_line()
    {
        var lines = new[]
        {
            "[21:06:27][dlc_reloadable.cpp:616]: DLC:",
            "Mod:",
            "[EU5] Better Borders|C:/mods/mod/better_borders/|Enabled",
            "[21:06:28][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/mods/mod/other/in_game",
            "Not A Mod Line|C:/mods/mod/not_a_mod/|Enabled",
        };

        var mod = Assert.Single(OosSummaryBuilder.ModsFromDebugLog(lines));

        Assert.Equal("[EU5] Better Borders", mod.Name);
    }

    [Fact]
    public void Two_machines_with_the_same_enabled_mods_get_no_mod_warning_when_one_name_starts_with_a_bracket()
    {
        var hostLog = """
            [21:07:03][dlc_reloadable.cpp:616]: DLC:
            Mod:
            Better Tactical Map Mode|C:/mods/mod/better_tactical_map_mode/|Enabled
            [EU5] Better Borders|C:/mods/mod/better_borders/|Enabled
            Third Mod|C:/mods/mod/third_mod/|Enabled

            """.ReplaceLineEndings("\n");
        var clientLog = """
            [21:05:07][dlc_reloadable.cpp:378]: Switching to playset index 3, Name: Map Mod
            [21:05:07][dlc_reloadable.cpp:333]: Mod Better Tactical Map Mode () version 1.0.* successfully matched game version 1.0.3.
            [21:05:07][dlc_reloadable.cpp:333]: Mod [EU5] Better Borders () version 1.0.* successfully matched game version 1.0.3.
            [21:05:07][dlc_reloadable.cpp:333]: Mod Third Mod () version 1.0.* successfully matched game version 1.0.3.
            """.ReplaceLineEndings("\n");
        using var tree = TwoMachines(hostDebug: WithFrames(hostLog), clientDebug: WithFrames(clientLog));

        var summary = BuildFor(tree, "host", "client");

        Assert.Equal(3, summary.Machines[0].Mods.Count);
        Assert.Equal(3, summary.Machines[1].Mods.Count);
        Assert.DoesNotContain(summary.Warnings, w => w.Kind == SummaryWarningKind.DifferentMods);
    }

    [Fact]
    public void The_last_non_empty_mod_block_wins_and_a_later_empty_block_does_not_clear_it()
    {
        var lines = new[]
        {
            "[21:06:27][dlc_reloadable.cpp:616]: DLC:",
            "Mod:",
            "First Mod|C:/mods/mod/first/|Enabled",
            "",
            "[21:06:28][dlc_reloadable.cpp:616]: DLC:",
            "Mod:",
            "Second Mod|C:/mods/mod/second/|Enabled",
            "",
            "[21:06:29][dlc_reloadable.cpp:616]: DLC:",
            "Mod:",
            "",
        };

        var mod = Assert.Single(OosSummaryBuilder.ModsFromDebugLog(lines));

        Assert.Equal("Second Mod", mod.Name);
        Assert.Equal("second", mod.Folder);
    }

    [Fact]
    public void Without_a_mod_block_only_mods_matched_after_the_last_playset_switch_count()
    {
        var lines = new[]
        {
            "[21:05:07][dlc_reloadable.cpp:378]: Switching to playset index 3, Name: Map Mod",
            "[21:05:07][dlc_reloadable.cpp:333]: Mod Old Mod () version 1.0.* successfully matched game version 1.0.3.",
            "[21:05:08][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/mods/mod/Old Mod/in_game",
            "[21:05:09][dlc_reloadable.cpp:378]: Switching to playset index 1, Name: Initial playset",
            "[21:05:09][dlc_reloadable.cpp:333]: Mod New Mod () version 1.0.* successfully matched game version 1.0.3.",
            "[21:05:10][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/mods/mod/New Mod/in_game",
        };

        var mod = Assert.Single(OosSummaryBuilder.ModsFromDebugLog(lines));

        Assert.Equal("New Mod", mod.Name);
        Assert.Equal(ModSourceKind.Local, mod.Kind);
        Assert.Equal("New Mod", mod.Folder);
    }

    [Fact]
    public void A_mod_without_a_known_source_shows_only_its_name()
    {
        var mod = Assert.Single(OosSummaryBuilder.ModsFromDebugLog(
            ["[21:05:07][dlc_reloadable.cpp:333]: Mod Lonely () version 1.0.* successfully matched game version 1.0.3."]));

        Assert.Equal(ModSourceKind.Unknown, mod.Kind);
        Assert.Equal("Lonely", DesyncTexts.ModLabel(mod));
    }

    [Fact]
    public void Save_header_gives_date_version_seed_count_and_dlcs()
    {
        var header = SaveHeaderReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(SaveText())));

        // Das "date=" und "version=" im Flaggen-Text darf nie gewinnen.
        Assert.Equal("1425.2.4.8", header.Date);
        Assert.Equal("1.0.3", header.Version);
        Assert.Equal(293541372, header.RandomSeed);
        Assert.Equal(68262752, header.RandomCount);
        Assert.Equal(["Ancient Monument Pack", "Shared DLC Data"], header.EnabledDlcs);
    }

    [Fact]
    public void Seed_and_count_too_large_for_a_long_are_unknown_instead_of_failing()
    {
        var text = SaveText().Replace("random_seed=293541372", "random_seed=999999999999999999999999999999")
            .Replace("random_count=68262752", "random_count=-999999999999999999999999999999");

        var header = SaveHeaderReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(text)));

        Assert.Null(header.RandomSeed);
        Assert.Null(header.RandomCount);
        Assert.Equal("1.0.3", header.Version);
    }

    [Fact]
    public void Frame_numbers_too_large_for_a_long_are_unknown_instead_of_failing()
    {
        using var tree = TwoMachines(
            hostDebug: DebugLog("OOSFrame: 99999999999999999999999\nStartFrame: 99999999999999999999999"),
            clientDebug: DebugLog("OOSFrame: 99999999999999999999999\nStartFrame: 99999999999999999999999"));

        var summary = BuildFor(tree, "host", "client");

        Assert.Null(summary.OosFrame);
        Assert.Null(summary.StartFrame);
        Assert.Equal(FullId, summary.OosIdFull);
    }

    [Fact]
    public void Start_and_oos_frame_come_from_the_same_machine()
    {
        // Die Referenz (Host) kennt nur den OOS-Frame; der Start-Frame des Clients gehört zu dessen eigener Zählung.
        using var tree = TwoMachines(
            hostDebug: DebugLog("OOSFrame: 100"),
            clientDebug: DebugLog("OOSFrame: 200\nStartFrame: 190"));

        var summary = BuildFor(tree, "host", "client");

        Assert.Equal(100, summary.OosFrame);
        Assert.Null(summary.StartFrame);
    }

    [Fact]
    public void Frames_come_from_the_other_machine_when_the_reference_has_none()
    {
        using var tree = TwoMachines(hostDebug: DebugLog(""), clientDebug: DebugLog("OOSFrame: 200\nStartFrame: 190"));

        var summary = BuildFor(tree, "host", "client");

        Assert.Equal(200, summary.OosFrame);
        Assert.Equal(190, summary.StartFrame);
    }

    [Fact]
    public void Save_header_of_an_empty_or_unrelated_stream_is_empty()
    {
        var header = SaveHeaderReader.Read(new MemoryStream(Encoding.UTF8.GetBytes("nothing here\n")));

        Assert.Null(header.Date);
        Assert.Null(header.Version);
        Assert.Null(header.RandomSeed);
        Assert.Null(header.RandomCount);
        Assert.Empty(header.EnabledDlcs);
    }

    [Fact]
    public void Save_header_reads_only_the_first_bytes_and_ignores_a_cut_off_last_line()
    {
        var text = SaveText();
        // Das Limit schneidet mitten durch die Zahl von random_count.
        var cut = Encoding.UTF8.GetByteCount(text[..(text.IndexOf("random_count=", StringComparison.Ordinal) + "random_count=68".Length)]);

        var header = SaveHeaderReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(text)), cut);

        Assert.Equal(293541372, header.RandomSeed);
        Assert.Null(header.RandomCount);
    }

    [Fact]
    public void Frames_and_full_oos_id_come_from_the_logs()
    {
        using var tree = TwoMachines();

        var summary = BuildFor(tree, "host", "client");

        Assert.Equal("11c0de175691b2bb", summary.OosId16);
        Assert.Equal(FullId, summary.OosIdFull);
        Assert.Equal(50663, summary.OosFrame);
        Assert.Equal(50653, summary.StartFrame);
        Assert.Equal("1425.2.4.8", summary.OosGameDate);
        Assert.Equal(["RANDOM_COUNT"], summary.ChecksumCategories);
    }

    [Fact]
    public void Machines_carry_role_player_commits_save_header_mods_and_file_flags()
    {
        using var tree = TwoMachines();

        var summary = BuildFor(tree, "host", "client");

        Assert.Equal([1, 2], summary.Machines.Select(m => m.MachineId));
        var host = summary.Machines[0];
        Assert.Equal("Host", host.Role);
        Assert.Equal("flogi", host.PlayerName);
        Assert.Equal("f2c15314d528e6065a86af708151248f23732b29", host.GameCommit);
        Assert.Equal("f57700e48809374e8418681b5cbd3ed1a27723fb", host.EngineCommit);
        Assert.Equal("1425.2.4.8", host.Save.Date);
        Assert.Equal("3599740945", Assert.Single(host.Mods).WorkshopId);
        Assert.False(host.HasRandomLog);
        Assert.False(host.HasCallstacks);
        Assert.Equal("Client", summary.Machines[1].Role);
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public void Random_log_and_callstacks_count_only_with_real_content()
    {
        using var tree = new TempTree();
        Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            [.. Files("Host", 1, SaveText()),
             (DumpFileKind.RandomLog, "some random calls"),
             (DumpFileKind.Callstacks, Dumps.IgnoredDueToChecksums)]);

        var summary = BuildFor(tree, "host");

        Assert.True(summary.Machines[0].HasRandomLog);
        Assert.False(summary.Machines[0].HasCallstacks);
    }

    [Fact]
    public void Different_game_commits_dlcs_and_mods_give_warnings()
    {
        var clientMetadata = Dumps.ClientMetadata11c0
            .Replace("game_commit:f2c15314d528e6065a86af708151248f23732b29", "game_commit:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
            .Replace("engine_commit:f57700e48809374e8418681b5cbd3ed1a27723fb", "engine_commit:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        var clientDebug = DebugLog().Replace("3599740945", "1111111111").Replace("Better Tactical Map Mode", "Other Mod");
        using var tree = TwoMachines(
            clientSave: SaveText(dlcs: "\"Shared DLC Data\""),
            clientMetadata: clientMetadata,
            clientDebug: clientDebug);

        var summary = BuildFor(tree, "host", "client");

        var kinds = summary.Warnings.Select(w => w.Kind).ToList();
        Assert.Contains(SummaryWarningKind.DifferentGameCommit, kinds);
        Assert.Contains(SummaryWarningKind.DifferentEngineCommit, kinds);
        Assert.Contains(SummaryWarningKind.DifferentDlcs, kinds);
        Assert.Contains(SummaryWarningKind.DifferentMods, kinds);
        Assert.DoesNotContain(SummaryWarningKind.MissingMachine, kinds);
        var commit = summary.Warnings.Single(w => w.Kind == SummaryWarningKind.DifferentGameCommit);
        Assert.Contains("f2c15314d528", commit.Detail);
        Assert.Contains("aaaaaaaaaaaa", commit.Detail);
    }

    [Fact]
    public void Mods_are_compared_by_name_not_by_path_or_source()
    {
        var clientDebug = DebugLog().Replace("C:/Program Files (x86)/Steam/steamapps/workshop/content/3450310/3599740945", "D:/Users/Other/mod/btmm");
        using var tree = TwoMachines(clientDebug: clientDebug);

        var summary = BuildFor(tree, "host", "client");

        Assert.DoesNotContain(summary.Warnings, w => w.Kind == SummaryWarningKind.DifferentMods);
    }

    [Fact]
    public void A_missing_machine_gives_a_warning_with_its_number()
    {
        using var tree = new TempTree();
        Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0, Files("Host", 1, SaveText()));

        var summary = BuildFor(tree, "host");

        var warning = Assert.Single(summary.Warnings);
        Assert.Equal(SummaryWarningKind.MissingMachine, warning.Kind);
        Assert.Equal("2", warning.Detail);
    }

    [Fact]
    public void Random_counts_of_both_machines_are_reported_raw_even_when_equal()
    {
        using var tree = TwoMachines(
            hostSave: SaveText(randomCount: 72570195),
            clientSave: SaveText(randomCount: 72570195));

        var summary = BuildFor(tree, "host", "client");

        Assert.All(summary.Machines, m => Assert.Equal(72570195, m.Save.RandomCount));
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public void A_dump_without_save_or_logs_yields_an_empty_machine_summary_without_warnings()
    {
        using var tree = new TempTree();
        Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0);
        Dumps.Folder(tree, "client", 2, Dumps.ClientMetadata11c0);

        var summary = BuildFor(tree, "host", "client");

        Assert.Equal(2, summary.Machines.Count);
        Assert.All(summary.Machines, m =>
        {
            Assert.Null(m.Role);
            Assert.Null(m.Save.Date);
            Assert.Empty(m.Save.EnabledDlcs);
            Assert.Empty(m.Mods);
        });
        Assert.Null(summary.OosIdFull);
        Assert.Null(summary.OosFrame);
        Assert.Null(summary.StartFrame);
        Assert.Null(summary.OosGameDate);
        Assert.Empty(summary.Warnings);
    }

    private const string OtherId = "2e5541e097da87cefc47d15d13967125";

    /// <summary>Server-Log mit zwei OOS; <paramref name="ownIsSecond"/> sagt, ob das OOS des Paares das zweite ist.</summary>
    private static string TwoOosServerLog(bool ownIsSecond)
    {
        var (firstId, secondId) = ownIsSecond ? (OtherId, FullId) : (FullId, OtherId);
        return $"[10:00:01]{Env}Host_1 Tick: 1431.4.12.2\n" +
               $"[10:00:02]{Env}Host_1 Tick: 1431.4.12.4\n" +
               $"[10:00:02]{Env}OOS Detected! OOS ID: {firstId}\n" +
               $"[10:30:01]{Env}Host_1 Tick: 1439.7.18.4\n" +
               $"[10:30:02]{Env}Host_1 Tick: 1439.7.18.6\n" +
               $"[10:30:02]{Env}OOS Detected! OOS ID: {secondId}\n";
    }

    private static string TwoFramePairsDebugLog() =>
        DebugLog("OOSFrame: 100\nStartFrame: 90") + DebugLog("OOSFrame: 200\nStartFrame: 190");

    private static OosSummary BuildTwoOos(bool ownIsSecond, string debugLog)
    {
        using var tree = new TempTree();
        Dumps.Folder(tree, "host", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.Debug, debugLog),
            (DumpFileKind.DedicatedServer, TwoOosServerLog(ownIsSecond)),
            (DumpFileKind.OosSave, SaveText()));
        Dumps.Folder(tree, "client", 2, Dumps.ClientMetadata11c0);
        return BuildFor(tree, "host", "client");
    }

    [Fact]
    public void Date_id_and_frames_belong_to_the_same_oos_when_the_pair_is_the_second_one_in_the_logs()
    {
        var summary = BuildTwoOos(ownIsSecond: true, TwoFramePairsDebugLog());

        Assert.Equal("1439.7.18.6", summary.OosGameDate);
        Assert.Equal(FullId, summary.OosIdFull);
        Assert.Equal(190, summary.StartFrame);
        Assert.Equal(200, summary.OosFrame);
    }

    [Fact]
    public void Date_id_and_frames_belong_to_the_same_oos_when_the_pair_is_the_first_one_in_the_logs()
    {
        var summary = BuildTwoOos(ownIsSecond: false, TwoFramePairsDebugLog());

        Assert.Equal("1431.4.12.4", summary.OosGameDate);
        Assert.Equal(FullId, summary.OosIdFull);
        Assert.Equal(90, summary.StartFrame);
        Assert.Equal(100, summary.OosFrame);
    }

    [Fact]
    public void When_frame_pairs_and_oos_lines_differ_in_number_the_last_frame_pair_is_used()
    {
        var summary = BuildTwoOos(ownIsSecond: false, DebugLog("OOSFrame: 100\nStartFrame: 90\nOOSFrame: 200\nStartFrame: 190\nOOSFrame: 300\nStartFrame: 290"));

        Assert.Equal(290, summary.StartFrame);
        Assert.Equal(300, summary.OosFrame);
    }
}
