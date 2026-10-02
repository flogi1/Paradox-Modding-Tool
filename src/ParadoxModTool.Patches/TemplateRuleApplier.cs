using System.Text.RegularExpressions;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>Vorlage pro Zeile: ein Baustein je Zeile (und Listenwert) im erzeugten Bereich des Ziels, auch in <c>.yml</c>-Dateien.</summary>
public static class TemplateRuleApplier
{
    private static readonly Regex LanguageSuffix = new(@"_l_(?<lang>[a-z_]+)\.yml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static void Apply(TemplateRule rule, RuleRunContext ctx)
    {
        var isLocalization = rule.Target.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);
        var checkDuplicates = rule.Inside is null && rule.Target.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
        var units = new List<RegionUnit>();
        var definitions = checkDuplicates ? DuplicateChecker.BuildIndex(ctx.Workspace, rule.Target, ctx.Recipe.Id, rule.Id) : null;
        foreach (var row in ctx.RowsFor(rule))
        {
            var rowUnits = TemplateRenderer.RenderEach(rule.Text, row.Values, ctx.Table.ListColumns)
                .Select(i => new RegionUnit(i.Suffix.Length > 0 ? row.Key + " " + i.Suffix : row.Key, LineRuleApplier.ApplyReplaces(ctx, rule.Target, i.Text)))
                .ToList();
            if (definitions is not null && FirstDuplicate(rowUnits, definitions) is { } hit)
            {
                ctx.Skipped.Add(new SkippedRow(rule.Id, row.Key, PatchTexts.AlreadyDefined(hit.File, hit.Line)));
                continue;
            }

            units.AddRange(rowUnits);
        }

        UnitRegionWriter.WriteUnits(ctx, rule.Id, rule.Target, rule.Inside, units, isLocalization, NewFileHeader(rule.Target, isLocalization));
    }

    /// <summary>Kopfzeile einer neuen Lokalisierungsdatei (<c>l_english:</c> aus <c>…_l_english.yml</c>).</summary>
    private static string? NewFileHeader(string target, bool isLocalization)
    {
        if (!isLocalization)
        {
            return null;
        }

        var match = LanguageSuffix.Match(target);
        return match.Success ? "l_" + match.Groups["lang"].Value.ToLowerInvariant() + ":" : null;
    }

    private static (string File, int Line)? FirstDuplicate(IEnumerable<RegionUnit> units, IReadOnlyDictionary<string, (string File, int Line)> definitions)
    {
        foreach (var unit in units)
        {
            ScriptBlock root;
            try
            {
                root = ScriptDocument.Parse(unit.Text);
            }
            catch (ScriptParseException)
            {
                continue;
            }

            foreach (var statement in root.Entries.OfType<ScriptStatement>())
            {
                if (definitions.TryGetValue(statement.Key, out var hit))
                {
                    return hit;
                }
            }
        }

        return null;
    }
}
