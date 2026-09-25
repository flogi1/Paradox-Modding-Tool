using System.Runtime.InteropServices;

namespace ParadoxModTool.SteamIntegration;

/// <summary>
/// Die echte <see cref="ISteamworksApi"/>: lädt die <c>steam_api64.dll</c> des Spiels und ruft deren Flat-API über Funktionszeiger auf. Die Accessor-Namen (<c>SteamAPI_SteamUGC_v016</c> usw.)
/// werden zur Laufzeit gesucht. Die Bibliothek wird nach <see cref="Shutdown"/> nicht entladen (ein späteres <see cref="Init"/> lädt dieselbe Datei wieder).
/// </summary>
public sealed unsafe class SteamworksNative : ISteamworksApi
{
    private static readonly string[] UgcFunctions =
    {
        "CreateItem", "StartItemUpdate", "SetItemTitle", "SetItemDescription", "SetItemContent", "SetItemPreview", "SetItemVisibility", "SubmitItemUpdate", "GetItemUpdateProgress",
    };

    private readonly string _dllPath;
    private readonly string _appId;
    private readonly Dictionary<string, IntPtr> _functions = new();
    private IntPtr _library;
    private IntPtr _ugc;
    private IntPtr _utils;
    private bool _initialized;

    public SteamworksNative(string dllPath, string appId)
    {
        _dllPath = dllPath;
        _appId = appId;
    }

    [DllImport("ucrtbase.dll", EntryPoint = "_putenv_s", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int PutEnv(string name, string value);

    public void Init()
    {
        if (_initialized)
        {
            return;
        }

        if (!File.Exists(_dllPath))
        {
            throw new SteamworksUnavailableException("Die Steamworks-Bibliothek des Spiels wurde nicht gefunden. Ist das Spiel installiert?");
        }

        SetEnvironment("SteamAppId", _appId);
        SetEnvironment("SteamGameId", _appId);

        try
        {
            _library = NativeLibrary.Load(_dllPath);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            throw new SteamworksUnavailableException("Die Steamworks-Bibliothek konnte nicht geladen werden: " + ex.Message);
        }

        if (!CallInit(out var detail))
        {
            throw new SteamworksUnavailableException(
                "Steam läuft nicht oder du bist nicht angemeldet. Starte Steam und melde dich an." + (string.IsNullOrWhiteSpace(detail) ? string.Empty : " (" + detail + ")"));
        }

        _initialized = true;
        try
        {
            var ugcAccessor = FindAccessor("SteamAPI_SteamUGC");
            var utilsAccessor = FindAccessor("SteamAPI_SteamUtils");
            _ugc = ((delegate* unmanaged[Cdecl]<IntPtr>)ugcAccessor)();
            _utils = ((delegate* unmanaged[Cdecl]<IntPtr>)utilsAccessor)();
            if (_ugc == IntPtr.Zero || _utils == IntPtr.Zero)
            {
                throw new SteamworksUnavailableException("Steam hat die Workshop-Schnittstelle nicht bereitgestellt.");
            }

            foreach (var name in UgcFunctions)
            {
                _functions[name] = Require("SteamAPI_ISteamUGC_" + name);
            }

            _functions["RunCallbacks"] = Require("SteamAPI_RunCallbacks");
            _functions["IsAPICallCompleted"] = Require("SteamAPI_ISteamUtils_IsAPICallCompleted");
            _functions["GetAPICallResult"] = Require("SteamAPI_ISteamUtils_GetAPICallResult");
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    public void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        _initialized = false;
        if (NativeLibrary.TryGetExport(_library, "SteamAPI_Shutdown", out var shutdown))
        {
            ((delegate* unmanaged[Cdecl]<void>)shutdown)();
        }
    }

    public void RunCallbacks() => ((delegate* unmanaged[Cdecl]<void>)_functions["RunCallbacks"])();

    public ulong CreateItem(uint appId) =>
        ((delegate* unmanaged[Cdecl]<IntPtr, uint, int, ulong>)_functions["CreateItem"])(_ugc, appId, 0);

    public ulong StartItemUpdate(uint appId, ulong publishedFileId) =>
        ((delegate* unmanaged[Cdecl]<IntPtr, uint, ulong, ulong>)_functions["StartItemUpdate"])(_ugc, appId, publishedFileId);

    public bool SetItemTitle(ulong handle, string title) => CallString("SetItemTitle", handle, title);

    public bool SetItemDescription(ulong handle, string description) => CallString("SetItemDescription", handle, description);

    public bool SetItemPreview(ulong handle, string previewFile) => CallString("SetItemPreview", handle, previewFile);

    public bool SetItemContent(ulong handle, string contentFolder) => CallString("SetItemContent", handle, contentFolder);

    public bool SetItemVisibility(ulong handle, int visibility) =>
        ((delegate* unmanaged[Cdecl]<IntPtr, ulong, int, byte>)_functions["SetItemVisibility"])(_ugc, handle, visibility) != 0;

    public ulong SubmitItemUpdate(ulong handle, string changeNote)
    {
        var text = string.IsNullOrEmpty(changeNote) ? IntPtr.Zero : Marshal.StringToCoTaskMemUTF8(changeNote);
        try
        {
            return ((delegate* unmanaged[Cdecl]<IntPtr, ulong, IntPtr, ulong>)_functions["SubmitItemUpdate"])(_ugc, handle, text);
        }
        finally
        {
            if (text != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(text);
            }
        }
    }

    public bool IsCallCompleted(ulong call)
    {
        byte failed;
        return ((delegate* unmanaged[Cdecl]<IntPtr, ulong, byte*, byte>)_functions["IsAPICallCompleted"])(_utils, call, &failed) != 0;
    }

    public CreateItemResult? ReadCreateItemResult(ulong call)
    {
        var buffer = ReadResult(call, SteamworksResults.CreateItemCallbackId);
        return buffer is null ? null : SteamworksResults.ParseCreateItem(buffer);
    }

    public SubmitItemResult? ReadSubmitItemResult(ulong call)
    {
        var buffer = ReadResult(call, SteamworksResults.SubmitItemUpdateCallbackId);
        return buffer is null ? null : SteamworksResults.ParseSubmitItem(buffer);
    }

    public ItemUpdateProgress GetItemUpdateProgress(ulong handle)
    {
        ulong processed = 0;
        ulong total = 0;
        var status = ((delegate* unmanaged[Cdecl]<IntPtr, ulong, ulong*, ulong*, int>)_functions["GetItemUpdateProgress"])(_ugc, handle, &processed, &total);
        return new ItemUpdateProgress(Enum.IsDefined(typeof(ItemUpdateStatus), status) ? (ItemUpdateStatus)status : ItemUpdateStatus.Invalid, processed, total);
    }

    public void Dispose() => Shutdown();

    private byte[]? ReadResult(ulong call, int callbackId)
    {
        var buffer = new byte[SteamworksResults.BufferSize];
        byte failed;
        byte ok;
        fixed (byte* pointer = buffer)
        {
            ok = ((delegate* unmanaged[Cdecl]<IntPtr, ulong, void*, int, int, byte*, byte>)_functions["GetAPICallResult"])(
                _utils, call, pointer, buffer.Length, callbackId, &failed);
        }

        return ok != 0 && failed == 0 ? buffer : null;
    }

    private bool CallString(string function, ulong handle, string value)
    {
        var text = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return ((delegate* unmanaged[Cdecl]<IntPtr, ulong, IntPtr, byte>)_functions[function])(_ugc, handle, text) != 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(text);
        }
    }

    private bool CallInit(out string? detail)
    {
        detail = null;
        if (NativeLibrary.TryGetExport(_library, "SteamAPI_InitFlat", out var flat))
        {
            var message = stackalloc byte[1024];
            var code = ((delegate* unmanaged[Cdecl]<byte*, int>)flat)(message);
            if (code != 0)
            {
                detail = Marshal.PtrToStringUTF8((IntPtr)message);
            }

            return code == 0;
        }

        foreach (var name in new[] { "SteamAPI_Init", "SteamAPI_InitSafe" })
        {
            if (NativeLibrary.TryGetExport(_library, name, out var init))
            {
                return ((delegate* unmanaged[Cdecl]<byte>)init)() != 0;
            }
        }

        throw new SteamworksUnavailableException("Die Steamworks-Bibliothek dieser Version bietet SteamAPI_Init nicht an.");
    }

    private IntPtr FindAccessor(string prefix)
    {
        var name = AccessorSearch.Find(prefix, candidate => NativeLibrary.TryGetExport(_library, candidate, out _));
        return name is null
            ? throw new SteamworksUnavailableException($"Die Steamworks-Bibliothek dieser Version bietet {prefix}_v### nicht an.")
            : NativeLibrary.GetExport(_library, name);
    }

    private IntPtr Require(string name) =>
        NativeLibrary.TryGetExport(_library, name, out var pointer)
            ? pointer
            : throw new SteamworksUnavailableException($"Die Steamworks-Bibliothek dieser Version bietet {name} nicht an.");

    private static void SetEnvironment(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        try
        {
            PutEnv(name, value);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Ohne die Laufzeitbibliothek bleibt nur die Windows-Umgebungsvariable.
        }
    }
}
