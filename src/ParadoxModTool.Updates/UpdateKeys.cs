namespace ParadoxModTool.Updates;

/// <summary>
/// Öffentlicher Schlüssel, mit dem PMT Update-Pakete prüft. Wird einmal mit <c>ParadoxModTool.ReleaseSigner keygen</c> erzeugt und hier
/// eingetragen. Solange leer, bietet PMT kein Selbst-Update an, nur die Release-Seite.
/// </summary>
public static class UpdateKeys
{
    public const string ProductionPublicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEQoh3NBPQJMeC1yATyH7fl/8ilL/ZOBHbnO5mq7MfZ9am/0EO18Ru4axAy8j/07gJ/Gcac14UPmeIsCkx37txZQ==";

    public static bool HasProductionKey => ProductionPublicKey.Length > 0;
}
