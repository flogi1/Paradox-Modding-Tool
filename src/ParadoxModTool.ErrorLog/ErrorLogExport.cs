using System.Globalization;
using System.Text;

namespace ParadoxModTool.ErrorLog;

public enum ExportFormat
{
    Text,
    Csv,
}

/// <summary>
/// Export der gefilterten Liste (Spec: Text oder CSV). CSV mit Semikolon als Trenner (so öffnet Excel mit deutscher Einstellung die Spalten
/// direkt), eine Zeile je Log-Eintrag. Ohne Vergleichslauf (<c>withStatus = false</c>) bleibt der Status leer.
/// </summary>
public static class ErrorLogExport
{
    private const string LineEnd = "\r\n";

    public static string Write(IReadOnlyList<ErrorSection> sections, ExportFormat format, string language, string? runName, bool withStatus, IReadOnlyList<string>? runFiles = null) =>
        format == ExportFormat.Csv ? Csv(sections, language, withStatus) : Text(sections, language, runName, withStatus, runFiles ?? Array.Empty<string>());

    private static string Csv(IReadOnlyList<ErrorSection> sections, string language, bool withStatus)
    {
        var builder = new StringBuilder();
        builder.Append(ErrorLogTexts.ExportColumns).Append(LineEnd);
        foreach (var section in sections)
        {
            var sectionName = ErrorLogTexts.SectionName(section);
            foreach (var group in section.Groups)
            {
                var kindName = ErrorLogTexts.KindName(group.Kind, language);
                var status = withStatus ? ErrorLogTexts.Status(group.Status) : string.Empty;
                foreach (var entry in group.Entries)
                {
                    var fields = new[]
                    {
                        sectionName,
                        group.Attribution.Mod?.Name ?? string.Empty,
                        group.Code,
                        kindName,
                        ErrorLogTexts.Severity(group.Severity),
                        status,
                        entry.FilePath ?? string.Empty,
                        entry.FileLine?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                        entry.Name ?? string.Empty,
                        entry.Message,
                    };
                    builder.AppendJoin(';', fields.Select(Field)).Append(LineEnd);
                }
            }
        }

        return builder.ToString();
    }

    private static string Text(IReadOnlyList<ErrorSection> sections, string language, string? runName, bool withStatus, IReadOnlyList<string> runFiles)
    {
        var builder = new StringBuilder();
        builder.Append(ErrorLogTexts.ExportHeading(runName ?? string.Empty)).Append(LineEnd);
        foreach (var section in sections)
        {
            builder.Append(LineEnd).Append(ErrorLogTexts.Section(section)).Append(LineEnd);
            foreach (var group in section.Groups)
            {
                var title = ErrorLogTexts.GroupTitle(ErrorLogTexts.DetailHeading(ErrorLogTexts.KindName(group.Kind, language), group.Code), group.GroupName);
                builder.Append("  ").Append(ErrorLogTexts.SeverityIcon(group.Severity)).Append(' ').Append(title).Append("  ");
                if (section.Kind == ErrorSectionKind.Fixed)
                {
                    builder.Append(ErrorLogTexts.GroupChange(group.PreviousCount, 0));
                }
                else
                {
                    builder.Append(ErrorLogTexts.GroupCount(group.Count));
                    if (withStatus)
                    {
                        builder.Append("  ").Append(group.Status == RunStatus.New
                            ? ErrorLogTexts.Status(RunStatus.New)
                            : ErrorLogTexts.GroupChange(group.PreviousCount, group.Count));
                    }
                }

                builder.Append(LineEnd);
                foreach (var entry in group.Entries)
                {
                    builder.Append("    ").Append(Location(entry, runFiles)).Append(LineEnd);
                }
            }
        }

        return builder.ToString();
    }

    private static string Location(ErrorEntry entry, IReadOnlyList<string> runFiles) => ErrorLogTexts.LogPosition(entry, runFiles);

    private static string Field(string value) =>
        value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
}
