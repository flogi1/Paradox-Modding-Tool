namespace ParadoxModTool.Generators;

/// <summary>
/// Pfadschutz (Spec Teil 5a): ein Pfad aus einem Paket (gerenderter Ausgabepfad, Asset-Pfad, ZIP-Eintrag) muss relativ sein und darf den
/// Zielordner nicht verlassen: nicht leer, kein Laufwerk oder <c>:</c>, kein führendes <c>/</c> oder <c>\</c>, kein Teil <c>..</c>.
/// </summary>
public static class SafeRelativePath
{
    public static bool IsSafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains(':') || path[0] is '/' or '\\')
        {
            return false;
        }

        return path.Split('/', '\\').All(part => part != "..");
    }
}
