using System.Text.RegularExpressions;
using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Patch = gewünschter Zustand minus Spielzustand ohne Patch (Spec, Schritt 4; Rulings 7, 10, 12 im Plan).</summary>
public static class PatchPlanner
{
    public const string OwnFileName = "pmt_compatch";

    public static PatchPlan Plan(ResolvedEntries resolved, ApplyResult applied, CompatchInput input, PatchTarget target, IReadOnlyDictionary<EntryAddress, PatchForm>? formChoices = null)
    {
        var plan = new PatchPlan();
        var wholeFiles = new Dictionary<string, List<EntryAddress>>(StringComparer.OrdinalIgnoreCase);
        void Collect(string path, EntryAddress address)
        {
            if (!wholeFiles.TryGetValue(path, out var list))
            {
                list = new List<EntryAddress>();
                wholeFiles[path] = list;
            }

            list.Add(address);
        }

        foreach (var (address, state) in applied.Desired.OrderBy(d => d.Key.Id, StringComparer.Ordinal))
        {
            var info = resolved.Addresses[address];
            switch (address.Kind)
            {
                case AddressKind.WholeFile:
                    if (state.FileChoice == info.NoPatch.Side)
                    {
                        plan.NotNeeded.Add(address);
                        break;
                    }

                    var chosen = state.FileChoice switch { MergeSide.A => info.A, MergeSide.B => info.B, _ => info.Vanilla };
                    plan.Files.Add(new PatchFileOp(chosen.RelativePath ?? address.Scope, null, chosen.FullPath, false, new[] { address }, false, true));
                    break;

                case AddressKind.FileRest:
                    if (StructuralEquality.Equivalent(state.Entries, info.NoPatch.Entries))
                    {
                        plan.NotNeeded.Add(address);
                        break;
                    }

                    Collect(info.NoPatch.RelativePath ?? address.Scope, address);
                    break;

                default:
                    if (StructuralEquality.Equivalent(state.Entries, info.NoPatch.Entries))
                    {
                        plan.NotNeeded.Add(address);
                        break;
                    }

                    var forms = AvailableForms(info, state.Entries, input);
                    plan.AvailableForms[address] = forms;
                    var form = formChoices is not null && formChoices.TryGetValue(address, out var wanted) && forms.Contains(wanted) ? wanted : forms[0];
                    if (form == PatchForm.WholeFile)
                    {
                        Collect(info.NoPatch.RelativePath ?? info.A.RelativePath ?? info.B.RelativePath ?? info.Vanilla.RelativePath ?? address.Scope, address);
                        break;
                    }

                    plan.Entries.Add(EntryOp(info, state, form, target, resolved));
                    break;
            }
        }

        foreach (var (path, addresses) in wholeFiles.OrderBy(w => w.Key, StringComparer.OrdinalIgnoreCase))
        {
            plan.Files.Add(FileOp(path, addresses, resolved, applied, input));
        }

        return plan;
    }

    public static IReadOnlyList<PatchForm> AvailableForms(AddressInfo info, IReadOnlyList<ScriptEntry> desired, CompatchInput input)
    {
        var forms = new List<PatchForm>();
        bool Has(string op) => input.KeyOperators.Contains(op, StringComparer.OrdinalIgnoreCase);
        if (info.Address.Kind == AddressKind.Key && desired.Count > 0)
        {
            switch (info.Behavior)
            {
                case OverrideBehavior.PerKey:
                    // Ein REPLACE: läuft nach jedem INJECT: und würde die Ergänzung des Kompatchs wieder löschen (Prüfbefund C1).
                    if (Has("INJECT") && !info.NoPatch.Operators.Contains(OverrideOperator.Replace) && InjectExtras(info.NoPatch.Entries, desired) is not null)
                    {
                        forms.Add(PatchForm.Inject);
                    }

                    if (Has("REPLACE") && info.NoPatch.Exists)
                    {
                        forms.Add(PatchForm.Replace);
                    }

                    if (!info.NoPatch.Exists && info.Certain)
                    {
                        forms.Add(PatchForm.PlainKey);
                    }

                    break;
                case OverrideBehavior.LocalizationKeys when info.Certain:
                    forms.Add(PatchForm.Replace);
                    break;
                case OverrideBehavior.LastWins or OverrideBehavior.FirstWins or OverrideBehavior.PartialKeys when info.Certain:
                    forms.Add(PatchForm.PlainKey);
                    break;
            }
        }

        forms.Add(PatchForm.WholeFile);
        return forms;
    }

    /// <summary>
    /// Die Ergänzungen, wenn der gewünschte Block mit allen Einträgen des Blocks ohne Patch beginnt (gleiche Reihenfolge) und danach
    /// weitere folgen; sonst <see langword="null"/>. <c>INJECT:</c> hängt hinten an, deshalb nur dann (Prüfbefund I5: ein eingefügtes
    /// <c>else_if</c> vor <c>else</c> darf nicht hinter <c>else</c> landen).
    /// </summary>
    internal static IReadOnlyList<ScriptEntry>? InjectExtras(IReadOnlyList<ScriptEntry> noPatch, IReadOnlyList<ScriptEntry> desired)
    {
        if (noPatch.Count != 1 || desired.Count != 1
            || noPatch[0] is not ScriptStatement { Value: ScriptBlock have } before
            || desired[0] is not ScriptStatement { Value: ScriptBlock want } after
            || before.Key != after.Key)
        {
            return null;
        }

        var haveEntries = have.Entries.Where(e => e is not ScriptCommentEntry).ToList();
        var wantEntries = want.Entries.Where(e => e is not ScriptCommentEntry).ToList();
        if (wantEntries.Count <= haveEntries.Count)
        {
            return null;
        }

        for (var i = 0; i < haveEntries.Count; i++)
        {
            if (!StructuralEquality.EntryEqual(haveEntries[i], wantEntries[i]))
            {
                return null;
            }
        }

        return wantEntries.Skip(haveEntries.Count).ToList();
    }

    /// <summary>
    /// Die PMT-eigene Datei im Bereich. Der Name muss nach Dateiname-Reihenfolge gewinnen: bei <c>FirstWins</c> vor allen anderen Dateien
    /// des Bereichs, sonst nach allen (Prüfbefund I1: Mods nutzen <c>zzz_</c>). Deshalb wird das Präfix verlängert, bis es alle schlägt.
    /// Der Name kann sich ändern, wenn eine Mod neue Dateien bekommt; Plan 2 räumt frühere PMT-Dateien des Bereichs dann auf.
    /// </summary>
    internal static string OwnFile(AddressInfo info, ResolvedEntries resolved)
    {
        var scope = info.Address.Scope;
        if (info.Behavior == OverrideBehavior.LocalizationKeys)
        {
            var language = info.Address.Key[..info.Address.Key.IndexOf('/')];
            return $"{scope}/{language}/replace/zz_{OwnFileName}_l_{language}.yml";
        }

        var firstWins = info.Behavior == OverrideBehavior.FirstWins;
        var extension = IsGuiKey(info.Address.Key) ? ".gui" : ".txt";
        var names = resolved.NoPatchFiles.Keys
            .Where(p => p.StartsWith(scope + "/", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetFileName(p).ToLowerInvariant())
            .ToList();
        var prefix = firstWins ? "00_" : "zz_";
        for (var i = 0; i < 32; i++)
        {
            var candidate = prefix + OwnFileName + extension;
            var beaten = firstWins
                ? names.All(n => string.CompareOrdinal(candidate, n) < 0)
                : names.All(n => string.CompareOrdinal(candidate, n) > 0);
            if (beaten)
            {
                break;
            }

            prefix = (firstWins ? "0" : "z") + prefix;
        }

        return $"{scope}/{prefix}{OwnFileName}{extension}";
    }

    private static PatchEntryOp EntryOp(AddressInfo info, DesiredState state, PatchForm form, PatchTarget target, ResolvedEntries resolved)
    {
        var key = info.Address.Key;
        var body = form switch
        {
            PatchForm.Inject => Render(new ScriptEntry[]
            {
                new ScriptStatement { Key = "INJECT:" + key, Value = new ScriptBlock { Entries = InjectExtras(info.NoPatch.Entries, state.Entries)!.ToList() } },
            }),
            PatchForm.Replace when info.Behavior == OverrideBehavior.LocalizationKeys => LocLine(state.Entries) + "\n",
            PatchForm.Replace => Render(state.Entries.Select(e => e is ScriptStatement s
                ? new ScriptStatement { Key = "REPLACE:" + key, Operator = s.Operator, Value = s.Value }
                : e).ToList()),
            _ => RenderPlain(info, state.Entries),
        };
        var notes = state.OpenConflicts.Select(OpenNote).ToList();
        var text = string.Concat(notes.Select(n => "# PMT offen: " + n + "\n")) + body;
        var inlineBody = form switch
        {
            PatchForm.PlainKey => Render(WithoutLeadingComments(state.Entries)),
            PatchForm.Replace when info.Behavior != OverrideBehavior.LocalizationKeys => Render(WithoutLeadingComments(state.Entries.Select(e => e is ScriptStatement s
                ? new ScriptStatement { Key = "REPLACE:" + key, Operator = s.Operator, Value = s.Value }
                : e).ToList())),
            _ => body,
        };
        var inline = string.Concat(notes.Select(n => "# PMT offen: " + n + "\n")) + inlineBody;
        var dependsOnOrder = form == PatchForm.PlainKey || (form == PatchForm.Replace && target != PatchTarget.Compatch);
        return new PatchEntryOp(info.Address, form, OwnFile(info, resolved), text, notes, dependsOnOrder, info.Uncertain, inline);
    }

    /// <summary>
    /// Der erste Eintrag ohne <see cref="ScriptEntry.LeadingComments"/>: Beim Ersetzen an einer Textstelle bleiben die Kommentare davor im
    /// Text stehen und würden sonst doppelt geschrieben (Befund M3 aus Plan 1). Quelltext-Angaben bleiben, damit die Formatierung erhalten bleibt.
    /// </summary>
    internal static IReadOnlyList<ScriptEntry> WithoutLeadingComments(IReadOnlyList<ScriptEntry> entries)
    {
        if (entries.Count == 0 || entries[0].LeadingComments.Count == 0)
        {
            return entries;
        }

        ScriptEntry first = entries[0] switch
        {
            ScriptStatement s => new ScriptStatement { Key = s.Key, Operator = s.Operator, Value = s.Value, TrailingComment = s.TrailingComment, SourceText = s.SourceText, SourceStart = s.SourceStart, SourceEnd = s.SourceEnd },
            ScriptListItem l => new ScriptListItem { Value = l.Value, TrailingComment = l.TrailingComment, SourceText = l.SourceText, SourceStart = l.SourceStart, SourceEnd = l.SourceEnd },
            var other => other,
        };
        return entries.Skip(1).Prepend(first).ToList();
    }

    private static PatchFileOp FileOp(string path, IReadOnlyList<EntryAddress> addresses, ResolvedEntries resolved, ApplyResult applied, CompatchInput input)
    {
        var rule = input.Rules.Match(path);
        if (!resolved.NoPatchFiles.TryGetValue(path, out var owner))
        {
            var fresh = string.Concat(addresses.Select(a => Render(applied.Desired[a].Entries)));
            return new PatchFileOp(path, fresh, null, false, addresses, true, true);
        }

        var bom = HasBom(owner.FullPath);
        var parts = KeyPartReader.Read(owner.Side, path, owner.FullPath, rule, input.KeyOperators, input.Cache);
        var text = parts.Text.Length > 0 || parts.Error is not null ? parts.Text : File.ReadAllText(owner.FullPath);
        var eol = text.Contains("\r\n") ? "\r\n" : "\n";

        if (rule.Behavior == OverrideBehavior.LocalizationKeys)
        {
            return new PatchFileOp(path, LocSubstitute(text, addresses, applied, eol), null, bom, addresses, false, true, parts.EncodingName);
        }

        var spans = new List<(int Start, int End, string Text)>();
        var normalized = parts.Error is not null || addresses.Any(a => a.Kind == AddressKind.FileRest);
        foreach (var address in addresses.Where(a => a.Kind == AddressKind.Key))
        {
            if (normalized)
            {
                break;
            }

            var desired = applied.Desired[address].Entries;
            var existing = parts.Keys.Where(k => k.Address == address).ToList();
            var replacement = desired.Count == 0 ? string.Empty : Eol(Render(WithPrefixOf(existing.FirstOrDefault(), existing.Count == 0 ? desired : WithoutLeadingComments(desired))).TrimEnd('\n'), eol);
            if (existing.Count == 0)
            {
                spans.Add(InsertNew(address, replacement, parts, text, eol));
                continue;
            }

            for (var i = 0; i < existing.Count; i++)
            {
                if (Span(existing[i]) is not { } span)
                {
                    normalized = true;
                    break;
                }

                spans.Add((span.Start, span.End, i == 0 ? replacement : string.Empty));
            }
        }

        if (normalized)
        {
            return new PatchFileOp(path, Eol(NormalizedFile(parts, addresses, applied), eol), null, bom, addresses, true, true, parts.EncodingName);
        }

        foreach (var (start, end, value) in spans.OrderByDescending(s => s.Start))
        {
            text = text[..start] + value + text[end..];
        }

        return new PatchFileOp(path, text, null, bom, addresses, false, true, parts.EncodingName);
    }

    /// <summary>
    /// Ein Schlüssel, den die Gewinnerdatei noch nicht hat: GUI-Typen kommen in den vorhandenen <c>types</c>-Block, Defines in ihren
    /// Elternblock (Prüfbefund I4); gibt es den Block nicht, wird er mit angelegt. Alles andere wird angehängt.
    /// </summary>
    private static (int Start, int End, string Text) InsertNew(EntryAddress address, string replacement, FileParts parts, string text, string eol)
    {
        var isType = address.Key.StartsWith("type ", StringComparison.Ordinal);
        var parent = parts.Rule.Behavior == OverrideBehavior.PartialKeys && address.Key.Contains('.') ? address.Key[..address.Key.IndexOf('.')] : null;
        if (isType || parent is not null)
        {
            var sibling = parts.Keys.FirstOrDefault(k => k.ContainerEnd is { } end && end < text.Length && text[end] == '}'
                && (isType ? k.Address.Key.StartsWith("type ", StringComparison.Ordinal) : k.Address.Key.StartsWith(parent + ".", StringComparison.Ordinal)));
            if (sibling?.ContainerEnd is { } at)
            {
                return (at, at, eol + replacement + eol);
            }

            replacement = (isType ? "types " + OwnFileName : parent + " =") + " {" + eol + replacement + eol + "}";
        }

        return (text.Length, text.Length, (text.EndsWith('\n') ? string.Empty : eol) + replacement + eol);
    }

    /// <summary>
    /// Dateirest (gewünscht oder unverändert) zuerst, damit Konstanten vor ihrer Verwendung stehen; dann alle Schlüssel. Defines kommen
    /// in ihre Elternblöcke, GUI-Typen in einen <c>types</c>-Block (Prüfbefund I4).
    /// </summary>
    private static string NormalizedFile(FileParts parts, IReadOnlyList<EntryAddress> addresses, ApplyResult applied)
    {
        var restAddress = addresses.FirstOrDefault(a => a.Kind == AddressKind.FileRest);
        var entries = new List<ScriptEntry>(restAddress is not null ? applied.Desired[restAddress].Entries : parts.Rest);
        var partialKeys = parts.Rule.Behavior == OverrideBehavior.PartialKeys;
        var parents = new Dictionary<string, List<ScriptEntry>>(StringComparer.Ordinal);
        var guiTypes = new List<ScriptEntry>();
        void Place(EntryAddress address, IReadOnlyList<ScriptEntry> chosen)
        {
            if (address.Key.StartsWith("type ", StringComparison.Ordinal))
            {
                guiTypes.AddRange(chosen);
            }
            else if (partialKeys && address.Key.Contains('.'))
            {
                var parent = address.Key[..address.Key.IndexOf('.')];
                if (!parents.TryGetValue(parent, out var children))
                {
                    children = new List<ScriptEntry>();
                    parents[parent] = children;
                    entries.Add(new ScriptStatement { Key = parent, Value = new ScriptBlock { Entries = children } });
                }

                children.AddRange(chosen);
            }
            else
            {
                entries.AddRange(chosen);
            }
        }

        var done = new HashSet<EntryAddress>();
        foreach (var part in parts.Keys)
        {
            if (done.Add(part.Address))
            {
                Place(part.Address, addresses.Contains(part.Address) ? applied.Desired[part.Address].Entries : part.Entries);
            }
        }

        foreach (var address in addresses.Where(a => a.Kind == AddressKind.Key && !done.Contains(a)))
        {
            Place(address, applied.Desired[address].Entries);
        }

        var text = Render(entries);
        return guiTypes.Count == 0 ? text : text + "types " + OwnFileName + " {\n" + Render(guiTypes) + "}\n";
    }

    private static IReadOnlyList<ScriptEntry> WithPrefixOf(KeyPart? existing, IReadOnlyList<ScriptEntry> desired)
    {
        if (existing is null || existing.Operator == OverrideOperator.None)
        {
            return desired;
        }

        var op = new OverrideKey(existing.Operator, string.Empty);
        var prefix = op.IsInject ? "REPLACE:" : PrefixText(existing.Operator);
        return desired.Select(e => e is ScriptStatement s ? new ScriptStatement { Key = prefix + s.Key, Operator = s.Operator, Value = s.Value } : e).ToList();
    }

    private static string PrefixText(OverrideOperator op) => op switch
    {
        OverrideOperator.InjectOrCreate => "INJECT_OR_CREATE:",
        OverrideOperator.ReplaceOrCreate => "REPLACE_OR_CREATE:",
        OverrideOperator.TryInject => "TRY_INJECT:",
        OverrideOperator.TryReplace => "TRY_REPLACE:",
        OverrideOperator.Inject => "INJECT:",
        OverrideOperator.Replace => "REPLACE:",
        _ => string.Empty,
    };

    private static (int Start, int End)? Span(KeyPart part)
    {
        var starts = part.Original.Select(e => e.SourceStart).ToList();
        var ends = part.Original.Select(e => e.SourceEnd).ToList();
        if (starts.Any(s => s is null) || ends.Any(e => e is null))
        {
            return null;
        }

        return (starts.Min()!.Value, ends.Max()!.Value);
    }

    private static string RenderPlain(AddressInfo info, IReadOnlyList<ScriptEntry> entries)
    {
        var key = info.Address.Key;
        if (key.StartsWith("type ", StringComparison.Ordinal))
        {
            return "types " + OwnFileName + " {\n" + Render(entries) + "}\n";
        }

        if (info.Behavior == OverrideBehavior.PartialKeys && key.Contains('.'))
        {
            return key[..key.IndexOf('.')] + " = {\n" + Render(entries) + "}\n";
        }

        return Render(entries);
    }

    private static string Render(IReadOnlyList<ScriptEntry> entries) =>
        ScriptWriter.Write(new ScriptBlock { Entries = entries.ToList() }, preferSourceVerbatim: true).Replace("\r\n", "\n").TrimEnd('\n') + "\n";

    private static string Eol(string text, string eol) => text.Replace("\r\n", "\n").Replace("\n", eol);

    private static string OpenNote(MergeAction action) =>
        $"{action.DisplayPath}: A = {Short(action.A)}, B = {Short(action.B)}";

    private static string Short(IReadOnlyList<ScriptEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "(entfernt)";
        }

        var text = string.Join(" ", entries.Select(SiblingMatcher.Canonical));
        return text.Length <= 80 ? text : text[..77] + "...";
    }

    private static string LocLine(IReadOnlyList<ScriptEntry> entries) =>
        " " + (entries.FirstOrDefault() is ScriptStatement { Value: ScriptScalar line } ? line.Text : string.Empty);

    /// <summary>Lokalisierung als ganze Datei: Zeile des Schlüssels ersetzen, entfernen oder anhängen.</summary>
    private static string LocSubstitute(string text, IReadOnlyList<EntryAddress> addresses, ApplyResult applied, string eol)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        foreach (var address in addresses.Where(a => a.Kind == AddressKind.Key))
        {
            var key = address.Key[(address.Key.IndexOf('/') + 1)..];
            var pattern = new Regex("^\\s*" + Regex.Escape(key) + ":");
            var desired = applied.Desired[address].Entries;
            var index = lines.FindIndex(l => pattern.IsMatch(l));
            if (desired.Count == 0)
            {
                if (index >= 0)
                {
                    lines.RemoveAt(index);
                }

                continue;
            }

            var line = LocLine(desired);
            if (index >= 0)
            {
                lines[index] = line;
            }
            else
            {
                var at = lines.Count > 0 && lines[^1].Length == 0 ? lines.Count - 1 : lines.Count;
                lines.Insert(at, line);
            }
        }

        return string.Join(eol, lines);
    }

    private static bool IsGuiKey(string key) =>
        key.StartsWith("type ", StringComparison.Ordinal) || key.StartsWith("template ", StringComparison.Ordinal);

    private static bool HasBom(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        Span<byte> head = stackalloc byte[3];
        return stream.Read(head) == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
    }
}
