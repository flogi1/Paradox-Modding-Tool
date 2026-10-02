namespace ParadoxModTool.Mods;

/// <summary>
/// Eine Abhängigkeit aus der Metadaten-Datei einer Mod: klassisch <c>dependencies = { "Name" }</c> (nur Anzeigename, <paramref name="Id"/> ist
/// <see langword="null"/>), im Launcher-v2-Format ein <c>relationships</c>-Eintrag mit <c>rel_type</c> <c>dependency</c>.
/// </summary>
public sealed record ModDependency(string? Id, string DisplayName);
