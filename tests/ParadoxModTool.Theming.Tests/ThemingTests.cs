using Xunit;
using ParadoxModTool.Theming;

namespace ParadoxModTool.Theming.Tests;

public class ThemeTokenTests
{
    [Fact]
    public void Alle_Tokens_haben_eindeutige_Schluessel_und_gueltige_Standardfarben()
    {
        Assert.Equal(ThemeTokens.All.Count, ThemeTokens.All.Select(t => t.Key).Distinct().Count());
        foreach (var token in ThemeTokens.All)
        {
            Assert.True(HexColor.IsValid(token.Light), $"{token.Key} hell");
            Assert.True(HexColor.IsValid(token.Dark), $"{token.Key} dunkel");
            Assert.Contains(token.Group, ThemeTokens.Groups);
        }
    }

    [Theory]
    [InlineData("#FFFFFF", true)]
    [InlineData("#80FFFFFF", true)]
    [InlineData("#fff", false)]
    [InlineData("FFFFFF", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HexColor_IsValid(string? value, bool expected) => Assert.Equal(expected, HexColor.IsValid(value));

    [Fact]
    public void Eingebaute_Themes_enthalten_alle_Tokens_und_Kategorien()
    {
        foreach (var theme in new[] { BuiltInThemes.CreateLight(), BuiltInThemes.CreateDark() })
        {
            Assert.All(ThemeTokens.All, t => Assert.True(theme.Colors.ContainsKey(t.Key)));
            Assert.All(ThemeDefinition.CategoryKeys, k => Assert.True(theme.CategoryColors.ContainsKey(k)));
            Assert.True(theme.IsBuiltIn);
        }

        Assert.False(BuiltInThemes.CreateLight().IsDark);
        Assert.True(BuiltInThemes.CreateDark().IsDark);
    }
}

public class ThemeNormalizerTests
{
    [Fact]
    public void Fehlende_und_ungueltige_Werte_kommen_aus_dem_Basistheme()
    {
        var source = new ThemeDefinition
        {
            Id = "custom-x",
            Name = "  Mein Theme ",
            IsDark = true,
            Colors = { ["Text"] = "#123456", ["Accent"] = "kaputt", ["Unbekannt"] = "#FFFFFF" },
            CategoryColors = { ["Text"] = "#ABCDEF", ["Gui"] = "rot" },
        };

        var result = ThemeNormalizer.Normalize(source);

        Assert.Equal("Mein Theme", result.Name);
        Assert.Equal("#123456", result.Colors["Text"]);
        Assert.Equal(BuiltInThemes.CreateDark().Colors["Accent"], result.Colors["Accent"]);
        Assert.False(result.Colors.ContainsKey("Unbekannt"));
        Assert.Equal(ThemeTokens.All.Count, result.Colors.Count);
        Assert.Equal("#ABCDEF", result.CategoryColors["Text"]);
        Assert.Equal(BuiltInThemes.CreateDark().CategoryColors["Gui"], result.CategoryColors["Gui"]);
        Assert.Equal(ThemeDefinition.CategoryKeys.Count, result.CategoryColors.Count);
    }

    [Fact]
    public void Leerer_Name_wird_ersetzt()
    {
        Assert.Equal("Unbenanntes Theme", ThemeNormalizer.Normalize(new ThemeDefinition { Id = "custom-x" }).Name);
    }
}

public class ThemeSelectionTests
{
    private static ThemeDefinition Custom(string id) => ThemeNormalizer.Normalize(new ThemeDefinition { Id = id, Name = id });

    [Fact]
    public void System_folgt_der_Windows_Einstellung()
    {
        Assert.Equal(ThemeDefinition.DarkId, ThemeSelection.Resolve(ThemeSelection.SystemId, true, []).Id);
        Assert.Equal(ThemeDefinition.LightId, ThemeSelection.Resolve(ThemeSelection.SystemId, false, []).Id);
        Assert.Equal(ThemeDefinition.LightId, ThemeSelection.Resolve(null, false, []).Id);
    }

    [Fact]
    public void Feste_Auswahl_ignoriert_die_Windows_Einstellung()
    {
        Assert.Equal(ThemeDefinition.LightId, ThemeSelection.Resolve(ThemeDefinition.LightId, true, []).Id);
        Assert.Equal("custom-a", ThemeSelection.Resolve("custom-a", true, [Custom("custom-a")]).Id);
    }

    [Fact]
    public void Geloeschtes_eigenes_Theme_faellt_auf_System_zurueck()
    {
        Assert.Equal(ThemeDefinition.DarkId, ThemeSelection.Resolve("custom-weg", true, [Custom("custom-a")]).Id);
    }

    [Fact]
    public void Kategorie_Farben_persoenliche_Anpassung_hat_Vorrang_und_ungueltige_werden_ignoriert()
    {
        var theme = BuiltInThemes.CreateDark();
        var effective = ThemeSelection.EffectiveCategoryColors(
            theme,
            new Dictionary<string, string> { ["Text"] = "#010203", ["Gui"] = "kaputt" });

        Assert.Equal("#010203", effective["Text"]);
        Assert.Equal(theme.CategoryColors["Gui"], effective["Gui"]);
        Assert.Equal(theme.CategoryColors["Other"], effective["Other"]);
        Assert.Equal(ThemeDefinition.CategoryKeys.Count, effective.Count);
    }

    [Fact]
    public void Kategorie_Farben_ohne_Anpassung_kommen_vom_Theme()
    {
        var theme = BuiltInThemes.CreateLight();
        Assert.Equal(theme.CategoryColors, ThemeSelection.EffectiveCategoryColors(theme, null));
    }
}

public sealed class ThemeStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-theme-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private static ThemeDefinition NewCustom(string name)
    {
        var theme = BuiltInThemes.CreateDark();
        theme.Id = ThemeStore.NewCustomId();
        theme.Name = name;
        theme.Colors["Accent"] = "#FF00AA";
        return theme;
    }

    [Fact]
    public void Speichern_und_Laden_ist_verlustfrei()
    {
        var theme = NewCustom("Violett");
        ThemeStore.Save(theme, _dir);

        var loaded = Assert.Single(ThemeStore.LoadAll(_dir));
        Assert.Equal(theme.Id, loaded.Id);
        Assert.Equal("Violett", loaded.Name);
        Assert.True(loaded.IsDark);
        Assert.Equal("#FF00AA", loaded.Colors["Accent"]);
    }

    [Fact]
    public void Eingebaute_Themes_lassen_sich_nicht_speichern()
    {
        Assert.Throws<InvalidOperationException>(() => ThemeStore.Save(BuiltInThemes.CreateLight(), _dir));
    }

    [Fact]
    public void LoadAll_ueberspringt_defekte_Dateien_und_sortiert_nach_Name()
    {
        ThemeStore.Save(NewCustom("Zebra"), _dir);
        ThemeStore.Save(NewCustom("Adler"), _dir);
        File.WriteAllText(Path.Combine(_dir, "kaputt.json"), "{ nicht json");
        File.WriteAllText(Path.Combine(_dir, "leer.json"), "{}");

        Assert.Equal(["Adler", "Zebra"], ThemeStore.LoadAll(_dir).Select(t => t.Name));
    }

    [Fact]
    public void LoadAll_ohne_Ordner_ist_leer() => Assert.Empty(ThemeStore.LoadAll(_dir));

    [Fact]
    public void Loeschen_entfernt_die_Datei()
    {
        var theme = NewCustom("Weg");
        ThemeStore.Save(theme, _dir);
        ThemeStore.Delete(theme.Id, _dir);
        Assert.Empty(ThemeStore.LoadAll(_dir));
        ThemeStore.Delete(theme.Id, _dir);
    }

    [Fact]
    public void Import_vergibt_neue_Kennung_auch_bei_vorgetaeuschtem_Built_in()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "export.json");
        var theme = NewCustom("Fremd");
        theme.Id = ThemeDefinition.LightId;
        ThemeStore.Export(theme, path);

        var imported = ThemeStore.Import(path);

        Assert.NotNull(imported);
        Assert.StartsWith("custom-", imported.Id);
        Assert.False(imported.IsBuiltIn);
        Assert.Equal("#FF00AA", imported.Colors["Accent"]);
    }

    [Fact]
    public void Import_liefert_null_fuer_Nicht_Themes()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "x.json");
        File.WriteAllText(path, "[1,2,3]");
        Assert.Null(ThemeStore.Import(path));
        File.WriteAllText(path, "{\"Name\":\"x\"}");
        Assert.Null(ThemeStore.Import(path));
        Assert.Null(ThemeStore.Import(Path.Combine(_dir, "gibtsnicht.json")));
    }
}
