using System.IO;
using ParadoxModTool.Categories;
using ParadoxModTool.Localization;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Wechselt die globale Oberflächensprache; läuft deshalb nicht parallel zu anderen Tests.</summary>
[CollectionDefinition("UiLanguage", DisableParallelization = true)]
public sealed class UiLanguageCollection;

[Collection("UiLanguage")]
public sealed class EditorPaneLanguageSortTests
{
    /// <summary>
    /// Die Sprache wirkt erst nach einem Neustart. Die Sitzung hält die Tabs in der Reihenfolge der alten Sprache (Text vor Wichtig);
    /// beim Wiederherstellen (Sortierung zuerst, dann Laden, dann manuelle Kategorie) gilt die neue (Important vor Text).
    /// </summary>
    [Fact]
    public void RestoringAfterALanguageChangeSortsByTheNewCategoryNames() => Sta.Run(() =>
    {
        var locales = FindLocales();

        // Danach gilt wieder die Sprache von vorher, nicht Deutsch. War noch nichts geladen (leere Tabelle: Rückfalltexte aus dem Code), bleibt es so:
        // dieselbe Sprache aus einem Ordner ohne Sprachdateien ergibt eine ebenso leere Tabelle.
        var previous = UiText.Current;
        var restoreFrom = ReferenceEquals(previous, UiTextTable.Empty) ? Path.Combine(locales, "_no_locales_" + Guid.NewGuid().ToString("N")) : locales;
        try
        {
            UiText.Load(locales, "de");
            var (germanText, germanImportant, germanPane) = Restore();
            Assert.Equal(new[] { germanText, germanImportant }, germanPane.Documents);

            UiText.Load(locales, "en");
            var (text, important, pane) = Restore();
            Assert.Equal("Important", important.CategoryDisplayName);
            Assert.Equal(new[] { important, text }, pane.Documents);
        }
        finally
        {
            UiText.Load(restoreFrom, previous.Language);
        }
    });

    /// <summary>Wie <c>MainWindow.RestorePane</c>: Sortierung setzen, Dateien in der gespeicherten Reihenfolge laden, dann die manuelle Kategorie.</summary>
    private static (OpenDocument Text, OpenDocument Important, EditorPane Pane) Restore()
    {
        var pane = new EditorPane { TabSort = TabSorting.Category };
        var text = pane.LoadFile(TempFiles.Write("langsort/a.txt"));
        var important = pane.LoadFile(TempFiles.Write("langsort/b.txt"));
        important.SetManualCategory(CategoryDefaults.ImportantId);
        return (text, important, pane);
    }

    private static string FindLocales()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var locales = Path.Combine(directory.FullName, "locales");
            if (File.Exists(Path.Combine(locales, "en.json")))
            {
                return locales;
            }
        }

        throw new DirectoryNotFoundException("locales");
    }
}
