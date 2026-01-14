using System.Runtime.InteropServices;

namespace PingPlugin;

public static partial class WineDetector
{
    public static bool IsWINE()
    {
        var ntdll = GetModuleHandle("ntdll.dll");
        if (ntdll == nint.Zero)
        {
            return false;
        }

        var wineGetVersion = GetProcAddress(ntdll, "wine_get_version");
        return wineGetVersion != nint.Zero;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandle(string lpModuleName);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetProcAddress(nint hModule, string procName);
}