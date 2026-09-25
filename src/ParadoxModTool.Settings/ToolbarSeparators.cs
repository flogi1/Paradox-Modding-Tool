namespace ParadoxModTool.Settings;

/// <summary>
/// Trennlinien der allgemeinen Werkzeugleiste: Vor jeder Gruppe außer der ersten steht eine Trennlinie. Sie ist sichtbar, wenn die Gruppe
/// selbst und irgendeine Gruppe davor je mindestens einen sichtbaren Eintrag haben. So steht zwischen zwei sichtbaren Gruppen genau eine
/// Trennlinie, auch wenn eine leere Gruppe dazwischen liegt, und am Rand keine.
/// </summary>
public static class ToolbarSeparators
{
    public static IReadOnlySet<ToolbarGroup> VisibleBefore(Func<string, bool> isVisible)
    {
        var result = new HashSet<ToolbarGroup>();
        var anyBefore = false;
        foreach (var group in Enum.GetValues<ToolbarGroup>())
        {
            var groupVisible = ToolbarCatalog.All.Any(item => item.Group == group && isVisible(item.Id));
            if (groupVisible && anyBefore)
            {
                result.Add(group);
            }

            anyBefore |= groupVisible;
        }

        return result;
    }
}
