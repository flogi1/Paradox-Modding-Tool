using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>Ein Special Building aus Vanilla, das eine <c>next_building</c>-Kette beginnt; <see cref="SpecialInMod"/>: die Mod definiert es noch als Special.</summary>
public sealed record VanillaSpecial(string Key, bool SpecialInMod);

/// <summary>
/// Vorlage "CK III: Special Buildings in normale Gebäude" (Spec 2026-09-30-patch-recipes, Etappe 4): Spalten, fünf Regeln in fester Reihenfolge
/// und die Suchmuster des Wächters; dazu die Liste der Vanilla-Specials für "Specials aus Vanilla einlesen".
/// </summary>
public static class SpecialBuildingsPreset
{
    public const string Id = "ck3-special-buildings";

    /// <summary>Ordner der Gebäude-Definitionen; "Bestehende Mod-Gebäude übernehmen" sucht und prüft Schlüssel hier.</summary>
    public const string BuildingsFolder = "common/buildings";

    private const string Buildings = BuildingsFolder + "/*.txt";

    /// <summary>Alle vier Formen, mit denen die History ein Special setzt; als Regex für das Auskommentieren.</summary>
    private const string SlotRegex = @"(?<![A-Za-z0-9_.])(add_)?special_building(_slot)?\s*=\s*{key}(?![A-Za-z0-9_])";

    private const string IteratorRegex = @"(?<![A-Za-z0-9_.])(every|any|random|ordered)_special_building_province(?![A-Za-z0-9_])";

    /// <summary>Alle Baronien als OR (bei einer auch), nichts ohne Baronie.</summary>
    private const string BaronyTrigger = "{?barony}OR = {\n{*barony}\tbarony = title:{barony}\n{/barony}}{/barony}";

    /// <summary>Datei der erzeugten Gebäude (Ziel der Block-Kopie); die Trigger-Regeln wirken nur auf sie.</summary>
    private const string Target = BuildingsFolder + "/pmt_special_buildings.txt";

    /// <summary>Die vier Ressourcen-Effekte (Holz, Stein, Metall, Luxus) mit dem Präfix <c>subtract</c> bzw. <c>add</c>, je Zeile einer.</summary>
    internal static string ResourceEffects(string verb) =>
        string.Join('\n', new[] { "wood", "stone", "metal", "luxury" }.Select(resource => $"flogis_techmod_{verb}_{resource}_250 = yes"));

    public static PatchRecipe Create(string name, IEnumerable<string> existingIds)
    {
        var recipe = RecipeEditing.Create(name, "ck3", existingIds);
        recipe.Preset = Id;
        recipe.WholeRows = true;
        recipe.Columns.Add(new PatchColumn { Name = "holdings", Type = ColumnType.List, Default = "castle_holding, city_holding, church_holding" });
        recipe.Columns.Add(new PatchColumn
        {
            Name = "provinz",
            Lookup = new DerivedLookup
            {
                Files = "history/provinces/*.txt",
                Source = LookupSource.Vanilla,
                Match = "special_building_slot = {key}; special_building = {key}; add_special_building_slot = {key}; add_special_building = {key}",
                Take = LookupTake.TopLevelKey,
                // Pflicht: ohne Ort in der History vergibt ein Script (Event, Entscheidung, Großprojekt) den Slot und baut das Gebäude gleich mit.
                // Umgewandelt wäre es überall baubar und add_special_building schlüge fehl, also bleibt die Zeile rot und das Gebäude Special.
                Optional = false,
                All = true,
            },
        });
        recipe.Columns.Add(new PatchColumn
        {
            Name = "barony",
            TitleKey = true,
            Lookup = new DerivedLookup
            {
                Files = "common/landed_titles/**/*.txt",
                Source = LookupSource.Vanilla,
                Match = "province = {provinz}",
                Take = LookupTake.EnclosingKey,
                Optional = true,
                All = true,
            },
        });

        recipe.Rules.Add(new LineRule
        {
            Id = "slots", Name = "Slots in der History", Files = "history/provinces/*.txt", Match = SlotRegex, MatchKind = MatchKind.Regex,
            Action = LineAction.Comment, CreateCopy = true,
        });
        recipe.Rules.Add(new LineRule
        {
            Id = "iterators", Name = "Special-Iteratoren", Scope = RuleScope.Global, Files = string.Empty, Match = IteratorRegex, MatchKind = MatchKind.Regex,
            Action = LineAction.Replace, Replacement = "$1_province", CreateCopy = true,
        });
        recipe.Rules.Add(new LineRule
        {
            Id = "originals", Name = "Original-Definitionen entfernen", Files = Buildings, Match = "{key}", Action = LineAction.RemoveBlock,
            FollowNext = true, CreateCopy = true, VanillaPathsOnly = true,
        });
        recipe.Rules.Add(new BlockCopyRule
        {
            Id = "buildings", Name = "Gebäude", SourceFiles = Buildings, BlockKey = "{key}", FollowNext = true,
            Target = Target,
            AdoptExisting = true,
            Edits =
            {
                new BlockEdit { Kind = BlockEditKind.Remove, Key = "type", Value = "special" },
                new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "can_construct_potential", Text = BaronyTrigger },

                // Ressourcen: jedes umgewandelte Gebäude (jede Stufe) zieht sie beim Baubeginn ab und gibt sie beim Abbruch zurück. Gibt es on_start oder
                // on_cancelled schon, kommen die Zeilen hinein (bei übernommenen Blöcken nur die fehlenden); sonst wird der Block angelegt.
                new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "on_start", Text = ResourceEffects("subtract") },
                new BlockEdit { Kind = BlockEditKind.InsertInto, Key = "on_cancelled", Text = ResourceEffects("add") },
            },
        });
        recipe.Rules.Add(new ListEntryRule
        {
            Id = "holdings", Name = "Holdings", Target = "common/holdings/00_holdings.txt", ListPath = "{holdings}/buildings", Entry = "{key}", CreateCopy = true,
        });

        // Hauptgebäude statt Burg/Stadt/Kirche: Die erzeugten Gebäude prüfen die Stufe des Hauptgebäudes (Stufe 01 → 2, 02 → 4, 03 → 6, 04 → 8). Einzeilform und
        // Mehrzeilform trifft eine Regel (Text ist leerraum-tolerant). Mod-Entscheidung von Flogi's Buildings and Technology Mod; wer sie nicht braucht, schaltet die vier Regeln aus.
        for (var level = 1; level <= 4; level++)
        {
            recipe.Rules.Add(new LineRule
            {
                Id = $"trigger_level_{level:00}", Name = $"Hauptgebäude-Trigger {level:00} → {level * 2}", Scope = RuleScope.Global, Files = Target,
                Match = $"building_requirement_castle_city_church = {{ LEVEL = {level:00} }}", MatchKind = MatchKind.Text,
                Action = LineAction.Replace, Replacement = $"building_requirement_main_building = {{ LEVEL = {level * 2} }}",
            });
        }

        recipe.Watch.Patterns = new List<string>
        {
            "every_special_building_province", "any_special_building_province", "random_special_building_province", "ordered_special_building_province",
            "has_special_building", "special_building_slot", "add_special_building", "add_special_building_slot",
        };
        return recipe;
    }

    /// <summary>
    /// Vanilla-Specials (<c>type = special</c> in <c>common/buildings</c>), die keine Folgestufe eines anderen Specials sind, ordinal sortiert;
    /// dazu, ob die Mod denselben Schlüssel noch als Special definiert.
    /// </summary>
    public static IReadOnlyList<VanillaSpecial> VanillaSpecials(PatchWorkspace workspace)
    {
        var specials = new HashSet<string>(StringComparer.Ordinal);
        var nexts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rel in workspace.VanillaFiles(Buildings))
        {
            foreach (var (key, block) in SpecialBlocks(workspace.ReadVanilla(rel)))
            {
                specials.Add(key);
                foreach (var next in block.Entries.OfType<ScriptStatement>().Where(s => s.Key == "next_building" && s.Value is ScriptScalar))
                {
                    nexts.Add(((ScriptScalar)next.Value).Text);
                }
            }
        }

        var inMod = workspace.ModFiles(Buildings).SelectMany(rel => SpecialBlocks(workspace.Get(rel))).Select(b => b.Key).ToHashSet(StringComparer.Ordinal);
        return specials.Where(k => !nexts.Contains(k)).Order(StringComparer.Ordinal).Select(k => new VanillaSpecial(k, inMod.Contains(k))).ToList();
    }

    /// <summary>
    /// Umbenannte Schlüssel, die Zeilen werden (Etappe 5): Vanilla-Special am Kettenanfang, noch keine Zeile, Zeile ohne Fehler (Ort in der History).
    /// <c>holdings</c> kommt aus den heutigen Listen der Mod, damit die Regel nichts einträgt, was die Mod absichtlich weglässt; ohne Listung gilt der Standard.
    /// </summary>
    public static IReadOnlyList<PatchRow> RowsToAdopt(PatchRecipe recipe, PatchWorkspace workspace, IEnumerable<string> renamedKeys)
    {
        var starts = VanillaSpecials(workspace).Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        var existing = recipe.Rows.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        var holdings = HoldingsByBuilding(workspace);
        var candidates = renamedKeys.Distinct(StringComparer.Ordinal).Where(k => starts.Contains(k) && !existing.Contains(k))
            .Select(k =>
            {
                var row = new PatchRow { Key = k };
                if (holdings.TryGetValue(k, out var list))
                {
                    row.Values["holdings"] = string.Join(", ", list);
                }

                return row;
            })
            .ToList();
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var copy = PatchRecipeStore.Clone(recipe);
        copy.Rows = candidates;
        var table = ColumnResolver.Resolve(copy, workspace);
        return candidates.Where((_, index) => !table.Rows[index].HasErrors).ToList();
    }

    /// <summary>Gebäude → Holding-Typen (Blöcke der obersten Ebene in <c>common/holdings/*.txt</c> der Mod, deren <c>buildings</c>-Liste es nennt), in Dateireihenfolge.</summary>
    private static Dictionary<string, List<string>> HoldingsByBuilding(PatchWorkspace workspace)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var rel in workspace.ModFiles("common/holdings/*.txt"))
        {
            ScriptBlock root;
            try
            {
                root = ScriptDocument.Parse(workspace.Get(rel) ?? string.Empty);
            }
            catch (ScriptParseException)
            {
                continue;
            }

            foreach (var holding in root.Entries.OfType<ScriptStatement>().Where(s => s.Value is ScriptBlock))
            {
                var lists = ((ScriptBlock)holding.Value).Entries.OfType<ScriptStatement>().Where(s => s.Key == "buildings" && s.Value is ScriptBlock);
                foreach (var item in lists.SelectMany(l => ((ScriptBlock)l.Value).Entries.OfType<ScriptListItem>()).Select(i => i.Value).OfType<ScriptScalar>())
                {
                    if (!result.TryGetValue(item.Text, out var list))
                    {
                        list = new List<string>();
                        result[item.Text] = list;
                    }

                    if (!list.Contains(holding.Key))
                    {
                        list.Add(holding.Key);
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<(string Key, ScriptBlock Block)> SpecialBlocks(string? text)
    {
        if (text is null)
        {
            yield break;
        }

        ScriptBlock root;
        try
        {
            root = ScriptDocument.Parse(text);
        }
        catch (ScriptParseException)
        {
            yield break;
        }

        foreach (var statement in root.Entries.OfType<ScriptStatement>())
        {
            if (statement.Value is ScriptBlock block
                && block.Entries.OfType<ScriptStatement>().Any(s => s.Key == "type" && s.Value is ScriptScalar { Text: "special" }))
            {
                yield return (statement.Key, block);
            }
        }
    }
}
