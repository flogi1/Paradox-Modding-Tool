namespace ParadoxModTool.ModCompat;

/// <summary>Ein Teilnehmer der Prüfung: Vanilla (Rang 0, nur lesend) oder eine Mod des Sets. Höherer Rang = später geladen = gewinnt.</summary>
public sealed record CompatParticipant(string Id, string Name, string Folder, int Rank, bool IsVanilla, string? ModId = null);

/// <summary>Fortschritt einer Prüfung für die Anzeige.</summary>
public sealed record CompatProgress(string Text, int Done, int Total);
