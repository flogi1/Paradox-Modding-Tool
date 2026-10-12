using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Lookups;
using Button = System.Windows.Controls.Button;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Felder mit <c>lookup</c> (Spec 2026-10-05-generator-packages-lookups, Abschnitt 1): Knopf "Aus Datenbank", Katalog je Paket und Feld gemerkt, Übernehmen setzt die
/// Felder aus <c>fills</c> und die offiziellen Übersetzungen der Spielquellen.
/// </summary>
public partial class GeneratorWindow
{
    private readonly Dictionary<string, LookupCatalog> _lookupCatalogs = new(StringComparer.Ordinal);

    private FrameworkElement WithLookupButton(FrameworkElement control, FieldDef field, FormNode node)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(control);
        panel.Children.Add(LookupButton(field, node));
        return panel;
    }

    private Button LookupButton(FieldDef field, FormNode node)
    {
        var button = new Button { Content = new TextBlock { Text = GeneratorTexts.LookupButton }, ToolTip = GeneratorTexts.LookupButtonTip, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        button.Click += async (_, _) => await OpenLookup(field, node);
        return button;
    }

    private async Task OpenLookup(FieldDef field, FormNode node)
    {
        if (_package is not { } package || _state is null || field.Lookup is not { } lookup)
        {
            return;
        }

        var key = $"{package.Folder}|{field.Id}";
        async Task<LookupCatalog> Load(bool force)
        {
            if (!force && _lookupCatalogs.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var roots = _host.GameRoots();
            var catalog = await Task.Run(() => LookupCatalog.Load(package, lookup, roots));
            _lookupCatalogs[key] = catalog;
            return catalog;
        }

        void ChooseAsset(string sourceId)
        {
            if (package.Lookups?.FirstOrDefault(s => s.Id == sourceId)?.Asset is { } asset)
            {
                PickAsset(asset);
            }
        }

        var window = new GeneratorLookupWindow(this, Load, _host.Session(), _host.ChooseGameFolder, ChooseAsset);
        if (window.ShowDialog() == true && window.Picked is { } entry && window.Catalog is { } catalog)
        {
            await ApplyLookup(package, field, lookup, node, entry, catalog);
        }
    }

    /// <summary>Setzt die Felder aus <c>fills</c>; mehrsprachige Felder bekommen die Texte der Spielquelle je Sprache des Arbeitsstands oder keine Übersetzung.</summary>
    private async Task ApplyLookup(GeneratorPackage package, FieldDef field, FieldLookupDef lookup, FormNode node, LookupEntry entry, LookupCatalog catalog)
    {
        var languages = _state!.Languages.Where(l => l != LanguageFill.English).ToList();
        var roots = _host.GameRoots();
        Cursor = Cursors.Wait;
        Dictionary<string, IReadOnlyDictionary<string, string>> perLanguage;
        try
        {
            perLanguage = await Task.Run(() => languages.ToDictionary(l => l, l => catalog.Translations(entry, l, roots)));
        }
        finally
        {
            Cursor = null;
        }

        var fields = FieldsBeside(package, field);
        foreach (var (entryField, formField) in lookup.Fills)
        {
            var value = entryField switch
            {
                "name" => entry.Name,
                "desc" => entry.Desc,
                _ => null,
            };
            if (value is null)
            {
                continue;
            }

            node.Values[formField] = value;
            if (fields.FirstOrDefault(f => f.Id == formField) is { Multilingual: true })
            {
                var translations = perLanguage
                    .Where(p => p.Value.TryGetValue(entryField, out var text) && text.Length > 0)
                    .ToDictionary(p => p.Key, p => p.Value[entryField], StringComparer.Ordinal);
                if (translations.Count > 0)
                {
                    node.Translations[formField] = translations;
                }
                else
                {
                    node.Translations.Remove(formField);
                }
            }
        }

        MarkDirty();
        BuildForm();
    }

    /// <summary>Die Feldliste (Kopf oder Ebene), in der das Feld steht.</summary>
    private static IReadOnlyList<FieldDef> FieldsBeside(GeneratorPackage package, FieldDef field)
    {
        if (package.Head.Contains(field))
        {
            return package.Head;
        }

        for (var level = package.Root; level is not null; level = level.Child)
        {
            if (level.Fields.Contains(field))
            {
                return level.Fields;
            }
        }

        return Array.Empty<FieldDef>();
    }
}
