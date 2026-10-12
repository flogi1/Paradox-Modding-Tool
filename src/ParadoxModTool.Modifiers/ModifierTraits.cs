namespace ParadoxModTool.Modifiers;

/// <summary>
/// Eigenschaften eines Modifiers aus den Definitionsdateien, die nicht die Anzeige des Werts betreffen (Spec 2026-10-05-generator-packages-lookups):
/// <see cref="Boolean"/> (<c>boolean = yes</c>, Wert ja/nein), <see cref="Bad"/> (<c>color = bad</c>, ein höherer Wert schadet) und
/// <see cref="Category"/> (<c>category</c>, z. B. <c>country</c>; leer ohne Angabe).
/// </summary>
public sealed record ModifierTraits(bool Boolean, bool Bad, string Category);
