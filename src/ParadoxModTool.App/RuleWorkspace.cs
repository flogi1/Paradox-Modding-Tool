using ParadoxModTool.Templates;

namespace ParadoxModTool.App;

/// <summary>
/// Gemeinsamer Zugriff der Fenster auf die Regeln aller Ebenen: die Ebenen Alle Spiele, Spiel und Mod liegen in Dateien (<see cref="RuleStore"/>), die Ebene Vorlage
/// im Zustand des Template-Fensters. Jede Änderung meldet <c>changed</c>, damit das Template-Fenster die Regeln neu auflöst und das Snippet neu analysiert.
/// </summary>
public sealed class RuleWorkspace
{
    private readonly Func<RuleFile> _getTemplate;
    private readonly Action _changed;

    public RuleWorkspace(RuleStore store, Func<RuleFile> getTemplate, Action changed)
    {
        Store = store;
        _getTemplate = getTemplate;
        _changed = changed;
    }

    public RuleStore Store { get; }

    /// <summary>Die Datei einer Ebene (bei "Vorlage" die im Speicher; sonst frisch von der Platte).</summary>
    public RuleFile FileOf(RuleLayer layer) => layer == RuleLayer.Template ? _getTemplate() : Store.Load(layer);

    public bool HasLayer(RuleLayer layer) => layer == RuleLayer.Template || Store.HasLayer(layer);

    /// <summary>Ändert und speichert die Datei einer Ebene. Wirft <see cref="InvalidOperationException"/>, wenn die Ebene nicht verfügbar ist, und E/A-Fehler beim Schreiben.</summary>
    public void Modify(RuleLayer layer, Action<RuleFile> change)
    {
        if (layer == RuleLayer.Template)
        {
            change(_getTemplate());
        }
        else
        {
            Store.Update(layer, change);
        }

        _changed();
    }

    /// <summary>Alle Regeln aller Ebenen in Rangfolge, mit Wirksamkeit.</summary>
    public IReadOnlyList<RuleListRow> List()
    {
        var layers = new List<RuleLayerFile>
        {
            new(RuleLayer.AllGames, Store.Load(RuleLayer.AllGames)),
            new(RuleLayer.Game, Store.Load(RuleLayer.Game)),
        };
        if (Store.HasLayer(RuleLayer.Mod))
        {
            layers.Add(new RuleLayerFile(RuleLayer.Mod, Store.Load(RuleLayer.Mod)));
        }

        layers.Add(new RuleLayerFile(RuleLayer.Template, _getTemplate()));
        return RuleResolver.List(layers, Store.GameId);
    }
}
