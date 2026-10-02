using System.IO;
using System.Text;
using ParadoxModTool.SteamIntegration;
using ParadoxModTool.Updates;

namespace ParadoxModTool.App;

/// <summary>
/// Der Einstiegspunkt. Mit <see cref="SteamUploadProtocol.Argument"/> startet das Programm ohne Oberfläche als kurzlebiger Hilfsprozess für den Upload über den Steam-Client
/// (siehe <see cref="SteamUploadHelper"/>); sonst läuft die normale Anwendung. Mit <c>--apply-update</c> läuft der Update-Modus
/// (siehe <see cref="UpdateApplyMode"/>).
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == ApplyUpdateArguments.Switch)
        {
            return UpdateApplyMode.Run(args);
        }

        if (args.Length >= 2 && args[0] == SteamUploadProtocol.Argument)
        {
            return RunSteamUploadHelper(args[1]);
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    private static int RunSteamUploadHelper(string requestFile)
    {
        // Der Hilfsprozess startet ohne App.OnStartup: die Meldungen, die er zurückgibt, wären sonst immer deutsch.
        GlobalUiLanguage.Load(ParadoxModTool.Session.SessionRepository.CreateDefault());

        using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
        using var input = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        return SteamUploadHelper
            .RunAsync(requestFile, request => new SteamworksNative(request.DllPath, request.AppId), output, input, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
