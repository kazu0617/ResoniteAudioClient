using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace AudioClient.Core;

public static class NativeLibraryResolver
{
    private static string[] _searchDirs = Array.Empty<string>();
    private static bool _hooked;

    public static void Register(IEnumerable<string> probeDirs)
    {
        var dirs = new List<string>();
        foreach (string probe in probeDirs)
        {
            foreach (string rid in GetNativeRids())
            {
                string path = Path.Combine(probe, "runtimes", rid, "native");
                if (Directory.Exists(path) && !dirs.Contains(path))
                    dirs.Add(path);
            }
        }
        _searchDirs = dirs.ToArray();
        if (_searchDirs.Length == 0) return;

        if (!_hooked)
        {
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += OnResolvingUnmanagedDll;
            _hooked = true;
        }
    }

    private static IntPtr OnResolvingUnmanagedDll(Assembly _, string libraryName)
    {
        foreach (string dir in _searchDirs)
        {
            foreach (string candidate in EnumerateCandidates(libraryName))
            {
                string full = Path.Combine(dir, candidate);
                if (File.Exists(full) && NativeLibrary.TryLoad(full, out IntPtr handle))
                    return handle;
            }
        }
        return IntPtr.Zero;
    }

    private static IEnumerable<string> EnumerateCandidates(string libraryName)
    {
        yield return libraryName;

        if (OperatingSystem.IsWindows())
        {
            if (!libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                yield return libraryName + ".dll";
        }
        else if (OperatingSystem.IsLinux())
        {
            if (!libraryName.StartsWith("lib", StringComparison.Ordinal))
                yield return "lib" + libraryName + ".so";
            if (!libraryName.EndsWith(".so", StringComparison.Ordinal))
                yield return libraryName + ".so";
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (!libraryName.StartsWith("lib", StringComparison.Ordinal))
                yield return "lib" + libraryName + ".dylib";
            if (!libraryName.EndsWith(".dylib", StringComparison.Ordinal))
                yield return libraryName + ".dylib";
        }
    }

    private static IEnumerable<string> GetNativeRids()
    {
        if (OperatingSystem.IsWindows()) yield return "win-x64";
        else if (OperatingSystem.IsLinux()) yield return "linux-x64";
        else if (OperatingSystem.IsMacOS()) yield return "osx-x64";
    }
}
