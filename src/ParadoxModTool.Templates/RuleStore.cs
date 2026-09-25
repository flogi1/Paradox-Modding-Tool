using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Templates;

/// <summary>
/// Die Regeldateien der Ebenen "Alle Spiele", "Spiel" und "Mod". Die Ebene "Vorlage" liegt in der Vorlage selbst und wird an <see cref="Resolve"/> übergeben.
/// Änderungen lesen die Datei unter <see cref="SharedFile.Locked(string, Action)"/> frisch, ändern sie und schreiben atomar; ein Merge ist nicht nötig,
/// weil die globale Datei zwar von den Prozessen aller Spiele geteilt wird, jede Änderung aber auf dem gerade gelesenen Stand aufsetzt.
/// </summary>
public sealed class RuleStore
{
    private readonly string _globalFile;
    private readonly string _gameFile;
    private readonly List<string> _warnings = new();

    public RuleStore(string globalFile, string gameFile, string gameId)
    {
        _globalFile = globalFile;
        _gameFile = gameFile;
        GameId = gameId;
    }

    public string GameId { get; }

    /// <summary>Regeldatei der Mod; <see langword="null"/>, solange keine Mod gewählt ist (dann gibt es die Ebene "Mod" nicht).</summary>
    public string? ModFile { get; set; }

    /// <summary>Hinweise zu Dateien, die nicht lesbar waren (jede Meldung einmal).</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    public bool HasLayer(RuleLayer layer) => FileFor(layer) is { Length: > 0 };

    /// <summary>Die Datei der Ebene. Eine fehlende Datei ergibt eine leere Ebene, eine kaputte wird als <c>.bad</c> gesichert und ergibt ebenfalls eine leere Ebene.</summary>
    public RuleFile Load(RuleLayer layer)
    {
        var path = FileFor(layer);
        return string.IsNullOrEmpty(path) ? new RuleFile() : SharedFile.Locked<RuleFile>(path, () => Read(path));
    }

    /// <summary>Ändert die Datei der Ebene unter Sperre auf dem frisch gelesenen Stand. Wirft <see cref="InvalidOperationException"/>, wenn die Ebene keine Datei hat.</summary>
    public void Update(RuleLayer layer, Action<RuleFile> change)
    {
        var path = FileFor(layer);
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException($"Für die Ebene {RuleLayerNames.Of(layer)} gibt es keine Regeldatei.");
        }

        SharedFile.Locked(path, () =>
        {
            var file = Read(path);
            change(file);
            SharedFile.WriteAllTextAtomic(path, JsonSerializer.Serialize(file, RuleFile.JsonOptions));
        });
    }

    /// <summary>Die wirksamen Regeln aller Ebenen in Rangfolge, mit der Vorlage als spezifischster Ebene.</summary>
    public IReadOnlyList<EffectiveRule> Resolve(RuleFile templateFile)
    {
        var layers = new List<RuleLayerFile>
        {
            new(RuleLayer.AllGames, Load(RuleLayer.AllGames)),
            new(RuleLayer.Game, Load(RuleLayer.Game)),
        };
        if (HasLayer(RuleLayer.Mod))
        {
            layers.Add(new RuleLayerFile(RuleLayer.Mod, Load(RuleLayer.Mod)));
        }

        layers.Add(new RuleLayerFile(RuleLayer.Template, templateFile));
        return RuleResolver.Resolve(layers, GameId);
    }

    private string? FileFor(RuleLayer layer) => layer switch
    {
        RuleLayer.AllGames => _globalFile,
        RuleLayer.Game => _gameFile,
        RuleLayer.Mod => ModFile,
        _ => null,
    };

    private RuleFile Read(string path)
    {
        if (!File.Exists(path))
        {
            return new RuleFile();
        }

        try
        {
            var file = JsonSerializer.Deserialize<RuleFile>(File.ReadAllText(path), RuleFile.JsonOptions) ?? throw new JsonException("Die Datei ist leer.");
            return Sanitize(file);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            var message = $"Die Regeldatei \"{path}\" war nicht lesbar; eine Kopie liegt als .bad daneben, die Ebene ist leer.";
            if (!_warnings.Contains(message))
            {
                _warnings.Add(message);
            }

            try
            {
                File.Copy(path, path + ".bad", overwrite: true);
            }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException)
            {
                // Die Kopie ist nur ein Versuch; die Warnung bleibt.
            }

            return new RuleFile();
        }
    }

    private static RuleFile Sanitize(RuleFile file)
    {
        file.Rules = (file.Rules ?? new List<CountingRule>()).Where(r => r is not null).ToList();
        foreach (var rule in file.Rules)
        {
            rule.Id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id;
            rule.Name ??= string.Empty;
            rule.Games ??= new List<string> { "*" };
            rule.Patterns ??= new List<string>();
            rule.WhereKey ??= string.Empty;
        }

        file.DisabledRuleIds = (file.DisabledRuleIds ?? new List<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        return file;
    }
}
