using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>
/// Hash-Prüfung der Lokalisierungsschlüssel (Spec 2026-10-10-generator-packages-iv2-dynamic-ideas, Abschnitt 2): zwei Schlüssel mit gleichem Hash füllen im Spiel
/// das error.log. Ein neuer Arbeitsstand mit <c>autoNumber</c> rückt auf die nächste freie Nummer; ein Stand, den die Mod schon hat, bekommt nur Warnungen.
/// </summary>
public partial class GeneratorWindow
{
    private readonly TextBlock _planWarnings = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
    /// <summary>
    /// Index-Teile je Ordner, im Hintergrund gebaut und für die Lebensdauer des Fensters behalten (das Spiel ändert sich dabei nicht; auf EU V dauert das
    /// Sekunden). Pakete mit <c>autoNumber</c> wärmen die Spielordner schon beim Anzeigen vor.
    /// </summary>
    private readonly KeyIndexCache _keyIndexes = new();
    /// <summary>Schließen des Fensters bricht Vorwärmen und laufendes Einlesen ab.</summary>
    private readonly CancellationTokenSource _lifetime = new();
    /// <summary>Ordner des Mod-Teils im <see cref="_keyIndexes"/> (nur wenn die Mod kein Spielordner ist); "Erzeugen" und ein Mod-Wechsel verwerfen ihn.</summary>
    private string? _modKeyIndexRoot;
    private bool _closed;

    /// <summary>Beim Schließen: laufende Index-Aufgaben abbrechen; ein noch wartendes Vorschau-Berechnen endet still.</summary>
    private void CancelKeyIndexOnClose()
    {
        _closed = true;
        _lifetime.Cancel();
    }

    private List<string> GameKeyRoots() => _host.GameRoots().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Paket mit <c>autoNumber</c> angezeigt: Spielordner und aktive Mod ohne Fenster im Hintergrund einlesen, damit die Vorschau später nicht wartet.
    /// </summary>
    private void WarmUpKeyIndex(GeneratorPackage package)
    {
        if (AutoNumber.FieldOf(package) is null)
        {
            return;
        }

        foreach (var root in KeyRoots(_host.ModRoot()))
        {
            // Fehler beobachtet der Zwischenspeicher; eine gescheiterte Aufgabe baut die Vorschau neu
            _ = _keyIndexes.Get(root, _lifetime.Token);
        }
    }

    /// <summary>Nach dem Erzeugen hat die Mod neue Schlüssel: Mod-Teil verwerfen und gleich ohne Fenster im Hintergrund neu einlesen.</summary>
    private void RebuildModKeyIndex()
    {
        if (_modKeyIndexRoot is { } modPart)
        {
            _keyIndexes.Invalidate(modPart);
            _ = _keyIndexes.Get(modPart, _lifetime.Token);
        }
    }

    /// <summary>
    /// Die Ordner des Index: die Spielordner und die Mod, wenn sie keiner davon ist. Wechselt die Mod, wird der Teil der vorigen verworfen (einer je Fenster).
    /// </summary>
    private List<string> KeyRoots(string? modRoot)
    {
        var roots = GameKeyRoots();
        if (!string.IsNullOrEmpty(modRoot) && !roots.Contains(modRoot, StringComparer.OrdinalIgnoreCase))
        {
            if (_modKeyIndexRoot is { } previous && !string.Equals(previous, modRoot, StringComparison.OrdinalIgnoreCase) && !roots.Contains(previous, StringComparer.OrdinalIgnoreCase))
            {
                _keyIndexes.Invalidate(previous);
            }

            _modKeyIndexRoot = modRoot;
            roots.Add(modRoot);
        }

        return roots;
    }

    /// <summary>
    /// Alle Schlüssel aus Spiel und Mod: die Spielordner aus dem Zwischenspeicher, die Mod einmal je Mod; "Erzeugen" baut nur den Mod-Teil neu, weil die
    /// Mod danach neue Schlüssel hat. Ist noch ein Teil in Arbeit, zeigt das Fortschrittsfenster einen Schritt je Ordner; Abbrechen wirft
    /// <see cref="OperationCanceledException"/>. Ein schon laufendes Vorwärmen wird dabei nur nicht mehr abgewartet, ein für diese Vorschau gestarteter Bau
    /// wird abgebrochen; der Zwischenspeicher behält keinen von beiden.
    /// </summary>
    private async Task<LocalizationKeyIndex> KeyIndexAsync(string modRoot)
    {
        var roots = KeyRoots(modRoot);
        using var run = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var tasks = roots.Select(root => _keyIndexes.Get(root, run.Token)).ToList();
        if (tasks.All(t => t.IsCompletedSuccessfully))
        {
            return LocalizationKeyIndex.Combine(tasks.Select(t => t.Result));
        }

        var window = new TaskProgressWindow(GeneratorTexts.KeyIndexTitle, GeneratorTexts.KeyIndexHint, SettingsTexts.Cancel, SettingsTexts.ProgressCount, run.Cancel) { Owner = this };
        window.Show();
        var succeeded = false;
        try
        {
            var parts = new List<LocalizationKeyIndex>();
            for (var i = 0; i < tasks.Count; i++)
            {
                window.Report(new CompatProgress(Path.GetFileName(Path.TrimEndingDirectorySeparator(roots[i])), i, tasks.Count));
                parts.Add(await tasks[i].WaitAsync(run.Token));
            }

            succeeded = true;
            return LocalizationKeyIndex.Combine(parts);
        }
        finally
        {
            // Vor dem Dispose von run, sonst riefe das Schließen des Fensters Cancel auf einer verworfenen Quelle auf
            window.CloseAfterFinished();
            if (!succeeded)
            {
                // Scheitert ein Teil, laufen die für diese Vorschau gestarteten Geschwister sonst ohne Verbindung zu _lifetime weiter
                run.Cancel();
            }
        }
    }

    /// <summary>Wie <see cref="TakenKeys"/>, aber nur mit autoNumber und bei unlesbaren Dateien nur mit den Arbeitsständen (für "Neu", das nicht abstürzen darf).</summary>
    private IReadOnlyList<string> TakenKeysForPrompt(string? modRoot)
    {
        if (AutoNumber.FieldOf(_package!) is null)
        {
            return Array.Empty<string>();
        }

        try
        {
            return TakenKeys(_package!, modRoot).ToList();
        }
        catch (Exception ex) when (ex is GeneratorOutputException or IOException or UnauthorizedAccessException)
        {
            return GeneratorStateStore.Keys(_package!.DataFolder);
        }
    }

    /// <summary>Schlüssel, die es schon gibt: Arbeitsstände des Pakets und was der Einleser in den Ordnern (Mod, Ausgabeordner) findet.</summary>
    private static HashSet<string> TakenKeys(GeneratorPackage package, params string?[] roots)
    {
        var keys = new HashSet<string>(GeneratorStateStore.Keys(package.DataFolder), StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)))
        {
            keys.UnionWith(GeneratorStateReader.Keys(package, root!));
        }

        return keys;
    }

    /// <summary>
    /// Plant und prüft die Hashes. Bei Kollision rückt ein neuer Stand (Schlüssel nicht in der Mod) mit <c>autoNumber</c> auf die nächste Nummer ohne Kollision,
    /// wird unter dem neuen Schlüssel gespeichert (die alte Datei entfällt), und <c>Renumbered</c> trägt den Hinweis dafür. Sonst kommen die
    /// Kollisionen als Warnungen in den Plan. Muss der Index erst eingelesen werden und ändert sich währenddessen das Formular oder der Stand (der Plan wurde
    /// verworfen) oder schließt das Fenster, ist das Ergebnis <c>null</c>; Abbrechen im Fortschrittsfenster wirft <see cref="OperationCanceledException"/>.
    /// </summary>
    private async Task<(GeneratorPlan Plan, string? Renumbered)?> PlanWithHashCheckAsync(GeneratorTarget target)
    {
        var package = _package!;
        var state = _state!;
        var generation = _planGeneration;
        var plan = GeneratorPlanner.Plan(package, state, target);
        // Ohne neue Schlüssel (der übliche Fall bei Paketen ohne neue Lokalisierung) braucht es den Index gar nicht
        var newKeys = HashCollisions.NewKeys(plan);
        if (newKeys.Count == 0)
        {
            return (plan, null);
        }

        var index = await KeyIndexAsync(target.ModRoot);
        // Während des Wartens bleibt das Fenster bedienbar: ein inzwischen veralteter Plan darf weder angezeigt werden noch einen Stand umbenennen
        if (_closed || generation != _planGeneration)
        {
            return null;
        }

        var collisions = HashCollisions.Find(newKeys, index);
        if (collisions.Count == 0)
        {
            return (plan, null);
        }

        // Der Schlüssel des Formulars; _state.Key bleibt bis zum Speichern der alte, wenn Schlüsselfelder geändert wurden
        var currentKey = PackageKey.Compose(package, state.Head.Values) ?? state.Key;
        var field = AutoNumber.FieldOf(package);
        var warnings = collisions.Select(c => GeneratorsTexts.HashCollision(c.NewKey, c.ExistingKey));
        if (field is null || !int.TryParse(PackageKey.Split(package, currentKey).GetValueOrDefault(field.Id), NumberStyles.None, CultureInfo.InvariantCulture, out var start))
        {
            return (plan.WithWarnings(warnings), null);
        }

        var known = GeneratorStateReader.Keys(package, target.ModRoot).Concat(target.OutputFolder is { Length: > 0 } output && Directory.Exists(output)
            ? GeneratorStateReader.Keys(package, output)
            : Array.Empty<string>());
        if (!AutoNumber.IsNew(package, currentKey, known))
        {
            return (plan.WithWarnings(warnings), null);
        }

        var taken = TakenKeys(package, target.ModRoot, target.OutputFolder);
        taken.Remove(currentKey);
        var result = AutoNumber.Resolve(start, number => taken.Contains(AutoNumber.KeyFor(package, currentKey, number)), number =>
        {
            var clone = GeneratorStateStore.Clone(state);
            PackageKey.Apply(package, clone, AutoNumber.KeyFor(package, currentKey, number));
            return GeneratorPlanner.Plan(package, clone, target);
        }, index);
        var newKey = AutoNumber.KeyFor(package, currentKey, result.Number);
        if (string.Equals(newKey, currentKey, StringComparison.OrdinalIgnoreCase))
        {
            return (result.Plan, null);
        }

        AutoNumber.Rekey(package, state, currentKey, newKey);
        var renumbered = GeneratorTexts.Renumbered(currentKey, newKey, string.Join(Environment.NewLine,
            result.Skipped.Select(s => $"{AutoNumber.KeyFor(package, currentKey, s.Number)}: {GeneratorsTexts.HashCollision(s.Collision.NewKey, s.Collision.ExistingKey)}").Take(10)));
        return (result.Plan, renumbered);
    }

    /// <summary>Zeigt die Warnungen des Plans über der Vorschau.</summary>
    private void ShowPlanWarnings()
    {
        var warnings = _plan?.Warnings ?? Array.Empty<string>();
        _planWarnings.Text = string.Join(Environment.NewLine, warnings.Select(w => "⚠ " + w));
        _planWarnings.Visibility = warnings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
