using System.IO;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.App;

/// <summary>
/// Baut den GUI-Arbeitsbereich (Vanilla, Abhängigkeits-Mods, aktive Mod) im Hintergrund und reicht ihn an den Designer weiter
/// (<see cref="GuiSettings"/>). Beim Mod-Wechsel werden die schon geparsten Vanilla-Dateien wiederverwendet; ein gespeicherter Katalog
/// wird geladen statt neu gelernt, solange sich die Vanilla-Dateien nicht geändert haben.
/// </summary>
public sealed class GuiWorkspaceService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _catalogPath;
    private readonly GuiRules _rules;
    private GuiWorkspace? _current;
    private string? _key;

    public GuiWorkspaceService(string catalogPath, GuiRules rules)
    {
        _catalogPath = catalogPath;
        _rules = rules;
    }

    /// <param name="installDirectory">Installationsordner mit game, jomini, clausewitz; null, wenn kein Spielpfad bekannt ist.</param>
    public async Task UpdateAsync(string? installDirectory, string? modRoot, IReadOnlyList<string> dependencyRoots, bool force, CancellationToken cancellation = default, IProgress<(int Done, int Total)>? progress = null)
    {
        await _gate.WaitAsync();
        try
        {
            if (installDirectory is null)
            {
                _current = null;
                _key = null;
                GuiSettings.SetWorkspace(null, GuiTexts.IndexNoGamePath);
                return;
            }

            var key = string.Join("|", new[] { installDirectory, modRoot ?? string.Empty }.Concat(dependencyRoots));
            if (!force && _current is not null && key == _key)
            {
                return;
            }

            var options = new GuiWorkspaceOptions(installDirectory, modRoot, dependencyRoots, _rules, _catalogPath, RelearnCatalog: force);
            var previous = force ? null : _current;
            var workspace = await Task.Run(() => GuiWorkspace.Create(options, previous, cancellation, progress), cancellation);

            // Während des Aufbaus ausgeschaltet: Ergebnis verwerfen, sonst bliebe der Speicher trotz "aus" belegt.
            if (!GuiSettings.ToolsEnabled)
            {
                return;
            }

            _current = workspace;
            _key = key;
            GuiSettings.SetWorkspace(workspace, Summarize(workspace));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Abgebrochener Neuaufbau: der bisherige Index bleibt.
            GuiSettings.SetWorkspace(_current, _current is null ? GuiTexts.IndexCanceled : Summarize(_current));
            throw;
        }
        catch (Exception ex)
        {
            // Läuft ohne Beobachter (fire-and-forget): jeder Fehler muss in der Statuszeile ankommen, sonst bliebe "wird geladen" stehen.
            GuiSettings.SetWorkspace(_current, GuiTexts.IndexFailed(ex.Message));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Eine gespeicherte .gui-Datei aus Mod oder Abhängigkeits-Mod in den Index übernehmen (andere Dateien ändern nichts).</summary>
    public void FileSaved(string path, string text)
    {
        if (_current is null || !path.EndsWith(".gui", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var updated = _current.WithModFile(path, text);
        if (!ReferenceEquals(updated, _current))
        {
            _current = updated;
            GuiSettings.SetWorkspace(updated, Summarize(updated));
        }
    }

    /// <summary>GUI-Werkzeuge ausgeschaltet: Index freigeben; der nächste <see cref="UpdateAsync"/> baut ihn neu.</summary>
    public void Clear()
    {
        _current = null;
        _key = null;
        GuiSettings.SetWorkspace(null, GuiTexts.IndexToolsOff);
    }

    private static string Summarize(GuiWorkspace workspace)
    {
        var types = workspace.Index.Definitions.Count(d => d.Kind == GuiDefinitionKind.Type);
        var learned = workspace.CatalogFromCache ? string.Empty : GuiTexts.IndexCatalogLearned;
        return GuiTexts.IndexSummary(workspace.Index.Files.Count, types, workspace.BuildDuration.TotalSeconds, learned);
    }
}
