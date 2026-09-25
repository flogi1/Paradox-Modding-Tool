using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.App;

/// <summary>Eine Konflikt-Zeile in der Auflösungs-UI, bindungsfreundlich um <see cref="MergeConflict"/> herum.</summary>
public sealed class ConflictRow : INotifyPropertyChanged
{
    private bool _preferMod;

    public required MergeConflict Conflict { get; init; }
    public string Path => Conflict.Path;
    public string Description => Conflict.Description;
    public string GroupName => string.Join("/", Conflict.PathSegments);
    public string VanillaOptionLabel => $"Spiel-Update übernehmen: {Describe(Conflict.LeftValue, "Stelle entfernt")}";
    public string ModOptionLabel => $"Mod übernehmen: {Describe(Conflict.RightValue, "Stelle entfernen")}";

    /// <summary>Kurzbeschreibung eines Werts für die Auswahl-Beschriftung (Blöcke nicht als Typname ausgeben).</summary>
    private static string Describe(ScriptValue? value, string whenMissing) => value switch
    {
        null => whenMissing,
        ScriptScalar scalar => scalar.ToString() ?? string.Empty,
        ScriptBlock block => $"{{ Block mit {block.Entries.Count} Einträgen }}",
        _ => string.Empty
    };

    public bool PreferVanilla
    {
        get => !_preferMod;
        set { if (value) { _preferMod = false; OnPropertyChanged(nameof(PreferVanilla)); OnPropertyChanged(nameof(PreferMod)); } }
    }

    public bool PreferMod
    {
        get => _preferMod;
        set { _preferMod = value; OnPropertyChanged(nameof(PreferVanilla)); OnPropertyChanged(nameof(PreferMod)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Zeigt alle Merge-Konflikte (Abschnitt 7) zur manuellen Entscheidung: pro Konflikt steht
/// standardmäßig die Spiel-Update-Fassung (konservatives Verhalten von <see cref="MergeEngine"/>),
/// der Nutzer kann gezielt auf die Mod-Fassung umschalten. Schreibt nichts selbst - liefert nur
/// die vom Nutzer getroffenen Entscheidungen für <see cref="ConflictResolution.ApplyResolutions"/>
/// an den Aufrufer zurück.
/// </summary>
public partial class ConflictResolutionWindow : Window
{
    public List<ConflictRow> Rows { get; }

    public ConflictResolutionWindow(IReadOnlyList<MergeConflict> conflicts)
    {
        InitializeComponent();
        Rows = new List<ConflictRow>();
        foreach (var conflict in conflicts)
        {
            Rows.Add(new ConflictRow { Conflict = conflict });
        }

        ConflictsList.ItemsSource = Rows;
    }

    /// <summary>Nur für Konflikte gesetzt, bei denen der Nutzer "Mod übernehmen" gewählt hat.</summary>
    public IReadOnlyList<ConflictResolutionChoice> BuildResolutions()
    {
        var resolutions = new List<ConflictResolutionChoice>();
        foreach (var row in Rows)
        {
            if (row.PreferMod)
            {
                resolutions.Add(new ConflictResolutionChoice(row.Conflict));
            }
        }

        return resolutions;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
