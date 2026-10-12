using System.Globalization;
using Scriban.Runtime;

namespace ParadoxModTool.Generators;

/// <summary>
/// Die Formulardaten als Scriban-Modell: Kopf-Felder, <c>constants</c>, <c>languages</c>, <c>lang</c>, <c>language_rules</c> und je Ebene ein Feld mit
/// den Elementen.
/// Ein Element hat seine Felder, bei festen Gruppen die Werte seiner Konstanten-Zeile, die Kind-Ebenen und die Hilfswerte
/// <c>index</c> (ab 1), <c>index0</c>, <c>first</c>, <c>last</c>, <c>number</c> (Basis des Elternelements + Index), <c>suffix</c> und <c>parent</c>.
/// Zu jedem mehrsprachigen Feld <c>x</c> gibt es <c>x_by_lang</c>: je Sprache des Arbeitsstands der aufgelöste Text, roh (ohne .yml-Maskierung), für JSON-Vorlagen.
/// </summary>
public static class TemplateModel
{
    public static ScriptObject Build(GeneratorPackage package, GeneratorState state, string? language)
    {
        var model = new ScriptObject();
        AddFields(model, package.Head, state.Head, state.LanguageRules, language, state.Languages);

        var constants = new ScriptObject();
        foreach (var (name, rows) in package.Constants)
        {
            var array = new ScriptArray();
            foreach (var row in rows)
            {
                var item = new ScriptObject();
                foreach (var (key, value) in row)
                {
                    item[key] = value;
                }

                array.Add(item);
            }

            constants[name] = array;
        }

        model["constants"] = constants;
        model["languages"] = new ScriptArray(state.Languages);
        model["lang"] = language;
        model["language_rules"] = new ScriptObject
        {
            ["extend_english"] = state.LanguageRules.ExtendEnglish,
            ["copy_english"] = state.LanguageRules.CopyEnglish,
            ["english_for_missing"] = state.LanguageRules.EnglishForMissing,
        };
        if (package.Root is { } root)
        {
            model[root.Id] = Level(package, root, state.Head.Children.GetValueOrDefault(root.Id) ?? new List<FormNode>(), model, state.LanguageRules, language, state.Languages);
        }

        return model;
    }

    private static ScriptArray Level(GeneratorPackage package, LevelDef level, List<FormNode> nodes, ScriptObject parent, LanguageDefaults rules, string? language, IReadOnlyList<string> languages)
    {
        var rows = level.ConstantsTable is { } table && package.Constants.TryGetValue(table, out var r) ? r : null;
        var array = new ScriptArray();
        for (var i = 0; i < nodes.Count; i++)
        {
            var item = new ScriptObject();
            if (rows is not null && i < rows.Count)
            {
                foreach (var (key, value) in rows[i])
                {
                    item[key] = value;
                }
            }

            AddFields(item, level.Fields, nodes[i], rules, language, languages);
            item["index"] = i + 1;
            item["index0"] = i;
            item["first"] = i == 0;
            item["last"] = i == nodes.Count - 1;
            item["suffix"] = i == 0 ? string.Empty : "_" + i.ToString(CultureInfo.InvariantCulture);
            item["parent"] = parent;
            if (level.NumberBaseField is { } baseField && parent[baseField] is { } baseValue
                && int.TryParse(Convert.ToString(baseValue, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                item["number"] = number + i + 1;
            }
            else if (level.NumberBaseField is null)
            {
                // Ohne Nummer-Basisfeld ist die Nummer der Index ab 1 (so liest der Einleser zählbare Ebenen auch zurück).
                item["number"] = i + 1;
            }

            if (level.Child is { } child)
            {
                item[child.Id] = Level(package, child, nodes[i].Children.GetValueOrDefault(child.Id) ?? new List<FormNode>(), item, rules, language, languages);
            }

            array.Add(item);
        }

        return array;
    }

    /// <summary>Feldwerte nach Typ: Ja/Nein als bool, Zahlen als int (sonst Text); mehrsprachige Texte in der Sprache, in .yml maskiert,
    /// dazu <c>&lt;feld&gt;_by_lang</c> mit dem rohen Text je Sprache von <paramref name="languages"/>.</summary>
    private static void AddFields(ScriptObject target, IReadOnlyList<FieldDef> fields, FormNode node, LanguageDefaults rules, string? language, IReadOnlyList<string> languages)
    {
        foreach (var field in fields)
        {
            var value = node.Values.GetValueOrDefault(field.Id) ?? field.Default ?? string.Empty;
            target[field.Id] = field.Type switch
            {
                FieldType.Bool => value is "yes" or "true",
                FieldType.Number when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
                _ when field.Multilingual => Text(value, node.Translations.GetValueOrDefault(field.Id), rules, language),
                _ => value,
            };

            if (field.Multilingual)
            {
                var byLang = new ScriptObject();
                var translations = node.Translations.GetValueOrDefault(field.Id);
                foreach (var name in languages)
                {
                    byLang[name] = LanguageFill.Resolve(value, translations, name, rules);
                }

                target[field.Id + "_by_lang"] = byLang;
            }
        }
    }

    private static string Text(string english, IReadOnlyDictionary<string, string>? translations, LanguageDefaults rules, string? language) =>
        language is null ? english : LanguageFill.YamlValue(LanguageFill.Resolve(english, translations, language, rules));
}
