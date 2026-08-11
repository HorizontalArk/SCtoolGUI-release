# 初回セットアップのモーダル化・前面化とタスクバーアイコン反映 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 初回セットアップウィザードをメインの前面にモーダル表示し、ユーザー設定アイコンをタスクバーへ反映する。

**Architecture:** ウィザード表示を Program.cs から App.OnStartup へ移し、MainWindow.Show() 直後に Owner 付き ShowDialog でモーダル化。アイコンは当初の WM_SETICON 実装を撤回し、画像を .ico 化して既知の .lnk 群の IconLocation を書き換える方式へ変更する。

**Tech Stack:** .NET 10 / WPF, Win32 (WScript.Shell 経由の .lnk 操作 or IShellLink), xUnit。

## Global Constraints

- 日本語コメント（SCtoolGUI は日本語コメント。ハングル混入禁止）。
- ユーザーの独自アイコン画像・変換後 .ico は `%AppData%\SCtoolGui` にのみ置き、リポジトリ・配布物・git 追跡に一切含めない。
- 依存パッケージ追加なし（Win32/WPF のみ）を優先。.ico エンコードで手書きが困難な場合のみ System.Drawing.Common を検討（Task 3 で判断）。
- テスト対象の純粋ロジックは Win32/WPF 非依存にし、`Tests\SCtoolGui.Tests.csproj` に `<Compile Include>` でソースリンクする（既存パターン）。
- 完了報告・コミットは日本語。

---

### Task 1: ウィザードをメイン前面のモーダルへ移す

**Files:**
- Modify: `App.xaml.cs`（OnStartup にウィザード表示ロジックを追加）
- Modify: `Program.cs:46-74`（ウィザード表示ブロックを削除し App へ移譲）
- Test: なし（UI/起動シーケンスのため実機検証。純粋ロジック `ShouldShowSetupWizard` は既存テストで担保済み）

**Interfaces:**
- Consumes: `SettingsManager`, `SettingsManager.ShouldShowSetupWizard(bool,bool)`, `AppUpdateService.IsInstalled`, `SetupWizardWindow(string,bool)`, `ShortcutLocationResolver.Resolve(bool,bool)`, `ShortcutInstaller.Create(ShortcutChoice,bool)`。
- Produces: なし（後続タスクは依存しない）。

- [ ] **Step 1: Program.cs からウィザード表示ブロックを削除**

`Program.cs` の 46-74 行（`// 初回セットアップウィザード…` の try ブロック全体）を削除する。削除後、`app.InitializeComponent();` の次は直接 `app.Run();` になる。

```csharp
            var app = new App();
            app.InitializeComponent();

            app.Run();
```

- [ ] **Step 2: App.OnStartup にウィザード表示を追加**

`App.xaml.cs` の `OnStartup` を次のようにする。MainWindow を表示した直後、その前面に Owner 付きモーダルでウィザードを出す。ウィザード表示中は起動時前面化と競合しないよう、先に MainWindow を出してから ShowDialog する。

```csharp
using System;
using System.Windows;

namespace SCtoolGui
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();

                // 初回セットアップウィザード（インストール版の初回のみ）。
                // MainWindow を表示した直後に、その前面へ Owner 付きモーダルで出す。
                // Owner を設定することで背後のメインは自動的に無効化（モーダルロック）される。
                ShowSetupWizardIfNeeded(mainWindow);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"起動エラー: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 未完了かつインストール版のときだけ、初回セットアップウィザードをメインの前面に
        /// モーダル表示する。完了時は設定を保存し、選択に従ってショートカットを作成する。
        /// </summary>
        private static void ShowSetupWizardIfNeeded(Window owner)
        {
            try
            {
                var settings = new SettingsManager();
                settings.Load();
                bool isInstalled = new AppUpdateService().IsInstalled;

                if (!SettingsManager.ShouldShowSetupWizard(settings.Current.SetupCompleted, isInstalled))
                    return;

                var wizard = new SetupWizardWindow(
                    settings.Current.SaveDirectory,
                    settings.Current.SaveInWindowNameFolder)
                {
                    Owner = owner,
                };
                wizard.Activate();

                if (wizard.ShowDialog() == true)
                {
                    settings.Current.SaveDirectory = wizard.SelectedSaveDirectory;
                    settings.Current.SaveInWindowNameFolder = wizard.SaveInWindowNameFolder;
                    settings.Current.SetupCompleted = true;
                    settings.Save();

                    var choice = ShortcutLocationResolver.Resolve(
                        wizard.CreateDesktopShortcut, wizard.CreateStartMenuShortcut);
                    ShortcutInstaller.Create(choice, isInstalled);
                }
                // キャンセル/閉じるは SetupCompleted=false のまま（次回再表示）
            }
            catch { }
        }
    }
}
```

- [ ] **Step 3: ビルドが通ることを確認**

Run: `dotnet build -c Debug -v q`
Expected: `ビルドに成功しました。` / 0 エラー。`Program.cs` から未使用の using（Velopack 以外）や `setupSettings` 参照が消えていること。`Program.cs` は `VelopackApp.Build().Run()` / 昇格 / SingleInstance / `app.Run()` のみになる。

- [ ] **Step 4: 実機でモーダル性・前面性を確認**

設定の `SetupCompleted` を false にして v1.0.7 相当のインストール版を起動し、次を確認する:
- ウィザードが前面（foreground）に出る
- 背後の MainWindow が無効化され操作できない（モーダル）
- 完了ボタンを押すと `SetupCompleted=true` になり、再起動で二度と出ない

（検証手順は既存の PowerShell UIA 計測を流用。実機目視 + UIA の foreground / ウィンドウ列挙で確認。）

- [ ] **Step 5: コミット**

```bash
git add App.xaml.cs Program.cs
git commit -m "初回セットアップをメイン前面のモーダルに変更"
```

---

### Task 2: WM_SETICON 実装の撤回

**Files:**
- Modify: `MainWindow.xaml.cs`（ApplyWindowIcon を this.Icon のみに戻し、WM_SETICON 一式を削除）
- Modify: `MainWindow.Capture.cs:25-37`（OnSourceInitialized のアイコン再適用を削除）
- Test: なし（削除のみ。ビルドと既存テストで担保）

**Interfaces:**
- Consumes: `_settingsManager.Current.IconPath`。
- Produces: `ApplyWindowIcon()`（this.Icon のみ設定する版。Task 4 が設定変更時に別途 .lnk 更新を呼ぶ）。

- [ ] **Step 1: ApplyWindowIcon を this.Icon のみに戻す**

`MainWindow.xaml.cs` の `ApplyWindowIcon` 以降に Task で追加された WM_SETICON 関連（`ApplyTaskbarIcon`, `CreateHIcon`, `CreateBgraDib`, `ICONINFO`/`BITMAPINFOHEADER`/`BITMAPINFO` 構造体, `_iconApplyPending`, `_bigIcon`, `_smallIcon`, `WM_SETICON`/`ICON_SMALL`/`ICON_BIG` 定数, `SendMessage`/`DestroyIcon`/`CreateIconIndirect`/`CreateBitmap`/`CreateDIBSection`/`DeleteObject` の DllImport）をすべて削除し、次のシンプルな版に戻す。

```csharp
        /// <summary>設定のアイコン画像パスを Window.Icon に反映する。空/不正なら埋め込み既定に戻す。</summary>
        private void ApplyWindowIcon()
        {
            string path = _settingsManager.Current.IconPath;
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(path));
                else
                    this.Icon = null; // 埋め込み既定に戻す
            }
            catch { this.Icon = null; }
        }
```

- [ ] **Step 2: OnClosed の HICON 破棄を削除**

`MainWindow.xaml.cs` の `OnClosed` 先頭に Task で追加した「WM_SETICON で送った独自 HICON を破棄」ブロック（`if (_bigIcon != IntPtr.Zero) ...` の 2 行 + コメント）を削除する。

- [ ] **Step 3: OnSourceInitialized のアイコン再適用を削除**

`MainWindow.Capture.cs` の `OnSourceInitialized` から、追加した `_iconApplyPending` 判定ブロックを削除し、元の形に戻す。

```csharp
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            RegisterHotKey();
        }
```

- [ ] **Step 4: ビルドと既存テスト**

Run: `dotnet build -c Debug -v q`
Expected: 0 エラー。未使用シンボルの警告が出ないこと。

Run: `cd Tests && dotnet test -v q`
Expected: 既存 74 テスト全合格。

- [ ] **Step 5: コミット**

```bash
git add MainWindow.xaml.cs MainWindow.Capture.cs
git commit -m "タスクバー未反映だったWM_SETICON実装を撤回しthis.Iconのみに戻す"
```

---

### Task 3: 画像を .ico に変換する IconIcoWriter

**Files:**
- Create: `IconIcoWriter.cs`（任意画像 → 複数解像度 .ico を `%AppData%\SCtoolGui` に書き出す）
- Test: `Tests\IconIcoWriterTests.cs`
- Modify: `Tests\SCtoolGui.Tests.csproj:25-34`（`IconIcoWriter.cs` をソースリンク）

**Interfaces:**
- Consumes: なし。
- Produces:
  - `public static class IconIcoWriter`
  - `public static bool TryWriteIco(string sourceImagePath, string destIcoPath, out string error)` — sourceImagePath（png/jpg/bmp/ico）を読み、16/32/48/256 の PNG フレームを内包する .ico を destIcoPath に書く。成功で true。
  - .ico のエンコードは WPF（`BitmapFrame` を各サイズにデコード → PNG バイト列 → ICONDIR/ICONDIRENTRY を手書き）で行い、System.Drawing 非依存とする。PNG 圧縮フレームを持つ .ico は Vista 以降で有効。

**判断メモ:** WPF に .ico エンコーダは無いため、ICO コンテナ（6 byte ICONDIR + 16 byte×N ICONDIRENTRY + 各 PNG データ）を手書きする。各フレームは `PngBitmapEncoder` で作った PNG バイト列をそのまま埋め込む（PNG 圧縮アイコン）。これで System.Drawing.Common 追加を回避できる。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.IO;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class IconIcoWriterTests
    {
        [Fact]
        public void PNGから複数解像度のicoを書き出せる()
        {
            string src = Path.Combine(Path.GetTempPath(), $"src_{System.Guid.NewGuid():N}.png");
            string dst = Path.Combine(Path.GetTempPath(), $"dst_{System.Guid.NewGuid():N}.ico");
            try
            {
                WriteTestPng(src, 64, 48); // 非正方でもよい
                bool ok = IconIcoWriter.TryWriteIco(src, dst, out string err);
                Assert.True(ok, err);
                Assert.True(File.Exists(dst));

                using var fs = File.OpenRead(dst);
                var head = new byte[6];
                fs.Read(head, 0, 6);
                // ICONDIR: reserved=0, type=1(icon), count>=1
                Assert.Equal(0, head[0] | head[1]);
                Assert.Equal(1, head[2] | (head[3] << 8));
                int count = head[4] | (head[5] << 8);
                Assert.True(count >= 1);
            }
            finally
            {
                if (File.Exists(src)) File.Delete(src);
                if (File.Exists(dst)) File.Delete(dst);
            }
        }

        // 32bit BGRA の単色 PNG を書く（テスト補助）
        private static void WriteTestPng(string path, int w, int h)
        {
            var rt = new System.Windows.Media.Imaging.RenderTargetBitmap(
                w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var dv = new System.Windows.Media.DrawingVisual();
            using (var dc = dv.RenderOpen())
                dc.DrawRectangle(System.Windows.Media.Brushes.Crimson, null,
                    new System.Windows.Rect(0, 0, w, h));
            rt.Render(dv);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rt));
            using var fs = File.Create(path);
            enc.Save(fs);
        }
    }
}
```

**注意:** このテストは WPF 型（RenderTargetBitmap 等）を使うため、テストプロジェクトが `net10.0` のままだと `PresentationCore` を参照できない。テストプロジェクトの TargetFramework を `net10.0-windows` にし `<UseWPF>true</UseWPF>` を足すか、テスト補助を Win32 非依存の最小 PNG バイト列直書きに変える。まず TargetFramework 変更で対応（Step 2 で反映）。

- [ ] **Step 2: テストプロジェクトを WPF 対応にし、ソースをリンク**

`Tests\SCtoolGui.Tests.csproj` の PropertyGroup を次に変更:

```xml
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
```

ItemGroup（ソースリンク）に追加:

```xml
    <Compile Include="..\IconIcoWriter.cs" Link="Source\IconIcoWriter.cs" />
```

- [ ] **Step 3: テストが失敗することを確認**

Run: `cd Tests && dotnet test --filter IconIcoWriterTests -v q`
Expected: FAIL（`IconIcoWriter` が存在しない、またはコンパイルエラー）。

- [ ] **Step 4: IconIcoWriter を実装**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SCtoolGui
{
    /// <summary>
    /// 任意画像（png/jpg/bmp/ico）を、複数解像度の PNG フレームを内包する .ico に変換して書き出す。
    /// .lnk の IconLocation はタスクバー表示に .ico を要求するため、ユーザー選択画像をこの形式へ変換する。
    /// System.Drawing に依存せず、WPF で各サイズにデコードした PNG を ICO コンテナに手書きする。
    /// </summary>
    public static class IconIcoWriter
    {
        // 生成するアイコンのサイズ（px）。タスクバー/一覧/大アイコン向け。
        private static readonly int[] Sizes = { 16, 32, 48, 256 };

        public static bool TryWriteIco(string sourceImagePath, string destIcoPath, out string error)
        {
            error = "";
            try
            {
                var src = new BitmapImage();
                src.BeginInit();
                src.CacheOption = BitmapCacheOption.OnLoad;
                src.UriSource = new Uri(sourceImagePath);
                src.EndInit();

                var pngFrames = new List<byte[]>();
                foreach (int size in Sizes)
                    pngFrames.Add(RenderPng(src, size));

                Directory.CreateDirectory(Path.GetDirectoryName(destIcoPath)!);
                using var fs = File.Create(destIcoPath);
                WriteIcoContainer(fs, pngFrames);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>元画像をアスペクト保持で size×size の正方に中央配置し、PNG バイト列にする。</summary>
        private static byte[] RenderPng(BitmapSource src, int size)
        {
            var target = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                double scale = Math.Min((double)size / src.PixelWidth, (double)size / src.PixelHeight);
                double w = src.PixelWidth * scale, h = src.PixelHeight * scale;
                dc.DrawImage(src, new Rect((size - w) / 2, (size - h) / 2, w, h));
            }
            target.Render(visual);

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(target));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return ms.ToArray();
        }

        /// <summary>ICONDIR + ICONDIRENTRY×N + 各 PNG データ を書く。</summary>
        private static void WriteIcoContainer(Stream fs, List<byte[]> pngFrames)
        {
            using var w = new BinaryWriter(fs);
            // ICONDIR
            w.Write((ushort)0);              // reserved
            w.Write((ushort)1);              // type = 1 (icon)
            w.Write((ushort)pngFrames.Count);

            // 各 PNG データはヘッダ群の直後から並ぶ
            int offset = 6 + 16 * pngFrames.Count;
            for (int i = 0; i < pngFrames.Count; i++)
            {
                int size = Sizes[i];
                byte[] png = pngFrames[i];
                // ICONDIRENTRY
                w.Write((byte)(size >= 256 ? 0 : size)); // width（256 は 0 表記）
                w.Write((byte)(size >= 256 ? 0 : size)); // height
                w.Write((byte)0);   // color count
                w.Write((byte)0);   // reserved
                w.Write((ushort)1); // planes
                w.Write((ushort)32);// bit count
                w.Write((uint)png.Length);
                w.Write((uint)offset);
                offset += png.Length;
            }
            foreach (var png in pngFrames)
                w.Write(png);
        }
    }
}
```

- [ ] **Step 5: テストが通ることを確認**

Run: `cd Tests && dotnet test --filter IconIcoWriterTests -v q`
Expected: PASS。

- [ ] **Step 6: 既存テストが壊れていないことを確認**

Run: `cd Tests && dotnet test -v q`
Expected: 全合格（既存 74 + 新規 1）。

- [ ] **Step 7: コミット**

```bash
git add IconIcoWriter.cs Tests/IconIcoWriterTests.cs Tests/SCtoolGui.Tests.csproj
git commit -m "画像を複数解像度icoへ変換するIconIcoWriterを追加"
```

---

### Task 4: .lnk の IconLocation を更新する ShortcutIconUpdater

**Files:**
- Create: `ShortcutIconUpdater.cs`（既知の .lnk 群の IconLocation を更新／既定へ戻す＋対象パス決定ロジック）
- Test: `Tests\ShortcutIconTargetsTests.cs`（対象パス決定の純粋ロジックのみ）
- Modify: `Tests\SCtoolGui.Tests.csproj`（対象パス決定ロジックをソースリンク）

**Interfaces:**
- Consumes: `IconIcoWriter.TryWriteIco`。
- Produces:
  - `public static class ShortcutIconUpdater`
  - `public static IReadOnlyList<string> KnownShortcutPaths()` — スタートメニュー/デスクトップ/タスクバーピン留めの SCtoolGui.lnk 候補パス（存在有無に関わらず既知パスを返す純粋関数。環境変数展開のみ）。
  - `public static void ApplyUserIcon(string userImagePath)` — 画像を `%AppData%\SCtoolGui\user_icon.ico` へ変換し、存在する既知 .lnk の IconLocation をその .ico に更新、SHChangeNotify で通知。
  - `public static void ResetToDefault(string exePath)` — 存在する既知 .lnk の IconLocation を `exePath,0`（app.ico）へ戻し、SHChangeNotify で通知。
- .lnk 操作は `WScript.Shell`（COM: `IWshShortcut.IconLocation`）を late-bind で使う。SHChangeNotify は user32/shell32 の DllImport。

- [ ] **Step 1: 対象パス決定の失敗するテストを書く**

対象パス決定だけを純粋関数に切り出してテストする（COM/シェルは実機検証）。

```csharp
using System;
using System.Linq;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class ShortcutIconTargetsTests
    {
        [Fact]
        public void 既知の3種のlnkパスを返す()
        {
            var paths = ShortcutIconTargets.Resolve(
                appData: @"C:\Users\u\AppData\Roaming",
                userProfile: @"C:\Users\u");

            Assert.Contains(paths, p => p.EndsWith(@"Start Menu\Programs\SCtoolGui.lnk"));
            Assert.Contains(paths, p => p.EndsWith(@"Desktop\SCtoolGui.lnk"));
            Assert.Contains(paths, p => p.EndsWith(@"User Pinned\TaskBar\SCtoolGui.lnk"));
            Assert.Equal(3, paths.Count);
        }
    }
}
```

- [ ] **Step 2: ShortcutIconTargets（純粋ロジック）を実装**

`ShortcutIconUpdater.cs` 内に、テスト可能な純粋クラスを分離して置く。

```csharp
using System.Collections.Generic;
using System.IO;

namespace SCtoolGui
{
    /// <summary>SCtoolGui.lnk が置かれ得る既知の標準パスを、環境変数展開だけで決める純粋ロジック。</summary>
    public static class ShortcutIconTargets
    {
        public static IReadOnlyList<string> Resolve(string appData, string userProfile)
        {
            return new[]
            {
                Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", "SCtoolGui.lnk"),
                Path.Combine(userProfile, "Desktop", "SCtoolGui.lnk"),
                Path.Combine(appData, "Microsoft", "Internet Explorer", "Quick Launch",
                             "User Pinned", "TaskBar", "SCtoolGui.lnk"),
            };
        }
    }
}
```

- [ ] **Step 3: テストプロジェクトにソースリンクし、テストが通ることを確認**

`Tests\SCtoolGui.Tests.csproj` の ItemGroup に追加:

```xml
    <Compile Include="..\ShortcutIconUpdater.cs" Link="Source\ShortcutIconUpdater.cs" />
```

Run: `cd Tests && dotnet test --filter ShortcutIconTargetsTests -v q`
Expected: PASS。

**注意:** `ShortcutIconUpdater.cs` は WScript.Shell COM や DllImport を含むが、`net10.0-windows`（Task 3 で変更済み）ならリンクしてもビルド可能。COM を使う部分はテストから呼ばないので実機検証に委ねる。

- [ ] **Step 4: ShortcutIconUpdater 本体（COM/シェル操作）を実装**

同ファイルに追記する。

```csharp
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SCtoolGui
{
    /// <summary>
    /// 既知の .lnk 群の IconLocation を更新し、タスクバー等のアイコンをユーザー画像へ反映する。
    /// タスクバー表示は実行中ウィンドウの WM_SETICON ではなく .lnk の IconLocation が支配するため、
    /// ここで .lnk を書き換える。反映は即時保証せず、SHChangeNotify 通知後 次回起動/サインインで確実化。
    /// ユーザー画像から変換した .ico は %AppData%\SCtoolGui にのみ置く（配布物・リポジトリに含めない）。
    /// </summary>
    public static class ShortcutIconUpdater
    {
        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        /// <summary>変換後 .ico の保存先（%AppData%\SCtoolGui\user_icon.ico）。</summary>
        public static string UserIcoPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "SCtoolGui", "user_icon.ico");

        /// <summary>ユーザー画像を .ico 化し、存在する既知 .lnk の IconLocation を更新する。</summary>
        public static void ApplyUserIcon(string userImagePath)
        {
            if (string.IsNullOrEmpty(userImagePath) || !File.Exists(userImagePath)) return;
            if (!IconIcoWriter.TryWriteIco(userImagePath, UserIcoPath, out _)) return;
            UpdateAll($"{UserIcoPath},0");
        }

        /// <summary>既知 .lnk の IconLocation を exe 埋め込み既定（app.ico）へ戻す。</summary>
        public static void ResetToDefault(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return;
            UpdateAll($"{exePath},0");
        }

        private static void UpdateAll(string iconLocation)
        {
            var targets = ShortcutIconTargets.Resolve(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            bool any = false;
            foreach (var lnk in targets)
            {
                if (!File.Exists(lnk)) continue; // ピン留め等していない場所はスキップ（正常系）
                try
                {
                    SetShortcutIcon(lnk, iconLocation);
                    any = true;
                }
                catch { /* 1 つ失敗しても他を試す */ }
            }
            if (any) SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>WScript.Shell 経由で .lnk の IconLocation を設定して保存する。</summary>
        private static void SetShortcutIcon(string lnkPath, string iconLocation)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic sc = shell.CreateShortcut(lnkPath);
                sc.IconLocation = iconLocation;
                sc.Save();
                Marshal.FinalReleaseComObject(sc);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
```

- [ ] **Step 5: ビルドと既存テスト**

Run: `dotnet build -c Debug -v q`
Expected: 0 エラー。

Run: `cd Tests && dotnet test -v q`
Expected: 全合格。

- [ ] **Step 6: コミット**

```bash
git add ShortcutIconUpdater.cs Tests/ShortcutIconTargetsTests.cs Tests/SCtoolGui.Tests.csproj
git commit -m "既知の.lnkのIconLocationを更新するShortcutIconUpdaterを追加"
```

---

### Task 5: 設定変更時にアイコンを .lnk へ反映する結線

**Files:**
- Modify: `MainWindow.xaml.cs:391-392`（設定保存時に ShortcutIconUpdater を呼ぶ）
- Test: なし（結線。実機検証）

**Interfaces:**
- Consumes: `ShortcutIconUpdater.ApplyUserIcon(string)`, `ShortcutIconUpdater.ResetToDefault(string)`, `_settingsManager.Current.IconPath`, `System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName`。
- Produces: なし。

- [ ] **Step 1: 詳細設定の保存処理にアイコン反映を追加**

`MainWindow.xaml.cs` の設定保存ブロック（`_settingsManager.Current.IconPath = settingsWin.ResultIconPath;` の直後、`ApplyWindowIcon();` の並び）に、.lnk 更新を追加する。

```csharp
                _settingsManager.Current.IconPath = settingsWin.ResultIconPath;
                ApplyWindowIcon();

                // タスクバー等のアイコンは .lnk の IconLocation が支配するため、ここで .lnk 群も更新する。
                // 反映は次回起動/サインインで確実化（即時反映は OS のアイコンキャッシュ都合で保証しない）。
                try
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (!string.IsNullOrEmpty(_settingsManager.Current.IconPath))
                        ShortcutIconUpdater.ApplyUserIcon(_settingsManager.Current.IconPath);
                    else
                        ShortcutIconUpdater.ResetToDefault(exePath);
                }
                catch { }
```

- [ ] **Step 2: ビルド**

Run: `dotnet build -c Debug -v q`
Expected: 0 エラー。

- [ ] **Step 3: 実機で反映を確認**

v1.0.7 相当のインストール版で、詳細設定からユーザー画像を選び保存 → `%AppData%\SCtoolGui\user_icon.ico` が生成され、存在する .lnk の IconLocation がそれを指すこと（WScript.Shell で読み出して確認）。アイコンを空に戻すと `current\SCtoolGui.exe,0` に戻ること。タスクバー見た目は Explorer 再起動 or 再サインインで反映されることを目視。

- [ ] **Step 4: コミット**

```bash
git add MainWindow.xaml.cs
git commit -m "詳細設定のアイコン変更を.lnkへ反映する結線を追加"
```

---

## Self-Review

- **Spec coverage:**
  - A（モーダル化・前面化）→ Task 1 ✓
  - B（WM_SETICON 撤回）→ Task 2 ✓
  - B（.ico 変換）→ Task 3 ✓
  - B（.lnk 群更新 + SHChangeNotify + 既定復帰）→ Task 4 ✓
  - B（設定変更時の結線）→ Task 5 ✓
  - 制約（%AppData% のみ・混入禁止）→ Task 3/4 の保存先で担保 ✓
  - 見切れ修正は別コミット済みのため本計画外（設計に明記済み）✓
- **Placeholder scan:** TBD/TODO なし。全コード提示済み。
- **Type consistency:** `IconIcoWriter.TryWriteIco(string,string,out string)`、`ShortcutIconTargets.Resolve(string,string)`、`ShortcutIconUpdater.ApplyUserIcon(string)/ResetToDefault(string)/UserIcoPath` を各タスクで一貫使用。
- **Ambiguity:** テストプロジェクトの net10.0→net10.0-windows 変更を Task 3 Step 2 で明示。COM 部分は実機検証と明記。
