namespace ParadoxModTool.LaunchControl;

/// <summary>Zuletzt gewählte Startoption wiederfinden: gemerkt werden ihre Argumente, weil die Anzeigenamen der Oberflächensprache folgen.</summary>
public static class LaunchOptionMemory
{
    /// <summary>Index der Option mit den gemerkten Argumenten; ohne Treffer (nie gewählt, Option entfernt) die erste Option, ohne Optionen -1.</summary>
    public static int IndexOf(IReadOnlyList<string> optionArgs, string? savedArgs)
    {
        if (optionArgs.Count == 0)
        {
            return -1;
        }

        for (var i = 0; i < optionArgs.Count; i++)
        {
            if (savedArgs is not null && string.Equals(optionArgs[i], savedArgs, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }
}
