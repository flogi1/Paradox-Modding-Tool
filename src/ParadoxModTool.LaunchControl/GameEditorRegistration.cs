using System.Text;
using System.Text.Json;

namespace ParadoxModTool.LaunchControl;

public enum GameEditorOutcome
{
    Registered,
    AlreadyRegistered,
    Restored,
    NothingToRestore,
    EditorChangedSince,
    SettingsMissing,
    UnknownFormat,
    WriteFailed,
}

/// <summary>Ergebnis; <c>Detail</c>: Pfad (SettingsMissing, UnknownFormat), alter Editor (Restored), aktueller Editor (EditorChangedSince) oder Fehlertext.</summary>
public sealed record GameEditorResult(GameEditorOutcome Outcome, string? Detail = null);

/// <summary>Gemerkter Stand vor dem Eintragen: ob und welcher Editor eingetragen war und welcher PMT-Pfad eingetragen wurde.</summary>
public sealed class GameEditorState
{
    public bool HadEditor { get; set; }

    public string? PreviousEditor { get; set; }

    public string RegisteredPath { get; set; } = string.Empty;
}

/// <summary>
/// "PMT als Editor eintragen" (Spec, Plan 4 Ruling 5): vorher Sicherung der Datei und des alten Werts im Ordner des Spiels; zweimal eintragen behält
/// den ersten alten Wert; wiederherstellen nur, solange PMT eingetragen ist. BOM und alles außer dem Wert bleiben. Ob das Spiel läuft, prüft der
/// Aufrufer.
/// </summary>
public static class GameEditorRegistration
{
    public const string StateFileName = "game-editor.json";

    public const string BackupFileName = "pdx_settings.backup.json";

    public static PdxEditorRead CurrentEditor(string settingsPath)
    {
        try
        {
            return File.Exists(settingsPath)
                ? PdxSettingsText.ReadEditor(ReadText(settingsPath, out _))
                : new PdxEditorRead(PdxSettingsProblem.Missing, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PdxEditorRead(PdxSettingsProblem.UnknownFormat, null);
        }
    }

    public static GameEditorResult Register(string settingsPath, string pmtPath, string stateDirectory)
    {
        if (!File.Exists(settingsPath))
        {
            return new GameEditorResult(GameEditorOutcome.SettingsMissing, settingsPath);
        }

        try
        {
            var text = ReadText(settingsPath, out var bom);
            var current = PdxSettingsText.ReadEditor(text);
            if (current.Problem != PdxSettingsProblem.None || PdxSettingsText.WithEditor(text, pmtPath) is not { } updated)
            {
                return new GameEditorResult(GameEditorOutcome.UnknownFormat, settingsPath);
            }

            if (SamePath(current.Editor, pmtPath))
            {
                return new GameEditorResult(GameEditorOutcome.AlreadyRegistered);
            }

            // Steht noch der früher eingetragene PMT-Pfad drin (z.B. PMT verschoben), bleibt der erste alte Wert; sonst gilt der jetzige.
            var state = ReadState(stateDirectory);
            if (state is null || !SamePath(current.Editor, state.RegisteredPath))
            {
                state = new GameEditorState { HadEditor = current.Editor is not null, PreviousEditor = current.Editor };
            }

            state.RegisteredPath = pmtPath;
            Directory.CreateDirectory(stateDirectory);
            File.Copy(settingsPath, Path.Combine(stateDirectory, BackupFileName), overwrite: true);
            File.WriteAllText(Path.Combine(stateDirectory, StateFileName), JsonSerializer.Serialize(state));
            WriteText(settingsPath, updated, bom);
            return new GameEditorResult(GameEditorOutcome.Registered);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new GameEditorResult(GameEditorOutcome.WriteFailed, ex.Message);
        }
    }

    public static GameEditorResult Restore(string settingsPath, string pmtPath, string stateDirectory)
    {
        if (ReadState(stateDirectory) is not { } state)
        {
            return new GameEditorResult(GameEditorOutcome.NothingToRestore);
        }

        if (!File.Exists(settingsPath))
        {
            return new GameEditorResult(GameEditorOutcome.SettingsMissing, settingsPath);
        }

        try
        {
            var text = ReadText(settingsPath, out var bom);
            var current = PdxSettingsText.ReadEditor(text);
            if (current.Problem != PdxSettingsProblem.None)
            {
                return new GameEditorResult(GameEditorOutcome.UnknownFormat, settingsPath);
            }

            if (!SamePath(current.Editor, state.RegisteredPath) && !SamePath(current.Editor, pmtPath))
            {
                return new GameEditorResult(GameEditorOutcome.EditorChangedSince, current.Editor);
            }

            var previous = state.HadEditor ? state.PreviousEditor : null;
            WriteText(settingsPath, PdxSettingsText.WithEditor(text, previous)!, bom);
            File.Delete(Path.Combine(stateDirectory, StateFileName));
            return new GameEditorResult(GameEditorOutcome.Restored, previous);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new GameEditorResult(GameEditorOutcome.WriteFailed, ex.Message);
        }
    }

    public static bool SamePath(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static GameEditorState? ReadState(string stateDirectory)
    {
        var path = Path.Combine(stateDirectory, StateFileName);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<GameEditorState>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string ReadText(string path, out bool bom)
    {
        var bytes = File.ReadAllBytes(path);
        bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
    }

    private static void WriteText(string path, string text, bool bom)
    {
        var body = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(path, bom ? new byte[] { 0xEF, 0xBB, 0xBF }.Concat(body).ToArray() : body);
    }
}
