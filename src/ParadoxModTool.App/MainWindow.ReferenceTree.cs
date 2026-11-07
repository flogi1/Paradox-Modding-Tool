using System.IO;
using ParadoxModTool.Editor;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ReferenceTree;
using ParadoxModTool.ScriptCheck;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>
/// Verweisbaum im Reiter "Verweise" der rechten Leiste: Kontextmenü und Kürzel im Editor, Laden der Daten aus den geteilten Sitzungen
/// (Schlüssel aktive Mod + Playset, wie die Fenster von Flag-Tracker und Skript-Prüfung), Hinweise bei Wechsel von Mod oder Playset und
/// bei neuen Daten. Der Baum bricht nie einen Aufbau ab: Läuft schon einer, wartet er auf dessen Ende.
/// </summary>
public partial class MainWindow
{
    /// <summary>Aktive Mod und Playset des letzten Ladens; null, solange der Baum nichts geladen hat (dann löst ein Wechsel nichts aus).</summary>
    private (string Mod, string? Playset)? _referenceScope;

    private ReferenceTreePanel References => NotesPanelControl.References;

    /// <summary>Mindestens eines der beiden Werkzeuge ist im Spiel verfügbar (dieselbe Tabelle wie Kacheln und Menüpunkte).</summary>
    private bool ReferenceTreeAvailable => ToolStateOf(TileIds.FlagTracker).Available || ToolStateOf(TileIds.ScriptCheck).Available;

    /// <summary>Beim Start: Panel verdrahten; ein Playsetwechsel leert einen benutzten Baum.</summary>
    private void InitializeReferenceTree()
    {
        References.LoadSources = LoadReferenceSourcesAsync;
        References.OpenRequested += (_, location) => OpenReferenceSite(location);
        References.ContentChanged += (_, _) => NotesPanelControl.ReferencesHaveContent = References.ShowsContent;
        PlaysetsCombo.SelectionChanged += (_, _) => ResetReferenceTreeIfScopeChanged();
    }

    /// <summary>Kontextmenü "Im Verweisbaum zeigen" beider Arbeitsbereiche; ohne verfügbares Werkzeug liefert der Picker keinen Namen.</summary>
    private void WireReferenceTreeEditor(EditorPane pane)
    {
        pane.ReferenceNamePicker = (line, column, selection) => ReferenceTreeAvailable ? ReferenceQuery.FromEditor(line, column, selection) : null;
        pane.ReferenceTreeHeader = ReferenceTreeTexts.ContextShow;
        pane.ReferenceTreeRequested += (_, name) => ShowInReferenceTree(name);
    }

    /// <summary>Leiste einschalten, Reiter "Verweise" wählen und den Namen zeigen.</summary>
    private void ShowInReferenceTree(string name)
    {
        _settings.NotesPanelVisible = true;
        NotesPanelControl.ShowReferencesTab();
        References.ShowName(name);
    }

    /// <summary>Strg+Umschalt+R: Name an der Cursorstelle des aktiven Editors; ohne Namen Reiter öffnen und Fokus ins Eingabefeld.</summary>
    private void ShowReferenceTreeFromEditor()
    {
        if (ActivePane.ReferenceNameAtCaret() is { } name)
        {
            ShowInReferenceTree(name);
            return;
        }

        OpenReferenceTreeInput();
    }

    /// <summary>Menüpunkt "Im Verweisbaum zeigen": wie das Kürzel ohne Namen.</summary>
    private void OpenReferenceTree_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ReferenceTreeAvailable)
        {
            OpenReferenceTreeInput();
        }
    }

    /// <summary>Leiste einschalten, Reiter "Verweise" wählen, Fokus ins Eingabefeld.</summary>
    private void OpenReferenceTreeInput()
    {
        _settings.NotesPanelVisible = true;
        NotesPanelControl.ShowReferencesTab();
        References.FocusInput();
    }

    /// <summary>Ob beim letzten <see cref="ApplyReferenceTreeAvailability"/> der Hinweis "nicht verfügbar" gesetzt wurde.</summary>
    private bool _referenceTreeShowsNotSupported;

    /// <summary>
    /// Aus <see cref="ApplyToolAvailability"/> (Start, Script-Doku geladen): Menüpunkt gesperrt mit Begründung, wenn weder Flag-Tracker noch
    /// Skript-Prüfung verfügbar ist; der Reiter zeigt dann von Anfang an "nicht verfügbar".
    /// </summary>
    private void ApplyReferenceTreeAvailability()
    {
        var available = ReferenceTreeAvailable;
        ReferenceTreeMenuItem.IsEnabled = available;
        ReferenceTreeMenuItem.ToolTip = available ? null : ReferenceTreeTexts.NotSupported;
        // Nur bei einer Änderung zurücksetzen: ein erneutes Anwenden (z. B. Script-Doku neu geladen) lässt Baum und Eingabefeld stehen.
        switch (ReferenceTreeRules.Availability(available, _referenceTreeShowsNotSupported))
        {
            case ReferenceAvailabilityStep.ShowNotSupported:
                _referenceScope = null;
                ReleaseReferenceSessions();
                References.Reset(ReferenceTreeTexts.NotSupported);
                _referenceTreeShowsNotSupported = true;
                break;
            case ReferenceAvailabilityStep.ClearNotSupported:
                References.Reset(string.Empty);
                _referenceTreeShowsNotSupported = false;
                break;
        }
    }

    /// <summary>
    /// Doppelklick auf eine Stelle: Datei an der Zeile öffnen; fehlt sie (auch erst beim Lesen) oder ist sie nicht lesbar, ein Hinweis in der
    /// Statusleiste.
    /// </summary>
    private void OpenReferenceSite(SiteLocation location)
    {
        if (!File.Exists(location.FullPath))
        {
            ModStatusText.Text = ReferenceTreeTexts.FileMissing(location.FullPath);
            return;
        }

        try
        {
            OpenPatchFileAtLine(location.FullPath, location.Line);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            ModStatusText.Text = ReferenceTreeTexts.FileMissing(location.FullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ModStatusText.Text = ex.Message;
        }
    }

    /// <summary>
    /// Nach einem Wechsel von aktiver Mod oder Playset: Hat der Baum mit anderen Werten geladen, leert er sich und zeigt "neu laden".
    /// Aufrufer für die Mod: <see cref="RefreshActiveMod"/> (nur bei fest gewählter Mod, wie die Fenster).
    /// </summary>
    private void ResetReferenceTreeIfScopeChanged()
    {
        if (_referenceScope is not { } used || (GameContext.Profile ?? _activeProfile) is not { } profile)
        {
            return;
        }

        var (mod, playset) = ReferenceScopeNow(CreateFlagTrackerHost(profile));
        if (string.Equals(mod, used.Mod, StringComparison.OrdinalIgnoreCase) && string.Equals(playset, used.Playset, StringComparison.Ordinal))
        {
            return;
        }

        _referenceScope = null;
        ReleaseReferenceSessions();
        References.Reset(ReferenceTreeTexts.ReloadHint);
    }

    /// <summary>
    /// Aktive Mod (vorhandener Ordner) und Playset aus denselben Ausdrücken wie in den Fenstern: gewähltes Playset, sonst das aktive des
    /// Launchers (null, wenn es keines gibt; der Schlüssel erlaubt das wie in den Fenstern).
    /// </summary>
    private static (string? Mod, string? Playset) ReferenceScopeNow(FlagTrackerWindowHost host) =>
        (host.ActiveModDirectory(), host.SelectedPlaysetName() ?? host.Mods.DefaultPlaysetName());

    /// <summary>
    /// Quellen für den Baum aus den geteilten Sitzungen: bereit → Index nehmen; läuft ein Aufbau → auf sein Ende warten; leer, fehlgeschlagen
    /// oder <paramref name="reload"/> (⟳) → Aufbau starten. Flag-Teil nur mit Befehlstabelle, Prüfungs-Teil nur mit Regeln.
    /// </summary>
    private async Task<ReferenceSources?> LoadReferenceSourcesAsync(bool reload)
    {
        WatchReferenceSessions(null, null);
        var profile = GameContext.Profile ?? _activeProfile;
        var flagsOn = ToolStateOf(TileIds.FlagTracker).Available;
        var rules = ToolStateOf(TileIds.ScriptCheck).Available ? ScriptCheckRulesOfGame() : null;
        if (profile is null || (!flagsOn && rules is null))
        {
            ReleaseReferenceSessions();
            References.SetStatus(ReferenceTreeTexts.NotSupported);
            return null;
        }

        var flagHost = CreateFlagTrackerHost(profile);
        var (mod, playset) = ReferenceScopeNow(flagHost);
        // Wie die Fenster: nur eine aktive Mod ist Pflicht, ohne Playset gilt das des Launchers oder keines.
        if (mod is null)
        {
            ReleaseReferenceSessions();
            References.SetStatus(ReferenceTreeTexts.NoModOrPlayset);
            return null;
        }

        _referenceScope = (mod, playset);
        // Nach einem Reset (anderer Schlüssel) überschreibt der Fortschritt den Hinweis "neu laden" nicht mehr.
        var progress = new Progress<BuildProgress>(p =>
        {
            if (Wanted())
            {
                References.ShowProgress(p.Total > 0 ? $"{ReferenceTreeTexts.Building} {p.Done}/{p.Total}" : ReferenceTreeTexts.Building);
            }
        });
        bool Wanted() => _referenceScope == (mod, playset);
        var flagTask = flagsOn ? LoadFlagSessionAsync(flagHost, mod, playset, reload, progress, Wanted) : Task.FromResult<FlagTrackerSession?>(null);
        var checkTask = rules is not null
            ? LoadCheckSessionAsync(CreateScriptCheckHost(profile, rules), mod, playset, reload, progress, Wanted)
            : Task.FromResult<ScriptCheckSession?>(null);
        await Task.WhenAll(flagTask, checkTask);
        var flags = flagTask.Result;
        var checks = checkTask.Result;

        // Schlüssel inzwischen gewechselt (Reset): dieses Ergebnis nicht mehr beobachten.
        if (_referenceScope != (mod, playset))
        {
            return null;
        }

        var failure = flags is { State: FlagTrackerState.Failed } ? flags.StatusText
            : checks is { State: ScriptCheckState.Failed } ? checks.StatusText
            : null;
        if (flags?.Index is null && checks?.Index is null)
        {
            References.SetStatus(failure is not null ? ReferenceTreeTexts.BuildFailed(failure) : flags?.StatusText ?? checks?.StatusText ?? string.Empty);
            return null;
        }

        if (failure is not null)
        {
            References.SetStatus(ReferenceTreeTexts.BuildFailed(failure));
        }

        WatchReferenceSessions(flags, checks);
        return new ReferenceSources(flags?.Index, checks?.Index);
    }
}
