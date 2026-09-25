using Xunit;

// Die Editor-Einstellungen (ScopeSettings, GuiSettings, ...) sind prozessweit statisch: Tests laufen nacheinander.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
