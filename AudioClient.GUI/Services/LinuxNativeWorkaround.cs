using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AudioClient.GUI.Services;

internal static class LinuxNativeWorkaround
{
    private static bool _hbRedirected;

    // Bundled libHarfBuzzSharp.so exports hb_* symbols with a custom version tag
    // ("@@libHarfBuzzSharp"). On distros with very recent libfreetype.so.6 (e.g. Arch),
    // the system freetype expects unversioned hb_* symbols, and the version mismatch
    // triggers a fatal symbol lookup error during font init. Redirect HarfBuzzSharp's
    // DllImport calls to the system libharfbuzz.so.0, which exports unversioned
    // symbols compatible with system libfreetype.
    public static void RedirectHarfBuzzSharpToSystem(string appDir)
    {
        if (_hbRedirected) return;
        if (!OperatingSystem.IsLinux()) return;

        string dllPath = Path.Combine(appDir, "HarfBuzzSharp.dll");
        if (!File.Exists(dllPath)) return;

        Assembly hbsharp;
        try { hbsharp = Assembly.LoadFrom(dllPath); }
        catch { return; }

        try
        {
            NativeLibrary.SetDllImportResolver(hbsharp, (libName, _, _) =>
            {
                if (string.Equals(libName, "libHarfBuzzSharp", StringComparison.Ordinal))
                {
                    if (NativeLibrary.TryLoad("libharfbuzz.so.0", out IntPtr h))
                        return h;
                }
                return IntPtr.Zero;
            });
            _hbRedirected = true;
        }
        catch (InvalidOperationException) { }
        catch (ArgumentException) { }
    }
}
