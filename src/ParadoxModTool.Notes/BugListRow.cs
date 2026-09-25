namespace ParadoxModTool.Notes;

/// <summary>
/// Eine Zeile in der Bug-Liste der Notizleiste: die Meldung plus der Bezug, der in dieser Ansicht zählt (in "Diese Datei" der Bezug auf die Datei,
/// in "Ganzes Projekt" der bevorzugte Bezug). <see cref="LocationText"/> zeigt in der Projekt-Ansicht zusätzlich den Pfad.
/// </summary>
public sealed class BugListRow
{
    private readonly bool _showPath;

    public BugListRow(BugReport report, BugLink? link, bool showPath)
    {
        Report = report;
        Link = link;
        _showPath = showPath;
    }

    public BugReport Report { get; }

    public BugLink? Link { get; }

    public string Id => Report.Id;

    public string Title => Report.Title;

    public string Preview => Report.BodyPreview;

    public string LocationText => Link is null
        ? string.Empty
        : !_showPath ? Link.LineText
        : Link.LineText.Length == 0 ? Link.DisplayPath : $"{Link.DisplayPath}, {Link.LineText}";

    public string SourceIcon => IconFor(Report.Source);

    public bool IsOpen => Report.IsOpen;

    public bool IsClosed => !Report.IsOpen;

    public bool IsLocal => Report.Source == BugSource.Local;

    public bool IsUnseen => !Report.Seen;

    public static string IconFor(BugSource source) => source switch
    {
        BugSource.GitHub => "🐙",
        BugSource.Discord => "💬",
        _ => "🐞",
    };
}
