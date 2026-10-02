using System.Globalization;

namespace ParadoxModTool.Desync;

/// <summary>Wählt aus den Hinweisregeln des Profils die zu einem Bericht passenden Texte.</summary>
public static class ReportAdvice
{
    /// <summary>Höchstzahl der Fundstellen in <c>{leads}</c>.</summary>
    private const int MaxLeadLocations = 5;

    /// <summary>
    /// Die Hinweistexte in Regelreihenfolge; jede Regel höchstens einmal. <c>{detail}</c> (Warnung) und <c>{leads}</c> (Hinweis in einer Mod)
    /// werden ersetzt, andere Platzhalter bleiben unverändert.
    /// </summary>
    public static IReadOnlyList<string> For(ReportFacts facts, IReadOnlyList<AdviceRule> rules)
    {
        var advice = new List<string>();
        foreach (var rule in rules)
        {
            switch (rule.Trigger)
            {
                case AdviceTrigger.Warning:
                    var warning = facts.Summary.Warnings.FirstOrDefault(w =>
                        string.Equals(w.Kind.ToString(), rule.Key, StringComparison.Ordinal));
                    if (warning is not null)
                        advice.Add(rule.Text.Replace("{detail}", warning.Detail, StringComparison.Ordinal));
                    break;
                case AdviceTrigger.Checksum:
                    if (facts.Summary.ChecksumCategories.Contains(rule.Key))
                        advice.Add(rule.Text);
                    break;
                case AdviceTrigger.LeadInMod:
                    var locations = Locations(facts.Leads);
                    if (locations.Count > 0)
                        advice.Add(rule.Text.Replace("{leads}", string.Join(", ", locations), StringComparison.Ordinal));
                    break;
            }
        }

        return advice;
    }

    /// <summary>Die verschiedenen Fundstellen <c>Datei:Zeile (Mod)</c> der Hinweise in einer Mod, in der Reihenfolge der Hinweise.</summary>
    private static List<string> Locations(IReadOnlyList<ReportLead> leads) =>
        leads.Where(l => l.ModName is not null)
            .Select(l => string.Create(CultureInfo.InvariantCulture,
                $"{l.File}{(l.Line is { } line ? $":{line}" : "")} ({l.ModName})").TrimStart())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxLeadLocations)
            .ToList();
}
