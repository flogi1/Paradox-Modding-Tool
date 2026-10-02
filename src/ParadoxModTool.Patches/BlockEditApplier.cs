using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>
/// Block bearbeiten: wendet die Bearbeitungen (<see cref="BlockEdit"/>, z. B. "Einfügen in") direkt auf die bestehenden Blöcke der Dateien an, ohne Block-Kopie,
/// ohne Bereich und ohne eigene Datei. Der Block steht in einer Mod-Datei: er wird dort bearbeitet. Steht er nur in Vanilla, legt die Option "Kopie aus Vanilla"
/// zuerst die Mod-Kopie dieser Datei an (wie bei Zeilen-Regeln). Ein Lauf ändert nur, was noch fehlt: wiederholt ändert er nichts, und ein teilweise gefüllter Block
/// bekommt nur die fehlenden Anweisungen. Zeilenenden und BOM der Datei bleiben.
/// </summary>
public static class BlockEditApplier
{
    public static void Apply(BlockEditRule rule, RuleRunContext ctx)
    {
        var edits = rule.Edits.Where(e => e.Enabled).ToList();
        var workspace = ctx.Workspace;
        var modDocs = new Dictionary<string, (string Text, ScriptBlock Root, List<(int Start, int End)> Regions)?>(StringComparer.OrdinalIgnoreCase);
        var vanillaDocs = new Dictionary<string, (string Text, ScriptBlock Root)?>(StringComparer.OrdinalIgnoreCase);

        (string Text, ScriptBlock Root, List<(int Start, int End)> Regions)? ModDoc(string rel)
        {
            if (!modDocs.TryGetValue(rel, out var doc))
            {
                doc = null;
                if (workspace.Get(rel) is { } raw)
                {
                    var text = raw.Replace("\r\n", "\n");
                    try
                    {
                        doc = (text, ScriptDocument.Parse(text), LineRuleApplier.OwnRegions(text, ctx.Recipe.Id));
                    }
                    catch (ScriptParseException ex)
                    {
                        ctx.Warnings.Add(PatchTexts.FileSkipped(rel, ex.Message));
                    }
                }

                modDocs[rel] = doc;
            }

            return doc;
        }

        (string Text, ScriptBlock Root)? VanillaDoc(string rel)
        {
            if (!vanillaDocs.TryGetValue(rel, out var doc))
            {
                doc = null;
                if (workspace.ReadVanilla(rel) is { } raw)
                {
                    var text = raw.Replace("\r\n", "\n");
                    try
                    {
                        doc = (text, ScriptDocument.Parse(text));
                    }
                    catch (ScriptParseException ex)
                    {
                        ctx.Warnings.Add(PatchTexts.FileSkipped(rel, ex.Message));
                    }
                }

                vanillaDocs[rel] = doc;
            }

            return doc;
        }

        ScriptStatement? FindInMod(string key, out string file)
        {
            foreach (var rel in workspace.ModFiles(rule.Files))
            {
                if (ModDoc(rel) is { } doc && FindOutsideRegions(doc.Root, doc.Regions, key) is { } statement)
                {
                    file = rel;
                    return statement;
                }
            }

            file = string.Empty;
            return null;
        }

        // Schlüssel (samt Folgestufen) → Werte der Zeile für Platzhalter in den Bearbeitungen; Vanilla-Dateien, die eine Mod-Kopie brauchen.
        var targets = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        var copies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ctx.RowsFor(rule))
        {
            if (ctx.SkipForEmptyPlaceholders(rule, row, rule.BlockKey))
            {
                continue;
            }

            var key = TemplateRenderer.Render(rule.BlockKey, row.Values).Trim();
            var queue = new Queue<string>(new[] { key });
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var found = false;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                ScriptStatement? statement = FindInMod(current, out _);
                if (statement is null && rule.CreateCopy)
                {
                    foreach (var rel in workspace.VanillaFiles(rule.Files).Where(rel => workspace.Get(rel) is null))
                    {
                        if (VanillaDoc(rel) is { } doc && ScriptSpans.FindTopLevel(doc.Root, current) is { } fromVanilla)
                        {
                            statement = fromVanilla;
                            copies.Add(rel);
                            break;
                        }
                    }
                }

                if (statement is null)
                {
                    continue;
                }

                found |= current == key;
                targets[current] = row.Values;
                if (rule.FollowNext && statement.Value is ScriptBlock body)
                {
                    foreach (var next in body.Entries.OfType<ScriptStatement>().Where(s => s.Key == "next_building" && s.Value is ScriptScalar))
                    {
                        queue.Enqueue(((ScriptScalar)next.Value).Text);
                    }
                }
            }

            if (!found)
            {
                ctx.Skipped.Add(new SkippedRow(rule.Id, row.Key, PatchTexts.BlockNotFound(key, rule.Files)));
            }
        }

        foreach (var rel in copies)
        {
            if (workspace.CreateFromVanilla(rel))
            {
                workspace.AddNote(rel, PatchTexts.CopyCreated);
                modDocs.Remove(rel);
            }
        }

        if (targets.Count == 0 || edits.Count == 0)
        {
            return;
        }

        foreach (var rel in workspace.ModFiles(rule.Files))
        {
            if (ModDoc(rel) is not { } doc)
            {
                continue;
            }

            var spans = new List<(int Start, int End, string Text)>();
            foreach (var statement in doc.Root.Entries.OfType<ScriptStatement>())
            {
                if (statement.Value is not ScriptBlock
                    || !targets.TryGetValue(statement.Key, out var values)
                    || statement.SourceStart is not { } start
                    || statement.SourceEnd is not { } end
                    || doc.Regions.Any(r => start >= r.Start && start < r.End))
                {
                    continue;
                }

                var original = doc.Text[start..end];
                var edited = original;
                foreach (var edit in edits)
                {
                    edited = BlockCopyApplier.ApplyEdit(edited, edit, values, skipExisting: true);
                }

                if (edited != original)
                {
                    spans.Add((start, end, edited));
                }
            }

            if (spans.Count == 0)
            {
                continue;
            }

            var result = doc.Text;
            foreach (var (start, end, text) in spans.OrderByDescending(s => s.Start))
            {
                result = result.Remove(start, end - start).Insert(start, text);
            }

            workspace.Set(rel, workspace.NewLineOf(rel) == "\r\n" ? result.Replace("\n", "\r\n") : result);
            workspace.AddNote(rel, PatchTexts.BlocksEdited(spans.Count));
        }
    }

    private static ScriptStatement? FindOutsideRegions(ScriptBlock root, List<(int Start, int End)> regions, string key) =>
        root.Entries.OfType<ScriptStatement>().FirstOrDefault(s =>
            s.Key == key && s.Value is ScriptBlock && s.SourceStart is { } at && !regions.Any(r => at >= r.Start && at < r.End));
}
