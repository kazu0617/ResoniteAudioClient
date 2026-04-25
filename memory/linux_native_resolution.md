---
name: Linux で runtimes/{rid}/native/ のネイティブを解決する仕組み
description: .NET の DllImport は Linux で runtimes/{rid}/native/ を自動探索しないため、AssemblyLoadContext.ResolvingUnmanagedDll でフォールバック解決する
type: project
---

## 事実

.NET (Linux) の DllImport は `AppContext.BaseDirectory` と `LD_LIBRARY_PATH` 系を見るが、`runtimes/{rid}/native/` 配下は deps.json で明示されていないと自動探索しない。Avalonia / SkiaSharp 等の native は appDir 直下から見つかるが、Resonite の `runtimes/linux-x64/native/` (libphonon.so 等) は届かないため `DllNotFoundException` になる。

**Why:** .NET の native lib 検索順序は基本的に各アセンブリの隣接ディレクトリ + `LD_LIBRARY_PATH`。サブフォルダ runtime 検索は deps.json の `nativeLibrary` エントリ経由でしか発生しない。

## 解決方法

`AudioClient.Core/NativeLibraryResolver` を `AssemblyLoadContext.Default.ResolvingUnmanagedDll` に登録する。これは標準解決失敗時の**フォールバック**として動くため、SharpFont のようにアセンブリ自身が `SetDllImportResolver` を持つ場合と非破壊的に共存できる。

**How to apply:** appDir / engineDir それぞれの `runtimes/{rid}/native/` を `_searchDirs` に積み、未解決ライブラリ名に対して `lib*.so` / `*.so` のリネーム候補も試す。

```csharp
AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, libName) =>
{
    foreach (var dir in _searchDirs)
        foreach (var candidate in EnumerateCandidates(libName))
            if (NativeLibrary.TryLoad(Path.Combine(dir, candidate), out var h))
                return h;
    return IntPtr.Zero;
};
```

## Linux で PATH 操作は無意味

`Environment.SetEnvironmentVariable("PATH", ...)` は Windows では DLL 解決に効くが、Linux の `dlopen` は `LD_LIBRARY_PATH` を見る。プロセス起動後の `LD_LIBRARY_PATH` 変更は glibc が必ずしも拾わない (起動時にキャッシュされている) ため、resolver で名前解決する方が確実。

## アセンブリ単位 resolver と ResolvingUnmanagedDll の使い分け

- `NativeLibrary.SetDllImportResolver(asm, resolver)`: そのアセンブリの DllImport の標準解決を**完全に置き換える**。同一アセンブリに 2 つは登録不可 (`InvalidOperationException`)。
- `AssemblyLoadContext.Default.ResolvingUnmanagedDll`: 標準解決が失敗したときの**フォールバック**。複数ハンドラ可能、共存しやすい。

横断的なフォールバックは後者、特定アセンブリの redirect は前者を使う。
