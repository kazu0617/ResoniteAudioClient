# GUI 固有メモ

- Avalonia 11.2.7 の `TextBox` は Windows の日本語 IME 変換中に `Text` バインディングを逐次 ViewModel へ反映すると、確定時に文字が重複することがある。GUI の入力欄は `UpdateSourceTrigger=LostFocus` を基本にし、Enter で即時処理したい箇所だけ `ImeAwareTextBox.FlushTextBindingToSource()` で明示同期する。
- IME 変換確定に Enter を使う欄では、送信や検索実行の前に `ImeAwareTextBox.HasActiveImeComposition` を見て、変換中の Enter をアプリ側ショートカットとして扱わないこと。
- `GuiSettingsStore.Save` はファイル全体を書き換えるので、GUI 設定を追加するときは既存設定を `Load()` して未変更の項目を保持したまま保存すること

## Linux 環境での留意点

### HarfBuzzSharp ネイティブのシンボル version 衝突
Avalonia がバンドルする `libHarfBuzzSharp.so` は `hb_*` シンボルを `@@libHarfBuzzSharp` というカスタム version 付きで export している。Arch Linux など新しめの `libfreetype.so.6` (= システム HarfBuzz の unversioned シンボルを期待) と同一プロセスにロードされると、フォント初期化時に `undefined symbol: hb_version_atleast` で fatal abort する。

対処として `Services/LinuxNativeWorkaround.cs` の `RedirectHarfBuzzSharpToSystem` で `HarfBuzzSharp.dll` の `[DllImport("libHarfBuzzSharp")]` をシステム `libharfbuzz.so.0` に redirect している。`RuntimeBootstrap.PrimeAssemblyLoading` から呼ばれる。

### `runtimes/{rid}/native/` のネイティブ解決
Linux の DllImport は `runtimes/{rid}/native/` を自動探索しない (deps.json 経由でないと届かない)。`AudioClient.Core/NativeLibraryResolver` を `AssemblyLoadContext.Default.ResolvingUnmanagedDll` に登録し、appDir/engineDir 配下の `runtimes/linux-x64/native/` をフォールバック探索している。`SetDllImportResolver` で自前 resolver を登録する SharpFont のようなアセンブリと共存できるよう、アセンブリ単位ではなくコンテキストイベントを使うのがポイント。

### Avalonia / Skia の起動デバッグ
`StartWithClassicDesktopLifetime` 内で stderr を出さずに segfault した場合、ネイティブシンボルの不整合 (上記 HarfBuzz 系) を疑う。`LD_DEBUG=libs dotnet AudioClient.GUI.dll` で `dlopen` トレースを出すと最後にロードしようとした `.so` が分かる。
