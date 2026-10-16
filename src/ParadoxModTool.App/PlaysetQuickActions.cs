using ParadoxModTool.Mods;

namespace ParadoxModTool.App;

/// <summary>Schnellstart mit einer Testmod aus der Mod-Liste (Rechtsklick): <see cref="CanStart"/> sagt, ob das Spiel aus PMT startbar ist.</summary>
/// <param name="CanStart">Ob das Spiel mit einem Playset aus PMT gestartet werden kann.</param>
/// <param name="StartWith">Mit dem gewählten Playset der Werkzeugleiste plus dieser Mod starten.</param>
/// <param name="NewFrom">Ein neues Playset aus dem gewählten plus dieser Mod anlegen und anbieten, damit zu starten.</param>
public sealed record PlaysetQuickActions(Func<bool> CanStart, Action<ModInfo> StartWith, Action<ModInfo> NewFrom);
