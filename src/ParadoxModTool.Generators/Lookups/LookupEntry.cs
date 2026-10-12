namespace ParadoxModTool.Generators.Lookups;

/// <summary>Ein Eintrag der Ideen-Datenbank: Quelle, Schlüssel, Name und Beschreibung (Englisch), Tag und Nation; fehlende Werte sind leer, nie null.</summary>
public sealed record LookupEntry(string SourceId, string Key, string Name, string Desc, string Tag, string Nation);
