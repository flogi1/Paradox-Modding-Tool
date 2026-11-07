namespace ParadoxModTool.FlagTracker;

/// <summary>Art des Namens, der gesetzt, gelesen oder entfernt wird.</summary>
public enum FlagKind
{
    Variable,
    GlobalVariable,
    LocalVariable,
    VariableList,
    GlobalVariableList,
    LocalVariableList,
    ScopeFlag,
    DefinitionFlag,
    SymbolValue,

    /// <summary>Event-Target (HOI IV, EU IV, Stellaris): <c>save_event_target_as</c>, <c>event_target:X</c>.</summary>
    EventTarget,

    /// <summary>Globales Event-Target: <c>save_global_event_target_as</c>.</summary>
    GlobalEventTarget,

    /// <summary>Temporäre Variable (HOI IV).</summary>
    TempVariable,

    /// <summary>Array (HOI IV).</summary>
    Array,

    /// <summary>Temporäres Array (HOI IV).</summary>
    TempArray,
}

/// <summary>Rolle der Fundstelle: setzt, liest oder entfernt den Namen.</summary>
public enum FlagRole
{
    Set,
    Read,
    Remove,
}

/// <summary>Herkunft einer Fundstelle: aktive Mod, andere Mod oder Vanilla.</summary>
public enum FlagOrigin
{
    ActiveMod,
    OtherMod,
    Vanilla,
}

/// <summary>
/// Ort einer Fundstelle. RootDirectory ist absolut, RelativePath nutzt "/" ohne führenden Slash,
/// Line zählt ab 1.
/// </summary>
public sealed record SiteLocation(string RootDirectory, string RelativePath, int Line)
{
    /// <summary>Voller Pfad aus Wurzel und relativem Pfad mit den Trennzeichen des Betriebssystems.</summary>
    public string FullPath => Path.Combine(RootDirectory, RelativePath.Replace('/', Path.DirectorySeparatorChar));
}

/// <summary>
/// Eine Fundstelle eines Flag- oder Variablennamens.
/// Family: bei ScopeFlag die Befehlsfamilie, z. B. "character_flag", sonst null.
/// Via: bei über $PARAM$ aufgelösten Stellen die Zeile im Scripted Effect bzw. Trigger; ViaName ist dessen Name.
/// IsPattern: Name enthält '*' für einen nicht aufgelösten Teil.
/// OriginName ist der Anzeigename der Mod oder "Vanilla".
/// </summary>
public sealed record FlagSite(
    string Name,
    FlagKind Kind,
    string? Family,
    FlagRole Role,
    SiteLocation Location,
    FlagOrigin Origin,
    string OriginName,
    SiteLocation? Via = null,
    string? ViaName = null,
    bool IsPattern = false);
