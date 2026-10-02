using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ParadoxModTool.Storage;

namespace ParadoxModTool.BugTracking;

/// <summary>
/// Der Discord-Bot-Token, verschlüsselt mit Windows-DPAPI (nur dieses Windows-Konto kann ihn lesen) in <c>%APPDATA%\ParadoxModTool\discord-token.dat</c>.
/// Ein Token für alle Mods und Spiele. Ist die Datei nicht lesbar (beschädigt, anderes Konto), gilt: kein Token.
/// </summary>
public static class DiscordTokenStore
{
    public const string FileName = "discord-token.dat";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ParadoxModTool.DiscordBotToken");

    public static string GetDefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParadoxModTool", FileName);

    public static void Save(string path, string token)
    {
        var protectedBytes = Dpapi.Protect(Encoding.UTF8.GetBytes(token.Trim()), Entropy);
        SharedFile.WriteAllTextAtomic(path, Convert.ToBase64String(protectedBytes));
    }

    public static string? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var token = Encoding.UTF8.GetString(Dpapi.Unprotect(Convert.FromBase64String(File.ReadAllText(path).Trim()), Entropy));
            return token.Length == 0 ? null : token;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>DPAPI über <c>crypt32</c> (keine NuGet-Abhängigkeit). Fehler als <see cref="CryptographicException"/>.</summary>
    private static class Dpapi
    {
        private const int UiForbidden = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Size;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob input, string? description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob output);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob output);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        public static byte[] Protect(byte[] data, byte[] entropy) => Run(data, entropy, protect: true);

        public static byte[] Unprotect(byte[] data, byte[] entropy) => Run(data, entropy, protect: false);

        private static byte[] Run(byte[] data, byte[] entropy, bool protect)
        {
            var input = ToBlob(data);
            var extra = ToBlob(entropy);
            var output = default(DataBlob);
            try
            {
                var ok = protect
                    ? CryptProtectData(ref input, null, ref extra, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, ref extra, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output);
                if (!ok)
                {
                    throw new CryptographicException(Marshal.GetLastWin32Error());
                }

                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(input.Data);
                Marshal.FreeHGlobal(extra.Data);
                if (output.Data != IntPtr.Zero)
                {
                    LocalFree(output.Data);
                }
            }
        }

        private static DataBlob ToBlob(byte[] bytes)
        {
            var blob = new DataBlob { Size = bytes.Length, Data = Marshal.AllocHGlobal(Math.Max(1, bytes.Length)) };
            Marshal.Copy(bytes, 0, blob.Data, bytes.Length);
            return blob;
        }
    }
}
