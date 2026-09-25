namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// 3-Wege-Merge für die Merge-Funktion aus Abschnitt 7: "Mod-Änderungen werden in die
/// aktualisierte Vanilla-Datei integriert". Ausgangspunkt ist die alte Vanilla-Fassung
/// (Basis); dagegen werden sowohl die Mod-Datei als auch die neue Vanilla-Fassung
/// verglichen. Grundregel: Wo nur der Mod etwas geändert (hinzugefügt, verändert ODER
/// entfernt) hat, wird die Mod-Änderung übernommen; wo nur das Spiel-Update etwas geändert hat,
/// steht bereits die neue Vanilla-Fassung als Basis. Wo BEIDE dieselbe Stelle unterschiedlich
/// verändert haben, wird das als <see cref="MergeConflict"/> gemeldet und NICHT automatisch
/// aufgelöst - die neue Vanilla-Version bleibt an dieser Stelle konservativ stehen, der Nutzer
/// entscheidet. Der Vergleich erfolgt strukturell (siehe <see cref="ScriptStructuralComparer"/>)
/// auf dem AST, nicht auf Rohtext. Adressierbare Einheiten sind Anweisungen, Skalar-
/// Listenelemente und "Kopfzeile + Rumpf"-Konstrukte (siehe <see cref="EntryUnit"/>), sodass
/// auch GUI-Dateien mit "template name { ... }" korrekt zusammengeführt werden.
/// </summary>
public static class MergeEngine
{
    public static MergeResult Merge(ScriptBlock oldVanilla, ScriptBlock newVanilla, ScriptBlock mod)
    {
        var (mergedEntries, conflicts) = MergeBlockLevel(
            oldVanilla, newVanilla, mod, Array.Empty<string>(), Array.Empty<string>());

        return new MergeResult
        {
            MergedRoot = new ScriptBlock { Entries = mergedEntries },
            Conflicts = conflicts
        };
    }

    private static (List<ScriptEntry> Merged, List<MergeConflict> Conflicts) MergeBlockLevel(
        ScriptBlock oldBlock, ScriptBlock newBlock, ScriptBlock modBlock,
        IReadOnlyList<string> parentSegments, IReadOnlyList<string> parentDisplays)
    {
        var conflicts = new List<MergeConflict>();

        var oldUnits = EntryUnits.Build(oldBlock.Entries);
        var rawNewUnits = EntryUnits.Build(newBlock.Entries);
        var rawModUnits = EntryUnits.Build(modBlock.Entries);
        // Wiederholte Schlüssel nach Inhalt zuordnen statt nach Position (sonst verschiebt eine Einfügung alle späteren "#n").
        var repeated = SiblingMatcher.RepeatedBaseKeys(oldUnits, rawNewUnits, rawModUnits);
        var newUnits = SiblingMatcher.Align(oldUnits, rawNewUnits, repeated).Units;
        // Beide fügen an derselben Stelle Verschiedenes ein: als Konflikt behandeln (konservativ wie vorher), nicht beide übernehmen.
        var modUnits = SiblingMatcher.PairFreshByPosition(newUnits, SiblingMatcher.Align(oldUnits, rawModUnits, repeated).Units);

        var oldByKey = oldUnits.ToDictionary(u => u.Key, StringComparer.Ordinal);
        var newByKey = newUnits.ToDictionary(u => u.Key, StringComparer.Ordinal);
        var modByKey = modUnits.ToDictionary(u => u.Key, StringComparer.Ordinal);

        // Ausgangspunkt ist die neue Vanilla-Fassung in ihrer eigenen Reihenfolge/Form -
        // "Vanilla-Formatierung/-Version wird im Zweifel übernommen" (Abschnitt 7).
        var merged = new List<ScriptEntry>(newBlock.Entries);

        // Einheiten, die aktuell in "merged" stehen (Schlüssel -> Einheit mit dem tatsächlich enthaltenen Eintrag).
        var current = new Dictionary<string, EntryUnit>(newByKey, StringComparer.Ordinal);

        foreach (var key in UnionKeysInStableOrder(newUnits, oldUnits, modUnits))
        {
            oldByKey.TryGetValue(key, out var oldUnit);
            newByKey.TryGetValue(key, out var newUnit);
            modByKey.TryGetValue(key, out var modUnit);

            var display = (newUnit ?? oldUnit ?? modUnit)!.Display;
            var segments = parentSegments.Append(key).ToList();
            var displays = parentDisplays.Append(display).ToList();
            var path = string.Join('.', displays);

            if (modUnit is null)
            {
                if (oldUnit is null || newUnit is null)
                {
                    // Nie im Mod vorhanden bzw. Spiel-Update hat es ohnehin auch entfernt: nichts zu tun.
                    continue;
                }

                if (UnitEquals(oldUnit, newUnit))
                {
                    // Nur der Mod hat diese Stelle entfernt -> Entfernung übernehmen.
                    RemoveUnit(merged, current, newUnit);
                }
                else
                {
                    conflicts.Add(new MergeConflict
                    {
                        Path = path,
                        PathSegments = segments,
                        Kind = MergeConflictKind.RemoveForMod,
                        Description =
                            $"Der Mod hat \"{display}\" entfernt, das Spiel-Update hat diese Stelle aber verändert. " +
                            "Die Fassung des Spiel-Updates wurde übernommen; \"Mod übernehmen\" entfernt die Stelle.",
                        LeftValue = newUnit.Value,
                        RightValue = null
                    });
                }

                continue;
            }

            if (oldUnit is null)
            {
                if (newUnit is null)
                {
                    // Nur der Mod hat diese Stelle eingeführt -> übernehmen.
                    InsertUnit(merged, current, modUnits, modUnit);
                }
                else if (!UnitEquals(modUnit, newUnit))
                {
                    conflicts.Add(new MergeConflict
                    {
                        Path = path,
                        PathSegments = segments,
                        Description =
                            $"Sowohl der Mod als auch das Spiel-Update haben \"{display}\" neu eingeführt, mit unterschiedlichem Inhalt. " +
                            "Die Fassung des Spiel-Updates wurde übernommen; bitte prüfen, ob die Mod-Fassung stattdessen nötig ist.",
                        LeftValue = newUnit.Value,
                        RightValue = modUnit.Value,
                        ModEntry = modUnit.Entry,
                        ModHeader = modUnit.Header
                    });
                }

                continue;
            }

            if (UnitEquals(oldUnit, modUnit))
            {
                // Mod hat diese Stelle unverändert aus Vanilla übernommen -> es gilt das Spiel-Update.
                continue;
            }

            if (newUnit is null)
            {
                conflicts.Add(new MergeConflict
                {
                    Path = path,
                    PathSegments = segments,
                    Kind = MergeConflictKind.InsertForMod,
                    AnchorKey = AnchorKeyFor(modUnits, modUnit, current),
                    Description =
                        $"Das Spiel-Update hat \"{display}\" entfernt, der Mod hat diese Stelle aber verändert. " +
                        "Die Stelle fehlt in der zusammengeführten Datei; \"Mod übernehmen\" fügt die Mod-Fassung wieder ein.",
                    LeftValue = null,
                    RightValue = modUnit.Value,
                    ModEntry = modUnit.Entry,
                    ModHeader = modUnit.Header
                });
                continue;
            }

            if (UnitEquals(oldUnit, newUnit))
            {
                // Nur der Mod hat geändert, das Spiel-Update hat diese Stelle unangetastet gelassen
                // -> die Mod-Änderung sicher übernehmen.
                ReplaceEntry(merged, current, newUnit, modUnit.Entry);
                continue;
            }

            if (UnitEquals(modUnit, newUnit))
            {
                // Beide sind unabhängig voneinander auf denselben neuen Stand gekommen -> kein Konflikt.
                continue;
            }

            if (oldUnit.Value is ScriptBlock oldNested && newUnit.Value is ScriptBlock newNested && modUnit.Value is ScriptBlock modNested)
            {
                var (nestedMerged, nestedConflicts) = MergeBlockLevel(oldNested, newNested, modNested, segments, displays);
                conflicts.AddRange(nestedConflicts);
                ReplaceEntry(merged, current, newUnit, EntryUnits.WithBlockValue(newUnit.Entry, new ScriptBlock { Entries = nestedMerged }));
                continue;
            }

            // Echter Konflikt auf Blattwert-Ebene: bewusst konservativ die Spiel-Update-Fassung stehen lassen.
            conflicts.Add(new MergeConflict
            {
                Path = path,
                PathSegments = segments,
                Description =
                    $"\"{display}\" wurde sowohl vom Mod als auch vom Spiel-Update unterschiedlich verändert. " +
                    "Die Fassung des Spiel-Updates wurde übernommen; bitte die Mod-Änderung bei Bedarf über \"Mod übernehmen\" wählen.",
                LeftValue = newUnit.Value,
                RightValue = modUnit.Value,
                ModEntry = modUnit.Entry,
                ModHeader = modUnit.Header
            });
        }

        return (merged, TranslateSegments(conflicts, merged, current, parentSegments.Count));
    }

    /// <summary>
    /// Konflikte tragen auf dieser Ebene Zuordnungsschlüssel (siehe <see cref="SiblingMatcher"/>). <see cref="ConflictResolution"/> sucht im
    /// Ergebnis aber nach Positionsschlüsseln ("#n"). Übersetzt daher das Segment dieser Ebene (und den Anker) in den Positionsschlüssel des
    /// Eintrags im Ergebnis. Fehlt der Eintrag im Ergebnis, bekommt das Segment ein "!" vorne und passt auf keinen vorhandenen Eintrag.
    /// </summary>
    private static List<MergeConflict> TranslateSegments(List<MergeConflict> conflicts, List<ScriptEntry> merged, Dictionary<string, EntryUnit> current, int depth)
    {
        var positional = new Dictionary<ScriptEntry, string>(ReferenceEqualityComparer.Instance);
        foreach (var unit in EntryUnits.Build(merged))
        {
            positional[unit.Entry] = unit.Key;
        }

        string Translate(string key) =>
            current.TryGetValue(key, out var unit) && positional.TryGetValue(unit.Entry, out var position) ? position : "!" + key;

        return conflicts.Select(conflict =>
        {
            var segments = conflict.PathSegments.ToList();
            segments[depth] = Translate(segments[depth]);
            var anchor = conflict.AnchorKey is not null && conflict.PathSegments.Count - 1 == depth ? Translate(conflict.AnchorKey) : conflict.AnchorKey;
            return conflict.WithPath(segments, anchor);
        }).ToList();
    }

    private static bool UnitEquals(EntryUnit a, EntryUnit b) => ScriptStructuralComparer.ValueEquals(a.Value, b.Value);

    private static void RemoveUnit(List<ScriptEntry> merged, Dictionary<string, EntryUnit> current, EntryUnit unit)
    {
        merged.Remove(unit.Entry);
        foreach (var header in unit.Header)
        {
            merged.Remove(header);
        }

        current.Remove(unit.Key);
    }

    private static void ReplaceEntry(List<ScriptEntry> merged, Dictionary<string, EntryUnit> current, EntryUnit target, ScriptEntry replacement)
    {
        var index = merged.IndexOf(target.Entry);
        if (index < 0)
        {
            return;
        }

        merged[index] = replacement;
        current[target.Key] = new EntryUnit
        {
            Key = target.Key,
            Display = target.Display,
            BaseKey = target.BaseKey,
            BaseDisplay = target.BaseDisplay,
            Kind = target.Kind,
            Entry = replacement,
            Header = target.Header
        };
    }

    /// <summary>Fügt eine nur im Mod vorhandene Einheit hinter der in der Mod-Reihenfolge vorangehenden (bereits vorhandenen) Einheit ein.</summary>
    private static void InsertUnit(List<ScriptEntry> merged, Dictionary<string, EntryUnit> current, List<EntryUnit> modUnits, EntryUnit modUnit)
    {
        var anchorKey = AnchorKeyFor(modUnits, modUnit, current);
        var insertAt = 0;

        if (anchorKey is not null)
        {
            var anchorIndex = merged.IndexOf(current[anchorKey].Entry);
            if (anchorIndex >= 0)
            {
                insertAt = anchorIndex + 1;
            }
        }

        merged.InsertRange(insertAt, modUnit.Header.Append(modUnit.Entry).ToList());
        current[modUnit.Key] = modUnit;
    }

    private static string? AnchorKeyFor(List<EntryUnit> modUnits, EntryUnit modUnit, Dictionary<string, EntryUnit> current)
    {
        var position = modUnits.IndexOf(modUnit);
        for (var i = position - 1; i >= 0; i--)
        {
            if (current.ContainsKey(modUnits[i].Key))
            {
                return modUnits[i].Key;
            }
        }

        return null;
    }

    private static IEnumerable<string> UnionKeysInStableOrder(params List<EntryUnit>[] unitLists)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();

        foreach (var units in unitLists)
        {
            foreach (var unit in units)
            {
                if (seen.Add(unit.Key))
                {
                    ordered.Add(unit.Key);
                }
            }
        }

        return ordered;
    }
}
