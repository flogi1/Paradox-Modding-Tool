using System.IO.Compression;

namespace ParadoxModTool.Desync.Tests;

/// <summary>Testdaten und Hilfen, die das Layout eines OOS-Dump-Ordners nachbauen.</summary>
internal static class Dumps
{
    /// <summary>Wortgetreuer Text von 11c0de175691b2bb_0/oos_metadata_1.txt (Host).</summary>
    public const string HostMetadata11c0 = """
        hotjoin_vs_preparation:preparation
        local_num_times_desynchronized:0
        build_configuration:ReleaseLto
        game_commit:f2c15314d528e6065a86af708151248f23732b29
        engine_commit:f57700e48809374e8418681b5cbd3ed1a27723fb
        oos_player_name:flogi
        oos_folder_name:11c0de175691b2bb_0
        local_machine_id:1
        time_utc: 21:47:15 0125-10-11
        language:l_english
        oos_machine_id:2
        all_checksums:RANDOM_COUNT
        oos_checksums:
        0:RANDOM_COUNT
        """;

    /// <summary>Metadaten des Clients (Maschine 2) desselben Dumps.</summary>
    public const string ClientMetadata11c0 = """
        hotjoin_vs_preparation:preparation
        local_num_times_desynchronized:0
        build_configuration:ReleaseLto
        game_commit:f2c15314d528e6065a86af708151248f23732b29
        engine_commit:f57700e48809374e8418681b5cbd3ed1a27723fb
        oos_player_name:client
        oos_folder_name:11c0de175691b2bb_0
        local_machine_id:2
        time_utc: 21:47:15 0125-10-11
        language:l_english
        oos_machine_id:2
        all_checksums:RANDOM_COUNT
        oos_checksums:
        0:RANDOM_COUNT
        """;

    /// <summary>Inhalt der Prüfsummen-Platzhalter-Dateien (bias_*.log u. ä.).</summary>
    public const string IgnoredDueToChecksums = "IGNORED DUE TO CHECKSUMS";

    /// <summary>Dateiname einer Dump-Datei für die angegebene Maschine.</summary>
    public static string FileName(DumpFileKind kind, int machineId) => kind switch
    {
        DumpFileKind.Metadata => $"oos_metadata_{machineId}.txt",
        DumpFileKind.OosSave => $"savegame_oos_machineid_{machineId}.oos",
        DumpFileKind.HotjoinSave => $"hotjoinsave_{machineId}.txt",
        DumpFileKind.Debug => $"debug_{machineId}.log",
        DumpFileKind.Error => $"error_{machineId}.log",
        DumpFileKind.Game => $"game_{machineId}.log",
        DumpFileKind.Multiplayer => $"multiplayer_{machineId}.log",
        DumpFileKind.DedicatedServer => $"dedicated_server_{machineId}.log",
        DumpFileKind.System => $"system_{machineId}.log",
        DumpFileKind.RandomLog => $"randomlog_{machineId}.log",
        DumpFileKind.Callstacks => $"callstacks_{machineId}.log",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Schreibt einen Dump-Ordner unter dem TempTree und liefert dessen Pfad.</summary>
    public static string Folder(TempTree tree, string folderName, int machineId, string metadataText,
        params (DumpFileKind Kind, string Content)[] files)
    {
        var folder = tree.PathOf(folderName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName(DumpFileKind.Metadata, machineId)), metadataText);
        foreach (var (kind, content) in files)
            File.WriteAllText(Path.Combine(folder, FileName(kind, machineId)), content);
        return folder;
    }

    /// <summary>
    /// Schreibt eine Zip-Datei mit dem Dump-Layout; <paramref name="innerFolder"/> ist der Ordnerpräfix
    /// der Einträge (leer = Dateien direkt im Wurzelverzeichnis der Zip). Gibt es die Zip schon, kommen die Einträge dazu
    /// (etwa die Dateien einer zweiten Maschine).
    /// </summary>
    public static void Zip(string zipPath, string innerFolder, int machineId, string metadataText,
        params (DumpFileKind Kind, string Content)[] files)
    {
        var prefix = string.IsNullOrEmpty(innerFolder) ? "" : innerFolder.TrimEnd('/') + "/";
        using var archive = ZipFile.Open(zipPath, File.Exists(zipPath) ? ZipArchiveMode.Update : ZipArchiveMode.Create);
        Add(archive, prefix + FileName(DumpFileKind.Metadata, machineId), metadataText);
        foreach (var (kind, content) in files)
            Add(archive, prefix + FileName(kind, machineId), content);
    }

    /// <summary>
    /// Überschreibt die komprimierten Daten des Eintrags, dessen Name auf <paramref name="entryNameEnd"/> endet, mit 0xFF: Das zentrale
    /// Verzeichnis bleibt gültig, erst das Lesen des Eintrags scheitert (ungültiger Deflate-Block).
    /// </summary>
    public static void CorruptEntry(string zipPath, string entryNameEnd)
    {
        var bytes = File.ReadAllBytes(zipPath);
        var position = 0;
        while (BitConverter.ToUInt32(bytes, position) == 0x04034b50)
        {
            var compressedSize = BitConverter.ToInt32(bytes, position + 18);
            var nameLength = BitConverter.ToUInt16(bytes, position + 26);
            var extraLength = BitConverter.ToUInt16(bytes, position + 28);
            var name = System.Text.Encoding.UTF8.GetString(bytes, position + 30, nameLength);
            var data = position + 30 + nameLength + extraLength;
            if (name.EndsWith(entryNameEnd, StringComparison.Ordinal))
            {
                Array.Fill(bytes, (byte)0xFF, data, compressedSize);
                File.WriteAllBytes(zipPath, bytes);
                return;
            }

            position = data + compressedSize;
        }

        throw new InvalidOperationException("Eintrag nicht gefunden: " + entryNameEnd);
    }

    private static void Add(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}
