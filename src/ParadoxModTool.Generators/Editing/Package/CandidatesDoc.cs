namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>
/// Abschnitt <c>candidates</c> für den Editor. <c>except</c> und <c>ask</c> stehen hier als Komma-Liste, in der Datei als Array;
/// <c>create.name</c> und <c>create.modifiers</c> sind <see cref="Name"/> und <see cref="Modifiers"/>.
/// </summary>
public sealed record CandidatesDoc(
    string Category,
    IReadOnlyList<(string File, string Block, string Except)> Used,
    IReadOnlyList<(string Id, LocalizedText Label, string Pattern, bool Hidden)> Groups,
    string Ask,
    string Name,
    string Modifiers);
