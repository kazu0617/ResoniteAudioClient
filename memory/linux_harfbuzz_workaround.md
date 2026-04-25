---
name: Linux で bundled libHarfBuzzSharp のシンボル version 衝突を回避する
description: Avalonia がバンドルする libHarfBuzzSharp.so は @@libHarfBuzzSharp 付きシンボルを export しており、最新の libfreetype と衝突して fatal abort する。HarfBuzzSharp.dll の DllImport をシステム libharfbuzz.so.0 へ redirect して回避
type: project
---

## 事実

Avalonia がバンドルする `libHarfBuzzSharp.so` は `hb_*` シンボルを `@@libHarfBuzzSharp` というカスタム version 付きで export している。Arch のような新しめの `libfreetype.so.6` は unversioned `hb_*` を期待するため、同一プロセスにロードされた瞬間にダイナミックリンカが `undefined symbol: hb_version_atleast (fatal)` で abort する。

**Why:** SkiaSharp.NativeAssets.Linux パッケージは独自 build の HarfBuzz をバンドルしているが、シンボル衝突を避けるため version を付けて隔離している。一方システム libfreetype は近年 HarfBuzz を弱参照で使うようになり、グローバルスコープに versioned だけ存在すると弱参照解決が失敗する。

## 症状

Avalonia の `StartWithClassicDesktopLifetime` 内で stderr に何も出さずに segfault (exit 139)。`LD_DEBUG=libs dotnet AudioClient.GUI.dll` で確認すると最後の行に `/usr/lib/libfreetype.so.6: error: symbol lookup error: undefined symbol: hb_version_atleast (fatal)`。

## 回避策

`HarfBuzzSharp.dll` の `[DllImport("libHarfBuzzSharp")]` をシステム `libharfbuzz.so.0` に redirect する。アセンブリ単位の `NativeLibrary.SetDllImportResolver` を使うので他のネイティブには影響しない。

**How to apply:** `AudioClient.GUI/Services/LinuxNativeWorkaround.cs` の `RedirectHarfBuzzSharpToSystem` を `RuntimeBootstrap.PrimeAssemblyLoading` から呼ぶ。Linux 以外では冒頭ガードで no-op。

```csharp
NativeLibrary.SetDllImportResolver(harfBuzzSharpAsm, (libName, _, _) =>
{
    if (libName == "libHarfBuzzSharp"
        && NativeLibrary.TryLoad("libharfbuzz.so.0", out IntPtr h))
        return h;
    return IntPtr.Zero;
});
```

## やってはいけない

- `LD_PRELOAD=libharfbuzz.so.0` で先読みすると、別の `free(): invalid pointer` (heap corruption) で死ぬ。バンドル HarfBuzz とシステム HarfBuzz が同一プロセスに同居するパスはどれも壊れる。
- `libHarfBuzzSharp.so` を削除すると Avalonia の Skia フォントマネージャ初期化で `DllNotFoundException`。Avalonia.Skia は HarfBuzzSharp に直接依存している。
- `dotnet publish --self-contained` への切り替えでは解決しない (バンドル HarfBuzz が同梱されるだけ)。
