namespace ParadoxModTool.SteamIntegration;

/// <summary>Texte für die <c>EResult</c>-Codes, die beim Workshop-Upload vorkommen (siehe <see cref="SteamTexts"/>).</summary>
public static class SteamResultText
{
    public static string Describe(int result) => result switch
    {
        2 => SteamTexts.ResultGeneric,
        3 => SteamTexts.ResultNoConnection,
        8 => SteamTexts.ResultInvalidParam,
        9 => SteamTexts.ResultFileNotFound,
        10 => SteamTexts.ResultBusy,
        15 => SteamTexts.ResultAccessDenied,
        16 => SteamTexts.ResultTimeout,
        25 => SteamTexts.ResultLimit,
        _ => SteamTexts.ResultOther(result),
    };
}
