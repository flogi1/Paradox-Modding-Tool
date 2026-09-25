using System.ComponentModel;
using System.Globalization;
using System.Windows;
using ParadoxModTool.Mods;
using ParadoxModTool.Templates;

namespace ParadoxModTool.App;

/// <summary>Gemeinsamer Zugriff der Prüflisten-Zeilen auf den Stand des Fensters.</summary>
public sealed class PointRowContext
{
    /// <summary>Anzahl der Durchläufe.</summary>
    public required Func<int> Instances { get; init; }

    /// <summary>Wurde der Punkt gegenüber der automatischen Erkennung geändert?</summary>
    public required Func<VariationPoint, bool> IsChanged { get; init; }

    /// <summary>Wie oft die Wiederholungsgruppe mit diesem Index wiederholt wird.</summary>
    public required Func<int, int> RepeatCount { get; init; }

    /// <summary>Name der allgemeinen Regel für mehrere Zahlen, wie im Feld "Mehrere Zahlen" (z. B. "erste zählt").</summary>
    public required Func<string> MultiRuleText { get; init; }

    /// <summary>Ob der Punkt nach der automatischen Erkennung (ohne deine Änderungen) zählen würde; <see langword="null"/>, wenn unbekannt.</summary>
    public required Func<VariationPoint, bool?> DefaultEnabled { get; init; }

    /// <summary>Die Zahlen der Bezeichner dieser Form und ob sie zählen; <see langword="null"/>, wenn die Form nicht (mehr) vorkommt.</summary>
    public required Func<string, ShapeInfo?> GetShape { get; init; }

    /// <summary>Legt fest, welche Zahlen (Nummern ab 0) für alle Bezeichner dieser Form zählen; <see langword="null"/> stellt die allgemeine Regel wieder her.</summary>
    public required Action<string, IReadOnlyCollection<int>?> SetShapeNumbers { get; init; }
}

/// <summary>Zeile der Prüfliste: ein Zählpunkt mit den Feldern, die der Nutzer ändern darf.</summary>
public sealed class PointRow : INotifyPropertyChanged
{
    private readonly Action _changed;
    private readonly PointRowContext _context;

    public PointRow(VariationPoint point, Action changed, PointRowContext context)
    {
        Point = point;
        _changed = changed;
        _context = context;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public VariationPoint Point { get; }

    /// <summary>Lässt alle Anzeigen der Zeile neu lesen, nachdem sich irgendein Wert geändert hat.</summary>
    public void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

    public bool Enabled
    {
        get => Point.Enabled;
        set
        {
            if (Point.Enabled != value)
            {
                Point.Enabled = value;
                _changed();
            }
        }
    }

    public string Label => Point.Label;

    private ShapeInfo? Shape => Point.Kind == PointKind.Number ? null : _context.GetShape(Point.Shape);

    public Visibility ShapeVisibility => Shape is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ResetVisibility => Shape?.HasCustomRule == true ? Visibility.Visible : Visibility.Collapsed;

    public string ShapeToolTip => "Die Zahlen in diesem Namen (Form " + Point.Shape + "; # steht für eine Zahl). Angehakte Zahlen zählen hoch, die anderen bleiben gleich. "
        + "Die Auswahl gilt für alle Bezeichner dieser Form und ersetzt dafür die allgemeine Regel \"Mehrere Zahlen\". Die fett gezeigte Zahl ist die dieser Zeile.";

    /// <summary>Ein Kästchen je Zahl im Namen, in der Reihenfolge von links.</summary>
    public IReadOnlyList<ShapeNumberItem> ShapeNumbers
    {
        get
        {
            var shape = Shape;
            if (shape is null)
            {
                return Array.Empty<ShapeNumberItem>();
            }

            return shape.Digits.Select((digits, index) => new ShapeNumberItem(digits, shape.Counted[index], index == Point.RunIndex, isCounted =>
            {
                var current = _context.GetShape(Point.Shape);
                if (current is null)
                {
                    return;
                }

                var numbers = Enumerable.Range(0, current.Digits.Count).Where(i => i == index ? isCounted : current.Counted[i]).ToList();
                _context.SetShapeNumbers(Point.Shape, numbers);
            })).ToList();
        }
    }

    /// <summary>Zurück zur allgemeinen Regel für diese Form.</summary>
    public void ResetRule() => _context.SetShapeNumbers(Point.Shape, null);

    private static readonly System.Windows.Media.Brush CountingBrush = MakeBrush(0x30, 0xFF, 0xC1, 0x07);

    private static System.Windows.Media.Brush MakeBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <summary>Hintergrund der Zeile: gelb getönt, wenn der Punkt zählt (wie die Markierung im Ergebnis).</summary>
    public System.Windows.Media.Brush RowBrush => Point.Enabled ? CountingBrush : System.Windows.Media.Brushes.Transparent;

    public Visibility ChangedVisibility => _context.IsChanged(Point) ? Visibility.Visible : Visibility.Collapsed;

    public string OccurrenceText => Point.Occurrences.Count + "×";

    /// <summary>Die Werte, die dieser Punkt im Ergebnis annimmt, damit sich Änderungen nachvollziehen lassen.</summary>
    public string Values
    {
        get
        {
            if (!Point.Enabled)
            {
                return "bleibt unverändert";
            }

            const int shown = 4;
            var instances = _context.Instances();
            string List(Func<int, string> value, int total) =>
                string.Join(", ", Enumerable.Range(0, Math.Min(total, shown)).Select(value)) + (total > shown ? ", …" : string.Empty);

            if (Point.RepeatGroup >= 0)
            {
                var repeats = _context.RepeatCount(Point.RepeatGroup);
                var text = "Wdh.: " + List(j => Point.Format(0, j), repeats);
                if (Point.Step != 0)
                {
                    text += " · 2. Durchlauf: " + List(j => Point.Format(1, j), repeats);
                }

                return text;
            }

            return "wird " + List(i => Point.Format(i, 0), instances);
        }
    }

    /// <summary>Warum der Punkt zählt oder nicht zählt: welche Regel dafür verantwortlich ist.</summary>
    public string Note
    {
        get
        {
            var reason = BaseReason();
            if (_context.DefaultEnabled(Point) is { } byDefault && byDefault != Point.Enabled)
            {
                reason = (Point.Enabled ? "von dir eingeschaltet" : "von dir ausgeschaltet") + " (sonst: " + (byDefault ? "zählt" : "aus") + ")";
            }

            return reason + (Point.RepeatGroup >= 0 ? " · in Wiederholung" : string.Empty);
        }
    }

    private string BaseReason()
    {
        if (Point.Enabled)
        {
            if (Point.LearnedFromExamples)
            {
                return "zählt: in den Beispielen erkannt";
            }

            if (Point.HasCustomRule)
            {
                return "zählt: bei der eigenen Auswahl für diese Form angehakt";
            }

            if (Point.CountedByRule.Length > 0)
            {
                return "zählt: " + Point.CountedByRule;
            }

            if (Point.RunCount >= 2)
            {
                return "zählt: allgemeine Regel „" + _context.MultiRuleText() + "“";
            }

            return Point.Kind == PointKind.Text ? "zählt: Zahl im Text" : "zählt: Zahl im Bezeichner";
        }

        if (Point.Irregular)
        {
            return "aus: in den Beispielen unregelmäßig";
        }

        if (Point.IgnoredReason.Length > 0)
        {
            return "aus: " + Point.IgnoredReason;
        }

        if (Point.RuleDisabled)
        {
            return Point.HasCustomRule
                ? "aus: bei der eigenen Auswahl für diese Form nicht angehakt"
                : "aus: weitere Zahl oder Namenszahl, allgemeine Regel „" + _context.MultiRuleText() + "“";
        }

        if (Point.ConstantInExamples)
        {
            return "aus: in den Beispielen immer gleich";
        }

        return Point.Kind == PointKind.Number ? "aus: einzelne Zahl (standardmäßig aus)" : "aus";
    }

    public string StartText
    {
        get => Point.Start.ToString(CultureInfo.InvariantCulture);
        set => SetNumber(value, Point.Start, v => Point.Start = v, nameof(StartText));
    }

    public string StepText
    {
        get => Point.Step.ToString(CultureInfo.InvariantCulture);
        set => SetNumber(value, Point.Step, v => Point.Step = v, nameof(StepText));
    }

    public string InnerStepText
    {
        get => Point.InnerStep.ToString(CultureInfo.InvariantCulture);
        set => SetNumber(value, Point.InnerStep, v => Point.InnerStep = v, nameof(InnerStepText));
    }

    public Visibility InnerVisibility => Point.RepeatGroup >= 0 ? Visibility.Visible : Visibility.Hidden;

    private void SetNumber(string text, long current, Action<long> apply, string property)
    {
        if (long.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            if (value != current)
            {
                apply(value);
                _changed();
            }
        }
        else
        {
            // Ungültige Eingabe: alten Wert wieder anzeigen.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}

/// <summary>Zeile der Liste wiederholter Unterblöcke.</summary>
public sealed class GroupRow : INotifyPropertyChanged
{
    private readonly Action _changed;

    public GroupRow(RepeatGroup group, string description, Action changed)
    {
        Group = group;
        Description = description;
        _changed = changed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RepeatGroup Group { get; }

    public string Description { get; }

    public string CountText
    {
        get => Group.Count.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count is >= 1 and <= 200)
            {
                if (count != Group.Count)
                {
                    Group.Count = count;
                    _changed();
                }
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CountText)));
            }
        }
    }
}

/// <summary>Zeile der Lokalisierungstabelle (Schlüsselmuster und Textmuster).</summary>
public sealed class LocRow
{
    private readonly Action _changed;

    public LocRow(LocalizationRow row, Action changed)
    {
        Row = row;
        _changed = changed;
    }

    public LocalizationRow Row { get; }

    public string KeyPattern => Row.KeyPattern;

    /// <summary>Warum die Zeile vorgeschlagen wird (z. B. "Objektname · Endung _desc" oder "Feld text").</summary>
    public string Origin => Row.Origin;

    public bool Selected
    {
        get => Row.Selected;
        set
        {
            if (Row.Selected != value)
            {
                Row.Selected = value;
                _changed();
            }
        }
    }

    public string TextPattern
    {
        get => Row.TextPattern;
        set
        {
            if (Row.TextPattern != value)
            {
                Row.TextPattern = value;
                _changed();
            }
        }
    }
}

/// <summary>Zeile der Vorschau erzeugter Einträge; eine Änderung des Textes wird als Einzeländerung gemerkt.</summary>
public sealed class EntryRow
{
    private readonly Action<string, string> _edited;
    private string _text;

    public EntryRow(LocalizationEntry entry, Action<string, string> edited)
    {
        Key = entry.Key;
        _text = entry.Text;
        _edited = edited;
    }

    public string Key { get; }

    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                _edited(Key, value);
            }
        }
    }
}

/// <summary>Was das Fenster vom Hauptfenster braucht (Einfügen, neues Dokument, offene Tabs, Ordner). Das Fenster kennt das Hauptfenster selbst nicht.</summary>
public sealed class TemplateGeneratorHost
{
    public required Action<string> InsertAtCaret { get; init; }

    public required Action<string> NewDocument { get; init; }

    /// <summary>Text des offenen Dokuments mit diesem Pfad; <see langword="null"/>, wenn die Datei nicht offen ist.</summary>
    public required Func<string, string?> ReadOpenDocument { get; init; }

    /// <summary>Ersetzt den Text des offenen Dokuments mit diesem Pfad; <see langword="false"/>, wenn es nicht offen ist.</summary>
    public required Func<string, string, bool> ReplaceOpenDocumentText { get; init; }

    /// <summary>Ordner der Mod, in dem der Dateidialog beginnt; kann <see langword="null"/> sein.</summary>
    public required Func<string?> ModRoot { get; init; }

    public required Func<ICSharpCode.AvalonEdit.Highlighting.IHighlightingDefinition?> Highlighting { get; init; }

    /// <summary>Alle Mods dieses Spiels (für die Mod-Auswahl).</summary>
    public required Func<IReadOnlyList<ModInfo>> Mods { get; init; }

    /// <summary>Die im Hauptfenster erkannte Mod; <see langword="null"/>, wenn keine erkannt ist.</summary>
    public required Func<ModInfo?> ActiveMod { get; init; }

    /// <summary>Ordner der Vorlagen einer Mod.</summary>
    public required Func<ModInfo, string> ModTemplateDirectory { get; init; }

    /// <summary>Kennung des Spiels dieses Fensters (für die Spieleliste der Regeln auf "Alle Spiele").</summary>
    public required string GameId { get; init; }

    /// <summary>Regeldatei der Ebene "Alle Spiele".</summary>
    public required string GlobalRulesFile { get; init; }

    /// <summary>Regeldatei der Ebene "Spiel".</summary>
    public required string GameRulesFile { get; init; }

    /// <summary>Regeldatei der Ebene "Mod" für eine Mod.</summary>
    public required Func<ModInfo, string> ModRulesFile { get; init; }

    /// <summary>Alle bekannten Spiele (Kennung, Anzeigename), für die Spielauswahl bei Regeln auf "Alle Spiele".</summary>
    public required Func<IReadOnlyList<(string Id, string Name)>> Games { get; init; }

    /// <summary>Die gelernten Lokalisierungs-Profile des Spiels; <see langword="null"/>, solange nichts gelernt ist.</summary>
    public required Func<LocalizationProfiles?> LocProfiles { get; init; }

    /// <summary>Ein Satz zum Stand der Profile (gelernt am … / Grund, warum es keine gibt).</summary>
    public required Func<string> LocProfileStatus { get; init; }

    /// <summary>Stößt das Lernen der Profile an, falls nötig, und wartet, bis es fertig ist.</summary>
    public required Func<Task> EnsureLocProfiles { get; init; }

    /// <summary>Der Ordner der zuletzt aktiven Datei relativ zum Spielordner (z. B. <c>common/building_types</c>); <see langword="null"/>, wenn unbekannt.</summary>
    public required Func<string?> SourceRelativeFolder { get; init; }
}

/// <summary>Eintrag der Mod-Auswahl im Template-Fenster; <see cref="Mod"/> ist <see langword="null"/> für "keine Mod".</summary>
public sealed class ModChoice
{
    public ModChoice(ModInfo? mod, bool detected)
    {
        Mod = mod;
        Detected = detected;
    }

    public ModInfo? Mod { get; }

    public bool Detected { get; }

    public override string ToString() => Mod is null ? "keine Mod" : Detected ? Mod.Name + " (erkannt)" : Mod.Name;
}

/// <summary>Eintrag der Vorlagenliste: Name und Ebene.</summary>
public sealed class TemplateListItem
{
    public TemplateListItem(string name, StoreLayer layer)
    {
        Name = name;
        Layer = layer;
    }

    public string Name { get; }

    public StoreLayer Layer { get; }

    public override string ToString() => Layer == StoreLayer.Mod ? Name + "  (Mod)" : Name;
}

/// <summary>Ein Kästchen für eine Zahl im Namen (siehe <see cref="PointRow.ShapeNumbers"/>).</summary>
public sealed class ShapeNumberItem
{
    private readonly Action<bool> _changed;
    private bool _counted;

    public ShapeNumberItem(string digits, bool counted, bool own, Action<bool> changed)
    {
        Digits = digits;
        _counted = counted;
        IsOwn = own;
        _changed = changed;
    }

    public string Digits { get; }

    public bool IsOwn { get; }

    public FontWeight Weight => IsOwn ? FontWeights.Bold : FontWeights.Normal;

    public bool IsCounted
    {
        get => _counted;
        set
        {
            if (_counted != value)
            {
                _counted = value;
                _changed(value);
            }
        }
    }
}

/// <summary>Eintrag der Auswahl "Dateityp": der Pfad des Profils (leer = automatisch, <c>*fallback</c> = Rückfall) und der Anzeigetext.</summary>
public sealed record LocProfileChoice(string Path, string Text)
{
    public override string ToString() => Text;
}
