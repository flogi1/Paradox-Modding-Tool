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
                    // UpdateMod: das Ergebnis gehört in die eigene Datei der Mod, nie in eine PMT-eigene Datei.
                    var form = target == PatchTarget.UpdateMod
                        ? PatchForm.WholeFile
                        : formChoices is not null && formChoices.TryGetValue(address, out var wanted) && forms.Contains(wanted) ? wanted : forms[0];
                    if (form == PatchForm.WholeFile)
                    {
                        Collect(WholeFilePath(info), address);
                        break;
                    }

                    plan.Entries.Add(EntryOp(info, state, form, target, resolved));
                    break;
            }
        }

        foreach (var (path, addresses) in wholeFiles.OrderBy(w => w.Key, StringComparer.OrdinalIgnoreCase))
        {
            plan.Files.Add(FileOp(path, addresses, resolved, applied, input, target));
        }

        return plan;
    }

    /// <summary>Die Datei, in die ein Schlüssel mit Form <see cref="PatchForm.WholeFile"/> geschrieben wird: die, die das Spiel ohne Patch lädt, sonst die erste vorhandene.</summary>
    public static string WholeFilePath(AddressInfo info) =>
        info.NoPatch.RelativePath ?? info.A.RelativePath ?? info.B.RelativePath ?? info.Vanilla.RelativePath ?? info.Address.Scope;

    public static IReadOnlyList<PatchForm> AvailableForms(AddressInfo info, IReadOnlyList<ScriptEntry> desired, CompatchInput input)
    {
        var forms = new List<PatchForm>();
        // Präfixe des Ordners (Victoria 3: nur Ordner der Wiki-Liste, manche nur REPLACE), ohne eigene die des Spiels.
        bool Has(string op) => (info.Operators ?? input.KeyOperators).Contains(op, StringComparer.OrdinalIgnoreCase);
        if (info.Address.Kind == AddressKind.Key && desired.Count > 0)
        {
            switch (info.Behavior)
            {
                case OverrideBehavior.PerKey:
                    // Ein REPLACE: läuft nach jedem INJECT: und würde die Ergänzung des Kompatchs wieder löschen (Prüfbefund C1).
                    if (Has("INJECT") && !info.NoPatch.Operators.Contains(OverrideOperator.Replace) && InjectEntries(info.NoPatch.Entries, desired, input.Rules.InjectAppends) is not null)
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

    /// <summary>Der Inhalt von <c>INJECT:</c> für den Block ohne Patch und den gewünschten Block, siehe <see cref="InjectShape.Of"/>; sonst <see langword="null"/>.</summary>
    /// <param name="injectAppends">INJECT hängt nur an (Victoria 3): dann nur reine Ergänzungen, kein geänderter Teilschlüssel.</param>
    internal static IReadOnlyList<ScriptEntry>? InjectEntries(IReadOnlyList<ScriptEntry> noPatch, IReadOnlyList<ScriptEntry> desired, bool injectAppends = false)
    {
        if (noPatch.Count != 1 || desired.Count != 1
            || noPatch[0] is not ScriptStatement { Value: ScriptBlock have } before
            || desired[0] is not ScriptStatement { Value: ScriptBlock want } after
            || before.Key != after.Key)
        {
            return null;
        }

        var shape = InjectShape.Of(have.Entries, want.Entries, StructuralEquality.EntryEqual);
        return injectAppends && shape is { Changed: > 0 } ? null : shape?.Entries;
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
                new ScriptStatement { Key = "INJECT:" + key, Value = new ScriptBlock { Entries = InjectEntries(info.NoPatch.Entries, state.Entries)!.ToList() } },
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

    private static PatchFileOp FileOp(string path, IReadOnlyList<EntryAddress> addresses, ResolvedEntries resolved, ApplyResult applied, CompatchInput input, PatchTarget target)
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
        // Neu geschriebene Stellen bekommen Einrückungsart und Tiefe der Datei (Nutzertest 2026-10-01: sonst Leerzeichen zwischen Tabs).
        var style = StyleOf(text);
        // UpdateMod: der Dateirest wird als Text geändert (RestSpans), damit Kommentare und Leerzeilen der Mod bleiben (Spec, Abschnitt 5).
        var updateMod = target == PatchTarget.UpdateMod;
        var normalized = parts.Error is not null || (!updateMod && addresses.Any(a => a.Kind == AddressKind.FileRest));
        // UpdateMod: neue Schlüssel kommen hinter ihren Vorgänger in der neuen Vanilla-Datei (A), damit der Vergleich mit Vanilla ruhig bleibt.
        var order = updateMod ? KeyOrderInA(path, addresses, resolved, rule, input) : null;
        foreach (var address in addresses.Where(a => a.Kind == AddressKind.Key).OrderBy(a => order is not null && order.TryGetValue(a, out var i) ? i : int.MaxValue))
        {
            if (normalized)
            {
                break;
            }

            var desired = applied.Desired[address].Entries;
            var existing = parts.Keys.Where(k => k.Address == address).ToList();
            if (existing.Count == 0)
            {
                if (desired.Count > 0 && Predecessor(address, order, parts) is { } before)
                {
                    spans.Add((before.End, before.End, eol + eol + Eol(RenderAt(desired, style, DepthAt(text, before.Start, style)), eol)));
                }
                else if (desired.Count > 0)
                {
                    spans.Add(InsertNew(address, desired, style, parts, text, eol));
                }

                continue;
            }

            for (var i = 0; i < existing.Count; i++)
            {
                if (Span(existing[i]) is not { } span)
                {
                    normalized = true;
                    break;
                }

                var replacement = i > 0 || desired.Count == 0
                    ? string.Empty
                    : Eol(RenderAt(WithPrefixOf(existing[0], WithoutLeadingComments(desired)), style, DepthAt(text, span.Start, style)).TrimStart(' ', '\t'), eol);
                spans.Add((span.Start, span.End, replacement));
            }
        }

        if (!normalized && updateMod && addresses.FirstOrDefault(a => a.Kind == AddressKind.FileRest) is { } restAddress)
        {
            if (RestSpans(parts, applied.Desired[restAddress].Entries, text, eol, style) is { } restSpans)
            {
                spans.AddRange(restSpans);
            }
            else
            {
                normalized = true;
            }
        }

        if (normalized)
        {
            return new PatchFileOp(path, Eol(NormalizedFile(parts, addresses, applied), eol), null, bom, addresses, true, true, parts.EncodingName);
        }

        // Bei gleichem Anfang zuerst ersetzen/entfernen, dann einfügen: sonst schnitte das Entfernen den eingefügten Text mit weg.
        // Mehrere Einfügungen an derselben Stelle in umgekehrter Reihenfolge, damit sie in ihrer Reihenfolge im Text stehen.
        var ordered = spans.Select((span, index) => (Span: span, Index: index))
            .OrderByDescending(s => s.Span.Start).ThenBy(s => s.Span.End > s.Span.Start ? 0 : 1).ThenByDescending(s => s.Index)
            .Select(s => s.Span);
        foreach (var (start, end, value) in ordered)
        {
            text = text[..start] + value + text[end..];
        }

        return new PatchFileOp(path, text, null, bom, addresses, false, true, parts.EncodingName);
    }

    /// <summary>
    /// Update-Modus: Dateirest als Text ändern, Einheit für Einheit (Kopfzeile plus Rumpf wie bei GUI-Fenstern). Gewünschte Einheiten werden
    /// den Einheiten der Mod zugeordnet wie im Merge (<see cref="SiblingMatcher"/>). Eine geänderte Einheit wird an ihrer Stelle ersetzt; die
    /// Kommentare davor bleiben die der Mod. Nicht mehr gewünschte Einheiten werden samt Zeilenende herausgeschnitten. Neue kommen hinter
    /// ihren Vorgänger, ohne Vorgänger vor den ersten obersten Schlüsseleintrag (Konstanten und <c>namespace</c> stehen vor ihrer Verwendung;
    /// nie in einen <c>types</c>-Block, Nutzertest 2026-10-01). Alles andere bleibt Zeichen für Zeichen. <see langword="null"/>, wenn eine
    /// betroffene Einheit keine Textstelle hat.
    /// </summary>
    private static List<(int Start, int End, string Text)>? RestSpans(FileParts parts, IReadOnlyList<ScriptEntry> desired, string text, string eol, ScriptWriteOptions style)
    {
        var modUnits = EntryUnits.Build(parts.Rest);
        var wanted = EntryUnits.Build(desired);
        var aligned = SiblingMatcher.Align(modUnits, wanted, SiblingMatcher.RepeatedBaseKeys(modUnits, wanted)).Units;
        var modByKey = modUnits.ToDictionary(u => u.Key, StringComparer.Ordinal);
        var paired = new HashSet<string>(StringComparer.Ordinal);
        var spans = new List<(int Start, int End, string Text)>();
        var inserts = new List<(int At, bool Behind, List<EntryUnit> Units)>();
        int? after = null;
        foreach (var unit in aligned)
        {
            if (modByKey.TryGetValue(unit.Key, out var mod) && paired.Add(unit.Key))
            {
                if (UnitSpan(mod) is not { } span)
                {
                    return null;
                }

                if (EntryUnits.HeaderText(mod.Header) != EntryUnits.HeaderText(unit.Header) || !StructuralEquality.EntryEqual(mod.Entry, unit.Entry))
                {
                    var rendered = RenderAt(WithoutLeadingComments(unit.Header.Append(unit.Entry).ToList()), style, DepthAt(text, span.Start, style));
                    spans.Add((span.Start, span.End, Eol(rendered.TrimStart(' ', '\t'), eol)));
                }

                after = span.End;
                continue;
            }

            var at = after ?? parts.FirstKeyStart ?? text.Length;
            if (inserts.Count > 0 && inserts[^1].At == at)
            {
                inserts[^1].Units.Add(unit);
            }
            else
            {
                inserts.Add((at, after is not null, new List<EntryUnit> { unit }));
            }
        }

        foreach (var mod in modUnits.Where(u => !paired.Contains(u.Key)))
        {
            if (UnitSpan(mod) is not { } span)
            {
                return null;
            }

            var end = span.End;
            if (end < text.Length && text[end] == '\r')
            {
                end++;
            }

            if (end < text.Length && text[end] == '\n')
            {
                end++;
            }

            spans.Add((span.Start, end, string.Empty));
        }

        foreach (var (at, behind, units) in inserts)
        {
            var rendered = Eol(RenderAt(units.SelectMany(u => u.Header.Append(u.Entry)).ToList(), style, 0), eol);
            spans.Add((at, at, behind
                ? eol + rendered
                : (at == text.Length && text.Length > 0 && !text.EndsWith('\n') ? eol : string.Empty) + rendered + eol));
        }

        return spans;
    }

    /// <summary>Reihenfolge der Schlüssel in der Datei der Seite A (Update-Modus: neue Vanilla-Fassung); <see langword="null"/> ohne diese Datei.</summary>
    private static Dictionary<EntryAddress, int>? KeyOrderInA(string path, IReadOnlyList<EntryAddress> addresses, ResolvedEntries resolved, RuleMatch rule, CompatchInput input)
    {
        var fullPath = addresses.Select(a => resolved.Addresses[a].A)
            .Where(a => a.FullPath is not null && string.Equals(PatchWriter.Norm(a.RelativePath ?? string.Empty), PatchWriter.Norm(path), StringComparison.OrdinalIgnoreCase))
            .Select(a => a.FullPath)
            .FirstOrDefault();
        if (fullPath is null)
        {
            return null;
        }

        var order = new Dictionary<EntryAddress, int>();
        foreach (var part in KeyPartReader.Read(MergeSide.A, path, fullPath, rule, input.KeyOperators, input.Cache).Keys)
        {
            order.TryAdd(part.Address, order.Count);
        }

        return order;
    }

    /// <summary>
    /// Textstelle des nächsten Schlüssels vor <paramref name="address"/> in <paramref name="order"/>, den die Datei schon hat, im selben
    /// umgebenden Block (GUI-Typ nur hinter GUI-Typ, Define nur im selben Elternblock); sonst <see langword="null"/>.
    /// </summary>
    private static (int Start, int End)? Predecessor(EntryAddress address, Dictionary<EntryAddress, int>? order, FileParts parts)
    {
        if (order is null || !order.TryGetValue(address, out var index))
        {
            return null;
        }

        var behavior = parts.Rule.Behavior;
        string? Container(string key) => key.StartsWith("type ", StringComparison.Ordinal) ? "types" : ParentOf(key, behavior);
        var container = Container(address.Key);
        var candidates = parts.Keys
            .Where(k => order.TryGetValue(k.Address, out var i) && i < index && Container(k.Address.Key) == container)
            .OrderByDescending(k => order[k.Address])
            .ToList();
        return candidates.Count == 0 ? null : Span(candidates.Last(k => k.Address == candidates[0].Address));
    }

    /// <summary>Textstelle einer Einheit von der ersten Kopfzeile bis zum Ende des Rumpfs (ohne die Kommentare davor).</summary>
    private static (int Start, int End)? UnitSpan(EntryUnit unit)
    {
        var entries = unit.Header.Append(unit.Entry).ToList();
        return entries.Any(e => e.SourceStart is null || e.SourceEnd is null)
            ? null
            : (entries.Min(e => e.SourceStart!.Value), entries.Max(e => e.SourceEnd!.Value));
    }

    /// <summary>
    /// Ein Schlüssel, den die Gewinnerdatei noch nicht hat: GUI-Typen kommen in den vorhandenen <c>types</c>-Block, Defines in ihren
    /// Elternblock (Prüfbefund I4); gibt es den Block nicht, wird er mit angelegt. Alles andere wird angehängt.
    /// </summary>
    private static (int Start, int End, string Text) InsertNew(EntryAddress address, IReadOnlyList<ScriptEntry> entries, ScriptWriteOptions style, FileParts parts, string text, string eol)
    {
        var isType = address.Key.StartsWith("type ", StringComparison.Ordinal);
        var parent = ParentOf(address.Key, parts.Rule.Behavior);
        if (isType || parent is not null)
        {
            var sibling = parts.Keys.FirstOrDefault(k => k.ContainerEnd is { } end && end < text.Length && text[end] == '}'
                && (isType ? k.Address.Key.StartsWith("type ", StringComparison.Ordinal) : ParentOf(k.Address.Key, parts.Rule.Behavior) == parent));
            if (sibling?.ContainerEnd is { } at)
            {
                return (at, at, eol + Eol(RenderAt(entries, style, DepthAt(text, at, style) + 1), eol) + eol);
            }

            var wrapped = isType
                ? "types " + OwnFileName + " {" + eol + Eol(RenderAt(entries, style, 1), eol) + eol + "}"
                : WrapIn(parent!, Eol(RenderAt(entries, style, 1), eol), eol);
            return (text.Length, text.Length, (text.EndsWith('\n') ? string.Empty : eol) + wrapped + eol);
        }

        return (text.Length, text.Length, (text.EndsWith('\n') ? string.Empty : eol) + Eol(RenderAt(entries, style, 0), eol) + eol);
    }

    /// <summary>
    /// Dateirest (gewünscht oder unverändert) zuerst, damit Konstanten vor ihrer Verwendung stehen; dann alle Schlüssel. Defines kommen
    /// in ihre Elternblöcke, GUI-Typen in einen <c>types</c>-Block (Prüfbefund I4).
    /// </summary>
    private static string NormalizedFile(FileParts parts, IReadOnlyList<EntryAddress> addresses, ApplyResult applied)
    {
        var restAddress = addresses.FirstOrDefault(a => a.Kind == AddressKind.FileRest);
        var entries = new List<ScriptEntry>(restAddress is not null ? applied.Desired[restAddress].Entries : parts.Rest);
        var parents = new Dictionary<string, List<ScriptEntry>>(StringComparer.Ordinal);
        var guiTypes = new List<ScriptEntry>();
        void Place(EntryAddress address, IReadOnlyList<ScriptEntry> chosen)
        {
            if (address.Key.StartsWith("type ", StringComparison.Ordinal))
            {
                guiTypes.AddRange(chosen);
            }
            else if (ParentOf(address.Key, parts.Rule.Behavior) is { } parent)
            {
                if (!parents.TryGetValue(parent, out var children))
                {
                    children = new List<ScriptEntry>();
                    parents[parent] = children;
                    entries.Add(NestedStatement(parent, children));
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

        if (ParentOf(key, info.Behavior) is { } parent)
        {
            return WrapIn(parent, Render(entries).TrimEnd('\n'), "\n") + "\n";
        }

        return Render(entries);
    }

    /// <summary>Der Block, in dem der Schlüssel steht: Elternblock bei Defines, Hülle bei <see cref="RuleKeys"/>; sonst <see langword="null"/>.</summary>
    private static string? ParentOf(string key, OverrideBehavior behavior) =>
        behavior == OverrideBehavior.PartialKeys
            ? key.Contains('.') ? key[..key.IndexOf('.')] : null
            : RuleKeys.WrapperOf(key);

    /// <summary>Text in seinen Hüllen: <c>a &gt; b</c> ergibt <c>a = { b = { … } }</c> (HOI IV: <c>ideas &gt; country</c>), eine Hülle wie bisher.</summary>
    private static string WrapIn(string parent, string inner, string eol)
    {
        var text = inner;
        foreach (var wrapper in parent.Split(RuleKeys.WrapSeparator).Reverse())
        {
            text = wrapper + " = {" + eol + text + eol + "}";
        }

        return text;
    }

    /// <summary>Block für die Einträge <paramref name="children"/> in ihren Hüllen (siehe <see cref="WrapIn"/>).</summary>
    private static ScriptStatement NestedStatement(string parent, List<ScriptEntry> children)
    {
        var wrappers = parent.Split(RuleKeys.WrapSeparator);
        var statement = new ScriptStatement { Key = wrappers[^1], Value = new ScriptBlock { Entries = children } };
        foreach (var wrapper in wrappers.Reverse().Skip(1))
        {
            statement = new ScriptStatement { Key = wrapper, Value = new ScriptBlock { Entries = new List<ScriptEntry> { statement } } };
        }

        return statement;
    }

    private static string Render(IReadOnlyList<ScriptEntry> entries) =>
        ScriptWriter.Write(new ScriptBlock { Entries = entries.ToList() }, preferSourceVerbatim: true).Replace("\r\n", "\n").TrimEnd('\n') + "\n";

    /// <summary>Wie <see cref="Render"/>, aber in Einrückungsart <paramref name="style"/> und Tiefe <paramref name="depth"/>; ohne Zeilenende am Schluss.</summary>
    private static string RenderAt(IReadOnlyList<ScriptEntry> entries, ScriptWriteOptions style, int depth) =>
        ScriptWriter.Write(new ScriptBlock { Entries = entries.ToList() }, preferSourceVerbatim: true, style, depth).Replace("\r\n", "\n").TrimEnd('\n');

    /// <summary>
    /// Einrückungsart einer Datei: Tabs, wenn mehr Zeilen mit einem Tab als mit Leerzeichen eingerückt sind. Wörtlicher Text wird umgerückt,
    /// damit auch Blocktexte aus dem Kompatch-Fenster (Leerzeichen, Tiefe 0) zur Stelle in der Datei passen.
    /// </summary>
    private static ScriptWriteOptions StyleOf(string text)
    {
        int tabs = 0, spaces = 0;
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] == '\n')
            {
                tabs += text[i + 1] == '\t' ? 1 : 0;
                spaces += text[i + 1] == ' ' ? 1 : 0;
            }
        }

        return new ScriptWriteOptions { UseTabs = tabs > spaces, ReindentVerbatim = true };
    }

    /// <summary>Tiefe an einer Textstelle aus der Einrückung ihrer Zeile; 0, wenn davor in der Zeile schon Text steht.</summary>
    private static int DepthAt(string text, int position, ScriptWriteOptions style)
    {
        var lineStart = position == 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;
        var lead = text[lineStart..position];
        return lead.Any(c => c is not (' ' or '\t')) ? 0 : lead.Count(c => c == '\t') + lead.Count(c => c == ' ') / style.IndentSize;
    }

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
