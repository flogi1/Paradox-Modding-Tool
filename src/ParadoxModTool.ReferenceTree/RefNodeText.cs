using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.ReferenceTree;

/// <summary>Beschriftung und Tooltip eines Knotens im Verweisbaum.</summary>
public static class RefNodeText
{
    /// <summary>Text und (nur bei Stellen) Tooltip des Knotens.</summary>
    public static (string Text, string? Tooltip) Format(RefNode node)
    {
        switch (node.NodeKind)
        {
            case RefNodeKind.Root:
                return ($"{node.Name} ({KindName(node)}){PartialSuffix(node)}", null);
            case RefNodeKind.Branch:
                return ($"{BranchName(node.Branch)} ({node.Count})", null);
            case RefNodeKind.Group:
                return ($"{GroupName(node.Group)} ({node.Count})", null);
            default:
                return FormatSite(node);
        }
    }

    private static (string, string?) FormatSite(RefNode node)
    {
        var location = node.Location;
        var place = location is null ? string.Empty : $"{Path.GetFileName(location.RelativePath)}:{location.Line}";
        var kind = KindName(node);
        var head = node.Name.Length == 0
            ? place
            : (kind.Length == 0 ? node.Name : $"{kind} {node.Name}") + (place.Length == 0 ? string.Empty : $" — {place}");
        var text = (node.Cycle ? "↻ " : string.Empty) + head + PartialSuffix(node);
        var tooltip = location is null ? null : $"{location.FullPath}:{location.Line}\n{node.OriginName}";
        return (text, tooltip);
    }

    private static string PartialSuffix(RefNode node) => node.Partial ? $" — {ReferenceTreeTexts.Partial}" : string.Empty;

    private static string KindName(RefNode node)
    {
        if (node.FlagKind is { } flag)
        {
            return flag is FlagKind.ScopeFlag or FlagKind.DefinitionFlag ? ReferenceTreeTexts.KindFlag : ReferenceTreeTexts.KindVariable;
        }

        return node.CheckKind switch
        {
            CheckKind.Event => ReferenceTreeTexts.KindEvent,
            CheckKind.OnAction => ReferenceTreeTexts.KindOnAction,
            CheckKind.Effect => ReferenceTreeTexts.KindEffect,
            CheckKind.Trigger => ReferenceTreeTexts.KindTrigger,
            CheckKind.Value => ReferenceTreeTexts.KindValue,
            CheckKind.Other or CheckKind.Named => ReferenceTreeTexts.KindOther,
            _ => string.Empty,
        };
    }

    private static string BranchName(RefBranch? branch) => branch switch
    {
        RefBranch.Set => ReferenceTreeTexts.BranchSet,
        RefBranch.Read => ReferenceTreeTexts.BranchRead,
        RefBranch.Remove => ReferenceTreeTexts.BranchRemove,
        RefBranch.UsedBy => ReferenceTreeTexts.BranchUsedBy,
        RefBranch.Uses => ReferenceTreeTexts.BranchUses,
        RefBranch.Sets => ReferenceTreeTexts.BranchSets,
        RefBranch.Reads => ReferenceTreeTexts.BranchReads,
        _ => string.Empty,
    };

    private static string GroupName(RefGroup? group) => group switch
    {
        RefGroup.OtherMods => ReferenceTreeTexts.GroupOtherMods,
        RefGroup.Vanilla => ReferenceTreeTexts.GroupVanilla,
        _ => string.Empty,
    };
}
