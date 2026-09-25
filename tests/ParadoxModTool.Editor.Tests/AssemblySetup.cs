using Xunit;

// Die Editor-Einstellungen (GuiSettings, TabIconSettings, ...) sind prozessweit statisch: Tests laufen nacheinander.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
