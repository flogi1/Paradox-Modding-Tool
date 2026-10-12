using System.IO;
using System.Windows;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Candidates;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Settings;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// "Modifier ohne Idee" (Spec 2026-10-12-iv2-dynamic-ideas-3-candidates): der Knopf öffnet das Kandidatenfenster, und "Anlegen…" darin erzeugt aus den gewählten
/// Modifiern neue Arbeitsstände (Angaben einmal fragen, offizielle Namen lesen, Stapel erzeugen, speichern, ersten zeigen).
/// </summary>
public partial class GeneratorWindow
{
    private const int CreatedKeysShown = 10;

    private CandidateWindow? _candidateWindow;

    private void Candidates_Click(object sender, RoutedEventArgs e)
    {
        if (_package is not { Candidates: { } def } package)
        {
            return;
        }

        var modRoot = _state?.ModRoot ?? _host.ModRoot();
        if (modRoot is null || !Directory.Exists(modRoot))
        {
            MessageBox.Show(this, GeneratorTexts.NoMod, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_candidateWindow is { } open)
        {
            if (open.WindowState == WindowState.Minimized)
            {
                open.WindowState = WindowState.Normal;
            }

            open.Activate();
            return;
        }

        var window = new CandidateWindow(this, package, () => _state?.ModRoot ?? _host.ModRoot(), () => _host.CandidateSources(def.Category), CreateFromCandidates);
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_candidateWindow, window))
            {
                _candidateWindow = null;
            }
        };
        _candidateWindow = window;
        window.Show();
    }

    /// <summary>
    /// Legt aus den gewählten Kandidaten Arbeitsstände an: die Angaben des Pakets einmal fragen und prüfen, die offiziellen Namen je Sprache des Stands lesen
    /// (mit Fortschritt und Abbruch), ungespeicherte Änderungen klären, den Stapel erzeugen, alle speichern und den ersten zeigen.
    /// <see langword="true"/>, wenn Arbeitsstände entstanden sind; bei Abbruch oder Fehler entsteht nichts.
    /// </summary>
    internal async Task<bool> CreateFromCandidates(IReadOnlyList<CandidateRow> rows)
    {
        if (_package is not { Candidates: { } def } package || _renderer is not { } renderer || rows.Count == 0)
        {
            return false;
        }

        var modRoot = _state?.ModRoot ?? _host.ModRoot();
        if (modRoot is null || !Directory.Exists(modRoot))
        {
            MessageBox.Show(this, GeneratorTexts.NoMod, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        // Steht ein gewählter Modifier schon in einem Arbeitsstand, entstünde mit "Anlegen…" ein zweiter: erst nachfragen
        if (rows.Where(r => r.StateKey is not null).ToList() is { Count: > 0 } existing)
        {
            var list = string.Join(Environment.NewLine, existing.Take(CreatedKeysShown).Select(r => r.Key + " → " + r.StateKey))
                + (existing.Count > CreatedKeysShown ? Environment.NewLine + "…" : string.Empty);
            if (MessageBox.Show(this, GeneratorTexts.CandidatesDuplicateConfirm(list), GeneratorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return false;
            }
        }

        if (CandidateAskWindow.Ask(this, package, rows.Count) is not { } asked)
        {
            return false;
        }

        // Die Sprachen eines neuen Stands; Englisch kommt für den Namen immer dazu (er steht im Kopf, auch wenn das Spielprofil kein Englisch hat)
        var languages = GeneratorState.CreateEmpty(package, _host.ProfileLanguages()).Languages;
        try
        {
            if (CandidateBatch.AskIssues(package, renderer, asked, languages) is { Count: > 0 } issues)
            {
                MessageBox.Show(this, string.Join(Environment.NewLine, issues), GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or GeneratorTemplateException)
        {
            MessageBox.Show(this, ex.Message, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // Das Fenster bleibt bedienbar, solange das Fortschrittsfenster offen ist: nichts davon darf den Stapel verändern
        IsEnabled = false;
        try
        {
            var names = await LoadModifierNamesAsync(rows.Select(r => r.Key).ToList(), languages.Append("english").Distinct(StringComparer.Ordinal).ToList());
            if (names is null || _closed || !ReferenceEquals(_package, package))
            {
                return false;
            }

            // Erst jetzt nach ungespeicherten Änderungen fragen: bricht man vorher ab (Duplikat, Angaben, Namen), bleibt die Eingabe im Formular unberührt
            if (!ConfirmLeave())
            {
                return false;
            }

            var picks = rows.Select(r => new CandidatePick(
                r.Key,
                r.Boolean,
                names.Where(l => l.Value.ContainsKey(r.Key)).ToDictionary(l => l.Key, l => l.Value[r.Key], StringComparer.Ordinal))).ToList();
            IReadOnlyList<GeneratorState> states;
            var cursor = Cursor;
            Cursor = System.Windows.Input.Cursors.Wait;
            try
            {
                states = CandidateBatch.Create(package, picks, asked, TakenKeys(package, modRoot), languages, modRoot);
                foreach (var state in states)
                {
                    GeneratorStateStore.Save(package.DataFolder, state);
                }
            }
            finally
            {
                Cursor = cursor;
            }

            _dirty = false;
            ShowState(states[0]);
            RefreshStates(states[0].Key);
            var keys = states.Select(s => s.Key).ToList();
            MessageBox.Show(this,
                GeneratorTexts.CandidatesCreated(states.Count, string.Join(", ", keys.Take(CreatedKeysShown)) + (keys.Count > CreatedKeysShown ? " …" : string.Empty)),
                GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }
        catch (Exception ex) when (ex is GeneratorOutputException or GeneratorTemplateException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            // Schon gespeicherte Stände der Liste hinzufügen, auch wenn der Stapel nicht ganz durchlief
            RefreshStates();
            MessageBox.Show(this, ex.Message, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        finally
        {
            IsEnabled = true;
        }
    }

    /// <summary>
    /// Liest die offiziellen Namen der Modifier, eine Sprache nach der anderen im Hintergrund (ein Schritt je Sprache im Fortschrittsfenster).
    /// Sprache → Modifier → Name; <see langword="null"/> bei Abbruch.
    /// </summary>
    private async Task<Dictionary<string, IReadOnlyDictionary<string, string>>?> LoadModifierNamesAsync(IReadOnlyList<string> modifiers, IReadOnlyList<string> languages)
    {
        var names = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        using var cancel = new CancellationTokenSource();
        var window = new TaskProgressWindow(GeneratorTexts.CandidatesNamesTitle, GeneratorTexts.CandidatesNamesHint, SettingsTexts.Cancel, SettingsTexts.ProgressCount, cancel.Cancel) { Owner = this };
        window.Show();
        try
        {
            for (var i = 0; i < languages.Count; i++)
            {
                window.Report(new CompatProgress(languages[i], i, languages.Count));
                var language = languages[i];
                names[language] = await Task.Run(() => _host.ModifierNames(modifiers, language)).WaitAsync(cancel.Token);
            }

            return names;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            // Vor dem Dispose der Quelle, sonst riefe das Schließen des Fensters Cancel auf einer verworfenen Quelle auf
            window.CloseAfterFinished();
        }
    }
}
