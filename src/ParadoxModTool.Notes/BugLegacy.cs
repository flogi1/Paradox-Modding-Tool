using System.Text.Json;

namespace ParadoxModTool.Notes;

/// <summary>
/// Liest die alte <c>bugs.json</c> (Wörterbuch "Notiz-Schlüssel → Liste von Bugs", ein Bug je Datei) und wandelt sie ins neue Modell.
/// Jeder alte Bug wird eine lokale Meldung mit genau einem Bezug. Die alte Datei selbst wird nie geschrieben.
/// </summary>
public static class BugLegacy
{
    private sealed class V1Entry
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public bool Done { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? DoneUtc { get; set; }

        public int? Line { get; set; }

        public int? EndLine { get; set; }
    }

    /// <summary>Wandelt den Inhalt der alten Datei um. Wirft <see cref="JsonException"/> bei kaputtem JSON.</summary>
    public static BugData Convert(string json)
    {
        var v1 = JsonSerializer.Deserialize<Dictionary<string, List<V1Entry>?>>(json) ?? new Dictionary<string, List<V1Entry>?>();
        var data = new BugData();
        foreach (var (key, entries) in v1)
        {
            foreach (var entry in entries ?? new List<V1Entry>())
            {
                data.Reports.Add(new BugReport
                {
                    Id = string.IsNullOrEmpty(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
                    ModId = BugLink.ModIdOf(key),
                    Title = entry.Title ?? string.Empty,
                    Body = entry.Description ?? string.Empty,
                    Status = entry.Done ? BugStatus.Done : BugStatus.Open,
                    CreatedUtc = entry.CreatedUtc,
                    UpdatedUtc = entry.DoneUtc ?? entry.CreatedUtc,
                    StatusChangedUtc = entry.DoneUtc,
                    Seen = true,
                    Links = { BugLink.Create(key, entry.Line, entry.EndLine) },
                });
            }
        }

        return data;
    }
}
