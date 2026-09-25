namespace ParadoxModTool.SteamIntegration;

/// <summary>Die Wege, auf denen hochgeladen werden kann; der gewählte Weg steht in <see cref="SteamCmdSettings.UploadMethod"/>.</summary>
public static class UploadMethods
{
    public const string Client = "client";
    public const string SteamCmd = "steamcmd";

    /// <summary>Der gespeicherte Weg, sonst der Steam-Client, wenn er benutzbar ist, sonst SteamCMD.</summary>
    public static string Resolve(string? saved, bool clientAvailable) =>
        saved is Client or SteamCmd ? saved : clientAvailable ? Client : SteamCmd;
}
