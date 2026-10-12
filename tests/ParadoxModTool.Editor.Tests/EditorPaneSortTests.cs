using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Categories;
using ParadoxModTool.Settings;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class EditorPaneSortTests
{
    [Fact]
    public void SettingNameSortReordersTabsAndKeepsTheActiveOne() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        var b = pane.LoadFile(TempFiles.Write("sort1/b.txt"));
        var a = pane.LoadFile(TempFiles.Write("sort1/a.txt"));
        pane.ActivateDocument(b);
        var raised = 0;
        pane.TabSortChanged += (_, _) => raised++;

        pane.TabSort = TabSorting.Name;

        Assert.Equal(new[] { a, b }, pane.Documents);
        Assert.Same(b, pane.ActiveDocument);
        Assert.Equal(1, raised);
    });

    [Fact]
    public void NewDocumentsInsertSortedWhileNameSortIsActive() => Sta.Run(() =>
    {
        var pane = new EditorPane { TabSort = TabSorting.Name };
        var c = pane.LoadFile(TempFiles.Write("sort2/c.txt"));
        var a = pane.LoadFile(TempFiles.Write("sort2/a.txt"));
        var b = pane.LoadFile(TempFiles.Write("sort2/b.txt"));
        Assert.Equal(new[] { a, b, c }, pane.Documents);

        var other = new EditorPane();
        var ab = other.LoadFile(TempFiles.Write("sort2/ab.txt"));
        other.ReleaseDocument(ab);
        pane.AdoptDocument(ab, insertBefore: c);
        Assert.Equal(new[] { a, ab, b, c }, pane.Documents);
        Assert.Equal(TabSorting.Name, pane.TabSort);
    });

    [Fact]
    public void DraggingATabSwitchesBackToManual() => Sta.Run(() =>
    {
        var pane = new EditorPane { TabSort = TabSorting.Name };
        pane.LoadFile(TempFiles.Write("sort3/a.txt"));
        pane.LoadFile(TempFiles.Write("sort3/b.txt"));

        pane.MoveDocument(pane.Documents[1], before: pane.Documents[0]);

        Assert.Equal(TabSorting.Manual, pane.TabSort);
        Assert.Equal(new[] { "b.txt", "a.txt" }, pane.Documents.Select(d => d.DisplayName));
    });

    [Fact]
    public void DroppingATabWithoutMovingItKeepsTheSort() => Sta.Run(() =>
    {
        var pane = new EditorPane { TabSort = TabSorting.Name };
        var a = pane.LoadFile(TempFiles.Write("sort4/a.txt"));
        var b = pane.LoadFile(TempFiles.Write("sort4/b.txt"));
        var raised = 0;
        pane.TabSortChanged += (_, _) => raised++;

        // Auf sich selbst abgelegt, und der letzte Tab auf der freien Fläche dahinter (= ans Ende).
        pane.MoveDocument(a, before: a);
        pane.MoveDocument(b, before: null);

        Assert.Equal(TabSorting.Name, pane.TabSort);
        Assert.Equal(0, raised);
        Assert.Equal(new[] { a, b }, pane.Documents);
    });

    [Fact]
    public void SortMenuListsFourChoicesAndAppliesThem() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        var items = pane.BuildSortMenu().Items.OfType<MenuItem>().ToList();
        Assert.Equal(new[] { TabSorting.Manual, TabSorting.Name, TabSorting.Category, TabSorting.Origin }, items.Select(i => (string)i.Tag));
        Assert.Equal(
            new[] { MenuTexts.WorkspaceSortManual, MenuTexts.WorkspaceSortName, MenuTexts.WorkspaceSortCategory, MenuTexts.WorkspaceSortOrigin },
            items.Select(i => (string)i.Header));
        Assert.True(items[0].IsChecked);

        items[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, items[2]));

        Assert.Equal(TabSorting.Category, pane.TabSort);
        Assert.Equal(MenuTexts.WorkspaceSort, ((Button)pane.FindName("SortButton")).Content);
    });

    [Fact]
    public void RenamingReSortsByNameAndKeepsTheActiveTab() => Sta.Run(() =>
    {
        var pane = new EditorPane { TabSort = TabSorting.Name };
        var a = pane.LoadFile(TempFiles.Write("sort5/a.txt"));
        var b = pane.LoadFile(TempFiles.Write("sort5/b.txt"));
        var c = pane.LoadFile(TempFiles.Write("sort5/c.txt"));
        pane.ActivateDocument(a);
        a.Document.Insert(0, "x");

        // "Speichern unter…" mit neuem Namen.
        pane.SetCurrentFilePath(TempFiles.Write("sort5/z.txt"));

        Assert.Equal(new[] { b, c, a }, pane.Documents);
        Assert.Same(a, pane.ActiveDocument);
        Assert.False(a.IsModified);
    });

    [Fact]
    public void ManualCategoryChangeReSortsByCategory() => Sta.Run(() =>
    {
        var pane = new EditorPane { TabSort = TabSorting.Category };
        var a = pane.LoadFile(TempFiles.Write("sort6/a.txt"));
        var b = pane.LoadFile(TempFiles.Write("sort6/b.txt"));
        pane.ActivateDocument(a);
        Assert.Equal(new[] { a, b }, pane.Documents);
        var earlier = FileCategoryClassifier.Categories.Select(c => c.Id)
            .First(id => StringComparer.CurrentCultureIgnoreCase.Compare(FileCategoryClassifier.DisplayName(id), a.CategoryDisplayName) < 0);

        b.SetManualCategory(earlier);

        Assert.Equal(new[] { b, a }, pane.Documents);
        Assert.Same(a, pane.ActiveDocument);
    });

    [Fact]
    public void OriginChangeReSortsByOrigin() => Sta.Run(() =>
    {
        var pane = new EditorPane { TabSort = TabSorting.Origin };
        var a = pane.LoadFile(TempFiles.Write("sort7/a.txt"));
        var b = pane.LoadFile(TempFiles.Write("sort7/game/b.txt"));
        pane.ActivateDocument(a);
        Assert.Equal(new[] { a, b }, pane.Documents);

        // FileOriginTracker (TabIconSettings.cs) ist prozessweit: Wurzeln setzen, nach dem Test leeren.
        FileOriginTracker.SetRoots(new[] { Path.Combine(TempFiles.Root, "sort7", "game") }, Array.Empty<string>());
        try
        {
            b.RefreshOrigin();

            Assert.Equal(new[] { b, a }, pane.Documents);
            Assert.Same(a, pane.ActiveDocument);
        }
        finally
        {
            FileOriginTracker.SetRoots(Array.Empty<string>(), Array.Empty<string>());
        }
    });

    [Fact]
    public void RefreshingAllOriginsSortsOnceAfterwards() => Sta.Run(() =>
    {
        var pane = new EditorPane { TabSort = TabSorting.Origin };
        var a = pane.LoadFile(TempFiles.Write("sort8/a.txt"));
        var b = pane.LoadFile(TempFiles.Write("sort8/game/b.txt"));
        var c = pane.LoadFile(TempFiles.Write("sort8/mod/c.txt"));
        pane.ActivateDocument(a);
        Assert.Equal(new[] { a, b, c }, pane.Documents);

        FileOriginTracker.SetRoots(new[] { Path.Combine(TempFiles.Root, "sort8", "game") }, new[] { Path.Combine(TempFiles.Root, "sort8", "mod") });
        try
        {
            // Ein Umsortieren je Dokument würde die Schleife über die Tabs mitten im Durchlauf verändern.
            pane.RefreshOrigins();

            Assert.Equal(new[] { b, c, a }, pane.Documents);
            Assert.Same(a, pane.ActiveDocument);
        }
        finally
        {
            FileOriginTracker.SetRoots(Array.Empty<string>(), Array.Empty<string>());
        }
    });

    [Fact]
    public void RefreshingAllAutoCategoriesSortsOnceAfterwards() => Sta.Run(() =>
    {
        var pane = new EditorPane { TabSort = TabSorting.Category };
        var a = pane.LoadFile(TempFiles.Write("sort9/a.aaa"));
        var b = pane.LoadFile(TempFiles.Write("sort9/b.bbb"));
        var c = pane.LoadFile(TempFiles.Write("sort9/c.ccc"));
        Assert.Equal(new[] { a, b, c }, pane.Documents);

        // Die Kategorie-Liste ist prozessweit: eigene Kategorie davor, nach dem Test die Standardliste zurück.
        FileCategoryClassifier.SetCategories(FileCategoryClassifier.Categories.Prepend(
            new CategoryDefinition { Id = "c-sorttest", Name = "0 Sorttest", Extensions = new() { ".bbb", ".ccc" } }));
        try
        {
            pane.RefreshAutoCategories();

            Assert.Equal(new[] { b, c, a }, pane.Documents);
        }
        finally
        {
            FileCategoryClassifier.SetCategories(null);
        }
    });

    [Fact]
    public void DirtyAndMarkChangesDoNotReorder() => Sta.Run(() =>
    {
        var pane = new EditorPane { TabSort = TabSorting.Name };
        var a = pane.LoadFile(TempFiles.Write("sort10/a.txt"));
        pane.LoadFile(TempFiles.Write("sort10/b.txt"));
        var changes = 0;
        ((INotifyCollectionChanged)pane.Documents).CollectionChanged += (_, _) => changes++;

        a.Document.Insert(0, "x");
        a.SetHasNote(true);

        Assert.Equal(0, changes);
    });

    [Fact]
    public void ManualSortIgnoresKeyChanges() => Sta.Run(() =>
    {
        var pane = new EditorPane();
        var b = pane.LoadFile(TempFiles.Write("sort11/b.txt"));
        var a = pane.LoadFile(TempFiles.Write("sort11/a.txt"));
        pane.ActivateDocument(b);

        pane.SetCurrentFilePath(TempFiles.Write("sort11/z.txt"));

        Assert.Equal(new[] { b, a }, pane.Documents);
    });
}
