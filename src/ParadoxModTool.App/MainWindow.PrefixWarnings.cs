using System.IO;
using ParadoxModTool.Editor;
using ParadoxModTool.Explorer;
using ParadoxModTool.ModCompat;

namespace ParadoxModTool.App;

/// <summary>
/// Präfixe, die im Ordner nicht wirken (Victoria 3), als Wellenlinie im Editor: dieselbe Prüfung wie in der Mod-Kompatibilität (<see cref="PrefixCheck"/>).
/// Der Pfad wird relativ zum Mod-Ordner (Register), zum Spielordner oder zu einem Sammelordner aller Mods bestimmt.
/// </summary>
public partial class MainWindow
{
    private void InstallPrefixWarnings()
    {
        if (_activeProfile is not { } profile || !profile.OverrideRules.Any(r => r.Operators is not null))
        {
            ScopeSettings.SetPrefixChecker(null);
            return;
        }

        ScopeSettings.SetPrefixChecker(PrefixCheckFor);
    }

    private (OverrideRules Rules, string File, DateTime Stamp, ParadoxModTool.GameProfiles.GameProfile Profile)? _prefixRules;

    /// <summary>Die Regeln des Spiels samt Regelfenster; neu gelesen nur, wenn sich compat/rules.json geändert hat (sonst je Prüfung eine Plattenlesung).</summary>
    private OverrideRules PrefixRules(ParadoxModTool.GameProfiles.GameProfile profile)
    {
        var file = Path.Combine(GameContext.Repository.GameDirectory(GameContext.GameId), "compat", "rules.json");
        var stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;
        if (_prefixRules is not { } cached || cached.Stamp != stamp || cached.File != file || !ReferenceEquals(cached.Profile, profile))
        {
            _prefixRules = cached = (OverrideRules.FromProfile(profile, OverrideRulesStore.Load(file)), file, stamp, profile);
        }

        return cached.Rules;
    }

    /// <summary>Auf dem UI-Thread: Wurzeln und Regeln für die Datei; die zurückgegebene Prüfung läuft im Hintergrund.</summary>
    private Func<string, IReadOnlyList<EditorWarning>>? PrefixCheckFor(string? path)
    {
        if (path is null || _activeProfile is not { } profile)
        {
            return null;
        }

        var (vanilla, _) = CollectOriginRoots();
        var roots = (_mods?.Registry.Resolve(path)?.Paths ?? new List<string>()).Concat(vanilla).ToList();
        var collections = ExplorerPanelControl.CaptureRoots().Where(r => r.Kind == ExplorerRootKind.ModFolder).Select(r => r.FullPath).ToList();
        var rules = PrefixRules(profile);
        return text => PrefixCheck.FindForFile(path, text, rules, roots, collections)
            .Select(issue => new EditorWarning(issue.Start, issue.Length, issue.Message, issue.Kind == PrefixIssueKind.NoEffect))
            .ToList();
    }
}
