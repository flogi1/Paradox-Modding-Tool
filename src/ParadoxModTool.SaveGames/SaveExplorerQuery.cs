namespace ParadoxModTool.SaveGames;

/// <summary>Was der Explorer zeigt: der aktuelle Stand, optional der Vergleichsstand und der Vergleich beider.</summary>
public sealed record SaveView(SaveSnapshot Current, SaveSnapshot? Before, SaveComparison? Comparison);

/// <summary>
/// Filter der Ansicht: <see cref="Search"/> als Teilstring ohne Groß-/Kleinschreibung, <see cref="IsOwnName"/> (null = kein Filter) für
/// "Nur Namen aus meinen Mods", <see cref="OnlyChanges"/> wirkt nur mit Vergleich. <see cref="Tag"/> (null = alle Länder) behält im Reiter
/// "Objekte" nur Objekte dieses Landes; Objekte ohne Land-Tag bleiben.
/// </summary>
public sealed record SaveFilter(string Search, Func<string, bool>? IsOwnName, bool OnlyChanges, string? Tag = null)
{
    public static SaveFilter None { get; } = new(string.Empty, null, false);
}

/// <summary>Zeile der Namensliste; <see cref="Change"/> ist null ohne Vergleich.</summary>
public sealed record SaveNameRow(string Name, SaveNameKind Kind, int ObjectCount, SaveChange? Change);

/// <summary>Zeile des Objektbaums; <see cref="Change"/> ist null ohne Vergleich.</summary>
public sealed record SaveObjectRow(SaveObjectRef Object, int EntryCount, SaveChange? Change);

/// <summary>Zeile der Wertetabelle; <see cref="Before"/> und <see cref="Change"/> sind null ohne Vergleich.</summary>
public sealed record SaveValueRow(SaveObjectRef Object, string Name, SaveNameKind Kind, string Type, string Value, string? Before, SaveChange? Change);

/// <summary>Baut die Zeilen der beiden Reiter aus Stand, Vergleich und Filter (ohne WPF, testbar).</summary>
public static class SaveExplorerQuery
{
    public static IReadOnlyList<SaveNameRow> Names(SaveView view, SaveFilter filter)
    {
        var rows = new Dictionary<(string, SaveNameKind), SaveNameRow>();
        foreach (var name in view.Current.Names)
        {
            rows[(name.Name, name.Kind)] = new SaveNameRow(name.Name, name.Kind, name.ObjectCount, NameChange(view, name.Name, name.Kind));
        }

        if (view.Before is not null && view.Comparison is not null)
        {
            foreach (var name in view.Before.Names)
            {
                rows.TryAdd((name.Name, name.Kind), new SaveNameRow(name.Name, name.Kind, 0, SaveChange.Removed));
            }
        }

        return rows.Values
            .Where(row => Matches(row.Name, filter.Search) && IsOwn(row.Name, filter) && KeepChange(row.Change, filter.OnlyChanges))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ThenBy(row => row.Kind)
            .ToList();
    }

    public static IReadOnlyList<SaveObjectRow> Objects(SaveView view, SaveFilter filter)
    {
        var rows = new List<SaveObjectRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reference in view.Current.ObjectsWithVariables)
        {
            seen.Add(reference.OwnerPath);
            var entries = view.Current.EntriesOf(reference.OwnerPath);
            if (ObjectMatches(reference, entries, filter))
            {
                rows.Add(new SaveObjectRow(reference, entries.Count, view.Comparison?.ChangeOfObject(reference.OwnerPath)));
            }
        }

        if (view.Before is not null && view.Comparison is not null)
        {
            foreach (var reference in view.Before.ObjectsWithVariables.Where(r => !seen.Contains(r.OwnerPath)))
            {
                var entries = view.Before.EntriesOf(reference.OwnerPath);
                if (ObjectMatches(reference, entries, filter))
                {
                    rows.Add(new SaveObjectRow(reference, entries.Count, SaveChange.Removed));
                }
            }
        }

        return rows
            .Where(row => KeepChange(row.Change, filter.OnlyChanges))
            .OrderBy(row => row.Object.Kind.Order)
            .ThenBy(row => row.Object.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Land-Tags der Objekte mit Variablen im aktuellen Stand, alphabetisch (für den Filter "Land:").</summary>
    public static IReadOnlyList<string> Tags(SaveView view) =>
        view.Current.ObjectsWithVariables
            .Select(o => o.Tag)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<SaveValueRow> ValuesForName(SaveView view, string name, SaveNameKind kind, bool onlyChanges)
    {
        var context = new Context(view);
        var rows = kind switch
        {
            SaveNameKind.Variable => context.Variables(view.Current.EntriesFor(name, SaveNameKind.Variable), view.Comparison?.Entries.Where(d => d.Name == name && d.Kind == SaveEntryKind.Variable)),
            SaveNameKind.List => context.ListItems(view.Current.EntriesFor(name, SaveNameKind.List), view.Comparison?.Entries.Where(d => d.Name == name && d.Kind == SaveEntryKind.ListItem)),
            _ => context.FlagValues(name),
        };

        return Sort(rows.Where(row => KeepChange(row.Change, onlyChanges)));
    }

    public static IReadOnlyList<SaveValueRow> ValuesForObject(SaveView view, string ownerPath, SaveFilter filter)
    {
        var context = new Context(view);
        var entries = view.Current.EntriesOf(ownerPath);
        var diffs = view.Comparison?.Entries.Where(d => d.OwnerPath == ownerPath).ToList();
        var rows = context.Variables(entries.Where(e => e.Kind == SaveEntryKind.Variable), diffs?.Where(d => d.Kind == SaveEntryKind.Variable))
            .Concat(context.ListItems(entries.Where(e => e.Kind == SaveEntryKind.ListItem), diffs?.Where(d => d.Kind == SaveEntryKind.ListItem)));

        return rows
            .Where(row => Matches(row.Name, filter.Search) && IsOwn(row.Name, filter) && KeepChange(row.Change, filter.OnlyChanges))
            .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ThenBy(row => row.Kind)
            .ToList();
    }

    private static SaveChange? NameChange(SaveView view, string name, SaveNameKind kind)
    {
        if (view.Before is null || view.Comparison is null)
        {
            return null;
        }

        if (kind != SaveNameKind.FlagValue)
        {
            return view.Comparison.ChangeOfName(name);
        }

        var now = view.Current.EntriesFor(name, kind).Select(e => e.OwnerPath).ToHashSet(StringComparer.Ordinal);
        var was = view.Before.EntriesFor(name, kind).Select(e => e.OwnerPath).ToHashSet(StringComparer.Ordinal);
        return was.Count == 0 ? SaveChange.Added
            : now.Count == 0 ? SaveChange.Removed
            : now.SetEquals(was) ? SaveChange.Unchanged
            : SaveChange.Changed;
    }

    private static bool ObjectMatches(SaveObjectRef reference, IReadOnlyList<SaveVariableEntry> entries, SaveFilter filter) =>
        (Matches(reference.DisplayName, filter.Search) || (reference.Id is not null && Matches(reference.Id, filter.Search)))
        && (filter.Tag is null || reference.Tag is null || string.Equals(reference.Tag, filter.Tag, StringComparison.OrdinalIgnoreCase))
        && (filter.IsOwnName is null || entries.Any(e => filter.IsOwnName(e.Name)));

    private static bool Matches(string text, string search) =>
        search.Length == 0 || text.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static bool IsOwn(string name, SaveFilter filter) => filter.IsOwnName is null || filter.IsOwnName(name);

    private static bool KeepChange(SaveChange? change, bool onlyChanges) =>
        !onlyChanges || change is null || change != SaveChange.Unchanged;

    private static IReadOnlyList<SaveValueRow> Sort(IEnumerable<SaveValueRow> rows) =>
        rows.OrderBy(row => row.Object.Kind.Order)
            .ThenBy(row => row.Object.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Formatierer und Namensauflösung beider Stände für eine Abfrage.</summary>
    private sealed class Context
    {
        private readonly SaveView _view;
        private readonly SaveValueFormat _current;
        private readonly SaveValueFormat? _before;

        public Context(SaveView view)
        {
            _view = view;
            _current = new SaveValueFormat(view.Current);
            _before = view.Before is null || view.Comparison is null ? null : new SaveValueFormat(view.Before);
        }

        public IEnumerable<SaveValueRow> Variables(IEnumerable<SaveVariableEntry> entries, IEnumerable<SaveDiffEntry>? diffs)
        {
            foreach (var entry in entries)
            {
                var diff = _view.Comparison?.Find(entry.OwnerPath, entry.Name, SaveEntryKind.Variable);
                var before = diff?.Before is { } was ? _before?.Format(was) : null;
                yield return Row(entry, CurrentObject(entry.OwnerPath), SaveNameKind.Variable, _current.Format(entry), before, diff?.Change);
            }

            foreach (var diff in diffs?.Where(d => d.Change == SaveChange.Removed) ?? Enumerable.Empty<SaveDiffEntry>())
            {
                yield return Row(diff.Before!, BeforeObject(diff.OwnerPath), SaveNameKind.Variable, string.Empty, _before?.Format(diff.Before!), SaveChange.Removed);
            }
        }

        public IEnumerable<SaveValueRow> ListItems(IEnumerable<SaveVariableEntry> entries, IEnumerable<SaveDiffEntry>? diffs)
        {
            if (diffs is null || _before is null)
            {
                foreach (var entry in entries)
                {
                    yield return Row(entry, CurrentObject(entry.OwnerPath), SaveNameKind.List, _current.Format(entry), null, null);
                }

                yield break;
            }

            foreach (var diff in diffs)
            {
                var entry = diff.After ?? diff.Before!;
                var reference = diff.After is null ? BeforeObject(diff.OwnerPath) : CurrentObject(diff.OwnerPath);
                var value = diff.After is null ? string.Empty : _current.Format(diff.After);
                var before = diff.Before is null ? null : _before.Format(diff.Before);
                yield return Row(entry, reference, SaveNameKind.List, value, before, diff.Change);
            }
        }

        public IEnumerable<SaveValueRow> FlagValues(string flag)
        {
            var current = _view.Current.EntriesFor(flag, SaveNameKind.FlagValue);
            if (_view.Before is null || _before is null)
            {
                foreach (var entry in current)
                {
                    yield return Row(entry, CurrentObject(entry.OwnerPath), SaveNameKind.FlagValue, _current.Format(entry), null, null);
                }

                yield break;
            }

            var was = _view.Before.EntriesFor(flag, SaveNameKind.FlagValue).GroupBy(e => (e.OwnerPath, e.Name)).ToDictionary(g => g.Key, g => g.First());
            foreach (var entry in current)
            {
                var existed = was.Remove((entry.OwnerPath, entry.Name), out var old);
                yield return Row(entry, CurrentObject(entry.OwnerPath), SaveNameKind.FlagValue, _current.Format(entry), existed ? _before.Format(old!) : null, existed ? SaveChange.Unchanged : SaveChange.Added);
            }

            foreach (var old in was.Values)
            {
                yield return Row(old, BeforeObject(old.OwnerPath), SaveNameKind.FlagValue, string.Empty, _before.Format(old), SaveChange.Removed);
            }
        }

        private SaveObjectRef CurrentObject(string ownerPath) => _view.Current.Objects.Resolve(ownerPath);

        private SaveObjectRef BeforeObject(string ownerPath) => (_view.Before ?? _view.Current).Objects.Resolve(ownerPath);

        private static SaveValueRow Row(SaveVariableEntry entry, SaveObjectRef reference, SaveNameKind kind, string value, string? before, SaveChange? change) =>
            new(reference, entry.Name, kind, entry.Type, value, before, change);
    }
}
