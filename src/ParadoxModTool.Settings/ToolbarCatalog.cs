namespace ParadoxModTool.Settings;

/// <summary>Kennungen der Einträge der allgemeinen Werkzeugleiste (gespeichert in der Sitzung, daher nie umbenennen).</summary>
public static class ToolbarItemIds
{
    public const string Undo = "undo";
    public const string Redo = "redo";
    public const string Search = "search";
    public const string GoToLine = "goToLine";
    public const string GoToBracket = "goToBracket";
    public const string ScopeFinder = "scopeFinder";
    public const string GuiDataHelper = "guiDataHelper";
    public const string Templates = "templates";
    public const string Notes = "notes";
    public const string Bookmark = "bookmark";
    public const string Bug = "bug";
    public const string BugTracker = "bugTracker";
    public const string Format = "format";
    public const string SplitView = "splitView";
    public const string Explorer = "explorer";
    public const string NotesPanel = "notesPanel";
    public const string AutoComplete = "autoComplete";
    public const string Hover = "hover";
    public const string ModifierReload = "modifierReload";
    public const string LaunchOption = "launchOption";
    public const string Playset = "playset";
    public const string LaunchGame = "launchGame";
}

/// <summary>Gruppen der allgemeinen Werkzeugleiste; zwischen zwei Gruppen steht eine Trennlinie.</summary>
public enum ToolbarGroup
{
    History,
    Tools,
    Toggles,
    Launch,
}

public sealed record ToolbarItem(string Id, ToolbarGroup Group);

/// <summary>Alle Einträge der allgemeinen Werkzeugleiste in der Reihenfolge der Spec (UI-Reorganisation).</summary>
public static class ToolbarCatalog
{
    public static IReadOnlyList<ToolbarItem> All { get; } = new[]
    {
        new ToolbarItem(ToolbarItemIds.Undo, ToolbarGroup.History),
        new ToolbarItem(ToolbarItemIds.Redo, ToolbarGroup.History),
        new ToolbarItem(ToolbarItemIds.Search, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.GoToLine, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.GoToBracket, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.ScopeFinder, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.GuiDataHelper, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.Templates, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.Format, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.Notes, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.Bookmark, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.Bug, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.BugTracker, ToolbarGroup.Tools),
        new ToolbarItem(ToolbarItemIds.SplitView, ToolbarGroup.Toggles),
        new ToolbarItem(ToolbarItemIds.Explorer, ToolbarGroup.Toggles),
        new ToolbarItem(ToolbarItemIds.NotesPanel, ToolbarGroup.Toggles),
        new ToolbarItem(ToolbarItemIds.AutoComplete, ToolbarGroup.Toggles),
        new ToolbarItem(ToolbarItemIds.Hover, ToolbarGroup.Toggles),
        new ToolbarItem(ToolbarItemIds.ModifierReload, ToolbarGroup.Toggles),
        new ToolbarItem(ToolbarItemIds.LaunchOption, ToolbarGroup.Launch),
        new ToolbarItem(ToolbarItemIds.Playset, ToolbarGroup.Launch),
        new ToolbarItem(ToolbarItemIds.LaunchGame, ToolbarGroup.Launch),
    };

    private static readonly HashSet<string> Known = All.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);

    public static bool IsKnown(string id) => Known.Contains(id);
}
