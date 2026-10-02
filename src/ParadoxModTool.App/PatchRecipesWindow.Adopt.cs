using System.Windows;
using ParadoxModTool.Patches;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// "Bestehende Mod-Gebäude übernehmen" (Spec 2026-09-30-patch-recipes, Etappe 5): Zuordnung wählen, Warnung zu Spielständen, Umbenennung als Plan im
/// Reiter "Vorschau"; nach erfolgreichem Anwenden kommen die umbenannten Specials mit Ort als Zeilen ins Rezept.
/// </summary>
public partial class PatchRecipesWindow
{
    private Button? _adoptModButton;

    /// <summary>Gesetzt, solange der Reiter "Vorschau" den Plan einer Umbenennung zeigt; <see cref="SetPlan"/> verwirft ihn.</summary>
    private PendingAdoption? _pendingAdoption;

    private sealed record PendingAdoption(IReadOnlyList<string> NewKeys);

    private async void AdoptModBuildings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await AdoptModBuildingsAsync();
        }
        catch (Exception ex)
        {
            // async-void-Handler: kein Fehler darf bis zum Dispatcher durchschlagen und PMT beenden.
            SetStatus(ex.Message);
        }
    }

    private async Task AdoptModBuildingsAsync()
    {
        if (Recipe is not { } recipe || Context() is not { } context)
        {
            SetStatus(ContextMissingText);
            return;
        }

        var dialog = new ModBuildingsAdoptDialog(context.ModRoot, context.VanillaRoot) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Selected is not { Count: > 0 } renames)
        {
            return;
        }

        if (MessageBox.Show(this, PatchRecipeTexts.AdoptModSaveWarning(renames.Count), PatchRecipeTexts.AdoptModButton, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        SaveNow();
        var copy = PatchRecipeStore.Clone(recipe);
        var wasComments = dialog.WasComments;
        var plan = await Task.Run(() => KeyRenamer.Plan(context, copy, renames, SpecialBuildingsPreset.BuildingsFolder, wasComments));
        if (!ReferenceEquals(recipe, Recipe))
        {
            return;
        }

        SetPlan(plan);
        _pendingAdoption = new PendingAdoption(renames.Select(r => r.NewKey).ToList());
        Tabs.SelectedItem = PreviewTab;
        SetStatus(PatchRecipeTexts.AdoptModPlanReady(renames.Count));
    }

    /// <summary>Nach dem Schreiben der Umbenennung: passende Specials als Zeilen ins Rezept, speichern, dann den Rezeptlauf als neue Vorschau.</summary>
    private async Task AfterRenameAppliedAsync(PendingAdoption pending)
    {
        if (Recipe is not { } recipe || Context() is not { } context)
        {
            return;
        }

        IReadOnlyList<PatchRow> rows;
        try
        {
            var copy = PatchRecipeStore.Clone(recipe);
            rows = await Task.Run(() => SpecialBuildingsPreset.RowsToAdopt(copy, new PatchWorkspace(context.ModRoot, context.VanillaRoot), pending.NewKeys));
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            return;
        }

        if (!ReferenceEquals(recipe, Recipe))
        {
            return;
        }

        recipe.Rows.AddRange(rows);
        MarkRecipeEdited();
        RebuildTable();
        SaveNow();
        await ComputePreviewAsync();
        SetStatus(PatchRecipeTexts.AdoptModDone(pending.NewKeys.Count, rows.Count));
    }
}
