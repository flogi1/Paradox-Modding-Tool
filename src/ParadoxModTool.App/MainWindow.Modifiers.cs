using System.IO;
using System.Windows;
using ParadoxModTool.Editor;
using ParadoxModTool.Localization;
using ParadoxModTool.Modifiers;
using ParadoxModTool.ScriptDocs;

namespace ParadoxModTool.App;

/// <summary>
/// Modifier-Werte wie im Spiel: baut den Index der Anzeigeformate (siehe <see cref="ModifierFormatIndex"/>) aus Spiel- und Mod-Ordnern bzw. der
/// Wiki-Script-Datenbank, dazu die Script-Values (<see cref="ScriptValueIndex"/>) und die übersetzten Namen (<see cref="ModifierNameIndex"/>), und reicht
/// sie an den Editor weiter (<see cref="ModifierValueSettings"/>).
/// </summary>
public partial class MainWindow
{
    private int _modifierFormatsVersion;

    private void ModifierReload_Click(object sender, RoutedEventArgs e) => ModifierValueSettings.RequestReload();

    /// <summary>
    /// Baut den Index im Hintergrund neu (beim Start, bei geänderten Explorer-Wurzeln, bei neuer Script-Datenbank). Nur das Ergebnis des jüngsten
    /// Aufrufs wird übernommen; ein Lesefehler lässt den bisherigen Index stehen, ohne Meldung.
    /// </summary>
    private async void RefreshModifierFormats(bool report = false)
    {
        var version = ++_modifierFormatsVersion;
        var settings = _activeProfile?.ModifierFormats;
        var (gameRoots, modRoots) = CollectOriginRoots();
        var wiki = settings?.Source switch
        {
            "wiki" when _scriptDocsService is { } service => service.Database.Entries
                .Where(entry => entry.Kind == ScriptDocKind.Modifier && entry.Extra.TryGetValue("Effect type", out _))
                .Select(entry => (entry.Name, entry.Extra["Effect type"]))
                .ToList(),
            // Format aus der Namensendung (Stellaris): alle Modifier der Script-Datenbank, mit ihrer Kategorie.
            "suffix" when _scriptDocsService is { } service => service.Database.Entries
                .Where(entry => entry.Kind == ScriptDocKind.Modifier)
                .Select(entry => (entry.Name, entry.Extra.GetValueOrDefault("Category") ?? string.Empty))
                .ToList(),
            _ => new List<(string, string)>(),
        };

        // Hearts of Iron IV: Nachkommastellen je Modifier aus der Doku ("Number with N decimal places").
        var docDecimals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (settings?.Source == "suffix" && _scriptDocsService is { } docs)
        {
            foreach (var entry in docs.Database.Entries.Where(e => e.Kind == ScriptDocKind.Modifier))
            {
                if (entry.Extra.TryGetValue("Decimals", out var text)
                    && int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var decimals))
                {
                    docDecimals[entry.Name] = decimals;
                }
            }
        }

        var languages = ModifierNameIndex.GameLanguages(UiText.Current.Language);

        try
        {
            var (index, scriptValues) = await Task.Run(() => (ModifierFormatIndex.Build(settings, gameRoots, modRoots, wiki, docDecimals), ScriptValueIndex.Build(gameRoots, modRoots)));
            if (version != _modifierFormatsVersion)
            {
                return;
            }

            // Formate und Script-Values sind in Millisekunden fertig; die Namen lesen die ganze Lokalisierung und folgen danach.
            ModifierValueSettings.SetIndex(index, scriptValues);
            var names = await Task.Run(() => ModifierNameIndex.Build(settings, index.AllNames, gameRoots, modRoots, languages));
            if (version == _modifierFormatsVersion)
            {
                ModifierValueSettings.SetNames(names);
                if (report)
                {
                    ModStatusText.Text = ModifierTexts.Reloaded(index.Count, scriptValues.Count, names.Count);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ohne Formate zeigt der Editor einfach keinen Wert an.
        }
    }
}
