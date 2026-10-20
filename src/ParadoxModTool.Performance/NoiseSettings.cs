namespace ParadoxModTool.Performance;

/// <summary>Rauschschwellen für den Laufvergleich. Eine Zeitänderung zählt nur, wenn sie sowohl die absolute Untergrenze als auch die relative Schwelle überschreitet.</summary>
/// <param name="RelativeThreshold">Relative Schwelle bezogen auf die Gesamtzeit in Lauf A (0,10 = 10 %).</param>
/// <param name="AbsoluteFloor">Absolute Untergrenze der Gesamtzeit-Änderung in Rohwerten des Dumps.</param>
/// <param name="CallsChangeThreshold">Relative Änderung der Aufrufzahl, ab der die Zeile als verändertes Verhalten gilt (0,25 = 25 %).</param>
public sealed record NoiseSettings(double RelativeThreshold = 0.10, double AbsoluteFloor = 0.05, double CallsChangeThreshold = 0.25);
