using ParadoxModTool.Mods;

namespace ParadoxModTool.ModCompat;

internal enum Verdict
{
    Normal,
    InfoOnly,
    Suppress,
    Compatch,
}

/// <summary>Bewertet die Beziehung aller Mods einer Fundstelle: Dev-Branches ersetzen sich (kein Konflikt), Submods gehören dazu (nur Info), ein Kompatch für alle übrigen macht den Fund zur Info.</summary>
internal static class RelationVerdict
{
    public static Verdict Judge(IReadOnlyList<CompatParticipant> group, Func<CompatParticipant, CompatParticipant, ModRelationKind>? relation) =>
        Judge(group, relation, null, out _);

    public static Verdict Judge(
        IReadOnlyList<CompatParticipant> group,
        Func<CompatParticipant, CompatParticipant, ModRelationKind>? relation,
        Func<CompatParticipant, CompatParticipant, bool>? isCompatchFor,
        out CompatParticipant? compatch)
    {
        compatch = null;
        var distinct = group.GroupBy(p => p.Id).Select(g => g.First()).ToList();
        if (isCompatchFor is not null)
        {
            var mods = distinct.Where(p => !p.IsVanilla).ToList();
            if (mods.Count >= 3)
            {
                compatch = mods.FirstOrDefault(p => mods.Where(q => q.Id != p.Id).All(q => isCompatchFor(p, q)));
                if (compatch is not null)
                {
                    return Verdict.Compatch;
                }
            }
        }

        if (relation is null)
        {
            return Verdict.Normal;
        }

        var kinds = new List<ModRelationKind>();
        for (var i = 0; i < distinct.Count; i++)
        {
            for (var j = i + 1; j < distinct.Count; j++)
            {
                kinds.Add(relation(distinct[i], distinct[j]));
            }
        }

        if (kinds.Count == 0 || kinds.Any(k => k == ModRelationKind.Unrelated))
        {
            return Verdict.Normal;
        }

        return kinds.All(k => k == ModRelationKind.Alternative) ? Verdict.Suppress : Verdict.InfoOnly;
    }
}
