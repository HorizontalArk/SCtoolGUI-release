# ファイル名のウィンドウタイトル対応 & コピー対象の二択 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** スクショのファイル名を実ウィンドウタイトルにできる設定と、クリップボードのコピー対象（一時プレビュー/最後に保存した画像）の二択を追加し、詳細設定のトグル群をFluent風スイッチに刷新する。

**Architecture:** テスト可能な判定ロジック（ファイル名ベース決定・コピー対象パス決定）を Win32/WPF 非依存の純粋クラスに切り出し、`Tests/SCtoolGui.Tests.csproj` に `Compile Include` でリンクして単体テストする。UI側（`ExecuteCapture`・コピーボタン・`SettingsWindow`）はその純粋クラスを呼ぶ薄い結線にする。トグルの見た目は WPF Style/ControlTemplate 差し替えで機能を変えずに刷新する。

**Tech Stack:** C# / .NET 10 / WPF、xUnit（テスト）、System.Text.Json（設定）

## Global Constraints

- コード内コメント・ログ・コミットメッセージ・UI文言はすべて**日本語**（英語混入・ハングル混入禁止）。
- テスト対象ロジックは Win32/WPF 非依存の純粋クラスに切り出し、`Tests/SCtoolGui.Tests.csproj` に `<Compile Include="..\XXX.cs" Link="Source\XXX.cs" />` でリンクする（既存パターン）。
- テストは xUnit、メソッド名は日本語（既存 `FileNameUtilTests` に倣う）。
- 設定の受け渡しは `SettingsWindow` のコンストラクタ引数と `Result*` プロパティ方式（既存パターン）。
- 新規設定はデフォルト値で後方互換（既存JSONに項目が無くても既定値が入る。マイグレーション不要）。
- ビルド/テストは `dotnet build` / `dotnet test`（リポジトリルート `C:/Users/Manju/SCtool/SCtoolGui/forGit/SCtoolGUI` で実行）。

---

## ファイル構成

**新規作成:**
- `FileBaseNameResolver.cs` — ファイル名ベース（拡張子・タイムスタンプ除く）を決める純粋クラス。設定ON/OFF・タイトル空フォールバックの分岐を閉じる。
- `CopyTargetResolver.cs` — コピー対象の種別（既定/明示）と各パスの存在から、実際にコピーするパスを決める純粋クラス。
- `Tests/FileBaseNameResolverTests.cs` — 上記の単体テスト。
- `Tests/CopyTargetResolverTests.cs` — 上記の単体テスト。

**変更:**
- `SettingsManager.cs` — `AppSettings` に `UseWindowTitleForFileName`・`CopySource` を追加。
- `MainWindow.Capture.cs` — `ExecuteCapture` のファイル名決定を `FileBaseNameResolver` 経由にする。
- `MainWindow.Preview.cs` — コピー処理を `CopyTargetResolver` 経由に一般化。スプリットボタンのイベントハンドラを追加。
- `MainWindow.xaml` — コピーボタンをスプリットボタン化。
- `SettingsWindow.xaml` / `SettingsWindow.xaml.cs` — 新設定2項目のUIと受け渡し。トグル群のStyle刷新。
- `MainWindow.xaml.cs` — `SettingsWindow` 呼び出しに新2項目を追加。
- `Tests/SCtoolGui.Tests.csproj` — 新規純粋クラス2つをリンク追加。

---

## Task 1: ファイル名ベース決定ロジック（純粋クラス + テスト）

**Files:**
- Create: `FileBaseNameResolver.cs`
- Test: `Tests/FileBaseNameResolverTests.cs`
- Modify: `Tests/SCtoolGui.Tests.csproj`（`Compile Include` 追加）

**Interfaces:**
- Produces: `static string SCtoolGui.FileBaseNameResolver.Resolve(bool useWindowTitle, string windowTitle, string registeredName)` — `useWindowTitle` が true かつ `windowTitle` が非空なら `FileNameUtil.ToSafeName(windowTitle)`、それ以外は `registeredName` をそのまま返す。`registeredName` は呼び出し側で既に安全化済みの登録名（`CurrentFolderName`）を渡す前提。

- [ ] **Step 1: テストプロジェクトに新クラスをリンク追加**

`Tests/SCtoolGui.Tests.csproj` の `<Compile Include ... />` 群（末尾、`ShortcutIconUpdater.cs` の次の行）に追加:

```xml
    <Compile Include="..\FileBaseNameResolver.cs" Link="Source\FileBaseNameResolver.cs" />
```

- [ ] **Step 2: 失敗するテストを書く**

Create `Tests/FileBaseNameResolverTests.cs`:

```csharp
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class FileBaseNameResolverTests
    {
        [Fact]
        public void OFFなら登録名をそのまま使う()
        {
            string result = FileBaseNameResolver.Resolve(
                useWindowTitle: false, windowTitle: "実際のタイトル", registeredName: "MyGame");
            Assert.Equal("MyGame", result);
        }

        [Fact]
        public void ONならウィンドウタイトルを安全化して使う()
        {
            string result = FileBaseNameResolver.Resolve(
                useWindowTitle: true, windowTitle: "a/b:c", registeredName: "MyGame");
            Assert.Equal("a_b_c", result);
        }

        [Fact]
        public void ONでもタイトルが空なら登録名にフォールバックする()
        {
            string result = FileBaseNameResolver.Resolve(
                useWindowTitle: true, windowTitle: "", registeredName: "MyGame");
            Assert.Equal("MyGame", result);
        }

        [Fact]
        public void ONでタイトルが長すぎる場合は切り詰められる()
        {
            string result = FileBaseNameResolver.Resolve(
                useWindowTitle: true, windowTitle: new string('x', 200), registeredName: "MyGame");
            Assert.True(result.Length <= 80);
        }
    }
}
```

- [ ] **Step 3: テストを実行して失敗を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter FullyQualifiedName~FileBaseNameResolverTests`
Expected: コンパイルエラー（`FileBaseNameResolver` が存在しない）で FAIL

- [ ] **Step 4: 最小実装を書く**

Create `FileBaseNameResolver.cs`:

```csharp
namespace SCtoolGui
{
    /// <summary>
    /// スクショのファイル名ベース（拡張子・タイムスタンプを除く部分）を決める。
    /// 設定に応じて登録名か実ウィンドウタイトルを使い分ける。
    /// </summary>
    public static class FileBaseNameResolver
    {
        /// <summary>
        /// ファイル名ベースを返す。
        /// useWindowTitle が true かつ windowTitle が非空なら、タイトルを安全化して使う。
        /// それ以外（OFF・タイトルが空）は、既に安全化済みの登録名をそのまま返す。
        /// </summary>
        public static string Resolve(bool useWindowTitle, string windowTitle, string registeredName)
        {
            if (useWindowTitle && !string.IsNullOrEmpty(windowTitle))
            {
                return FileNameUtil.ToSafeName(windowTitle);
            }
            return registeredName;
        }
    }
}
```

- [ ] **Step 5: テストを実行して成功を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter FullyQualifiedName~FileBaseNameResolverTests`
Expected: 4件すべて PASS

- [ ] **Step 6: コミット**

```bash
git add FileBaseNameResolver.cs Tests/FileBaseNameResolverTests.cs Tests/SCtoolGui.Tests.csproj
git commit -m "ファイル名ベース決定ロジックを追加（登録名/実タイトルの切替）"
```

---

## Task 2: コピー対象パス決定ロジック（純粋クラス + テスト）

**Files:**
- Create: `CopyTargetResolver.cs`
- Test: `Tests/CopyTargetResolverTests.cs`
- Modify: `Tests/SCtoolGui.Tests.csproj`（`Compile Include` 追加）

**Interfaces:**
- Produces:
  - `enum SCtoolGui.CopyTarget { TempPreview, LastSaved }`
  - `static string? SCtoolGui.CopyTargetResolver.Resolve(CopyTarget target, string tempPreviewPath, bool tempPreviewExists, string lastSavedPath, bool lastSavedExists)` — 指定 `target` に対応するパスが存在すればそのパスを、存在しなければ `null` を返す。存在判定は呼び出し側で `File.Exists` した結果を渡す（純粋関数に保つため）。
  - `static CopyTarget SCtoolGui.CopyTargetResolver.Parse(string source)` — 設定文字列 `"TempPreview"`/`"LastSaved"` を enum に変換。未知の値は `LastSaved` にフォールバック。

- [ ] **Step 1: テストプロジェクトに新クラスをリンク追加**

`Tests/SCtoolGui.Tests.csproj` の `Compile Include` 群に追加:

```xml
    <Compile Include="..\CopyTargetResolver.cs" Link="Source\CopyTargetResolver.cs" />
```

- [ ] **Step 2: 失敗するテストを書く**

Create `Tests/CopyTargetResolverTests.cs`:

```csharp
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class CopyTargetResolverTests
    {
        [Fact]
        public void 一時プレビュー指定で存在すればそのパスを返す()
        {
            string? result = CopyTargetResolver.Resolve(
                CopyTarget.TempPreview,
                tempPreviewPath: "temp.jpg", tempPreviewExists: true,
                lastSavedPath: "saved.jpg", lastSavedExists: true);
            Assert.Equal("temp.jpg", result);
        }

        [Fact]
        public void 最後に保存した画像指定で存在すればそのパスを返す()
        {
            string? result = CopyTargetResolver.Resolve(
                CopyTarget.LastSaved,
                tempPreviewPath: "temp.jpg", tempPreviewExists: true,
                lastSavedPath: "saved.jpg", lastSavedExists: true);
            Assert.Equal("saved.jpg", result);
        }

        [Fact]
        public void 一時プレビュー指定でも存在しなければnullを返す()
        {
            string? result = CopyTargetResolver.Resolve(
                CopyTarget.TempPreview,
                tempPreviewPath: "temp.jpg", tempPreviewExists: false,
                lastSavedPath: "saved.jpg", lastSavedExists: true);
            Assert.Null(result);
        }

        [Fact]
        public void 最後に保存した画像指定でも存在しなければnullを返す()
        {
            string? result = CopyTargetResolver.Resolve(
                CopyTarget.LastSaved,
                tempPreviewPath: "temp.jpg", tempPreviewExists: true,
                lastSavedPath: "saved.jpg", lastSavedExists: false);
            Assert.Null(result);
        }

        [Fact]
        public void Parseは設定文字列をenumに変換する()
        {
            Assert.Equal(CopyTarget.TempPreview, CopyTargetResolver.Parse("TempPreview"));
            Assert.Equal(CopyTarget.LastSaved, CopyTargetResolver.Parse("LastSaved"));
        }

        [Fact]
        public void Parseは未知の値をLastSavedにフォールバックする()
        {
            Assert.Equal(CopyTarget.LastSaved, CopyTargetResolver.Parse("なにか"));
        }
    }
}
```

- [ ] **Step 3: テストを実行して失敗を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter FullyQualifiedName~CopyTargetResolverTests`
Expected: コンパイルエラー（`CopyTargetResolver`/`CopyTarget` が存在しない）で FAIL

- [ ] **Step 4: 最小実装を書く**

Create `CopyTargetResolver.cs`:

```csharp
namespace SCtoolGui
{
    /// <summary>クリップボードにコピーする対象の種別。</summary>
    public enum CopyTarget
    {
        /// <summary>撮影前の確認用に表示している一時プレビュー画像。</summary>
        TempPreview,
        /// <summary>直近に撮影・保存した本番画像。</summary>
        LastSaved,
    }

    /// <summary>
    /// コピー対象の種別と各画像の存在状況から、実際にコピーすべきパスを決める。
    /// ファイル存在判定は呼び出し側で行い、結果だけ渡すことで純粋関数に保つ。
    /// </summary>
    public static class CopyTargetResolver
    {
        /// <summary>
        /// 指定した対象に対応するパスを返す。対象が存在しない場合は null。
        /// </summary>
        public static string? Resolve(
            CopyTarget target,
            string tempPreviewPath, bool tempPreviewExists,
            string lastSavedPath, bool lastSavedExists)
        {
            return target switch
            {
                CopyTarget.TempPreview => tempPreviewExists ? tempPreviewPath : null,
                CopyTarget.LastSaved => lastSavedExists ? lastSavedPath : null,
                _ => null,
            };
        }

        /// <summary>設定文字列を種別に変換する。未知の値は LastSaved 扱い。</summary>
        public static CopyTarget Parse(string source)
            => source == "TempPreview" ? CopyTarget.TempPreview : CopyTarget.LastSaved;
    }
}
```

- [ ] **Step 5: テストを実行して成功を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter FullyQualifiedName~CopyTargetResolverTests`
Expected: 6件すべて PASS

- [ ] **Step 6: コミット**

```bash
git add CopyTargetResolver.cs Tests/CopyTargetResolverTests.cs Tests/SCtoolGui.Tests.csproj
git commit -m "コピー対象パス決定ロジックを追加（一時プレビュー/最後に保存した画像）"
```

---

## Task 3: 設定に新2項目を追加

**Files:**
- Modify: `SettingsManager.cs`（`AppSettings` にプロパティ2つ）

**Interfaces:**
- Consumes: なし
- Produces: `AppSettings.UseWindowTitleForFileName` (bool, 既定 false)、`AppSettings.CopySource` (string, 既定 "LastSaved")

- [ ] **Step 1: `AppSettings` にプロパティを追加**

`SettingsManager.cs` の `AutoCopyClipboard` プロパティ（`public bool AutoCopyClipboard { get; set; } = true;`）の直後に追加:

```csharp

        /// <summary>ファイル名に実ウィンドウタイトルを使うか。false なら登録名（従来どおり）。フォルダ名は常に登録名。</summary>
        public bool UseWindowTitleForFileName { get; set; } = false;

        /// <summary>コピーボタンの既定対象。"LastSaved"（最後に保存した画像）/ "TempPreview"（一時プレビュー）。</summary>
        public string CopySource { get; set; } = "LastSaved";
```

- [ ] **Step 2: ビルドを実行して成功を確認**

Run: `dotnet build`
Expected: ビルド成功（警告のみ許容、エラーなし）

- [ ] **Step 3: コミット**

```bash
git add SettingsManager.cs
git commit -m "設定に UseWindowTitleForFileName と CopySource を追加"
```

---

## Task 4: 撮影時のファイル名決定を新ロジックに結線

**Files:**
- Modify: `MainWindow.Capture.cs:100-113`（`ExecuteCapture` 内）

**Interfaces:**
- Consumes: `FileBaseNameResolver.Resolve(bool, string, string)`（Task 1）、`AppSettings.UseWindowTitleForFileName`（Task 3）
- Produces: なし

- [ ] **Step 1: ファイル名決定を差し替える**

`MainWindow.Capture.cs` の現在の該当箇所（`safeName` を取得しフォルダとファイル名の両方に使っている部分）:

```csharp
                    // ファイル名・フォルダ名は、変化しうるタイトルではなく表示名を使う
                    string safeName = CurrentFolderName;

                    if (_settingsManager.Current.SaveInWindowNameFolder)
                    {
                        targetDir = Path.Combine(baseDir, safeName);
                    }

                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    string fullPath = Path.Combine(targetDir, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
```

を次に置き換える:

```csharp
                    // フォルダ名は常に登録名（表示名）を使う。フォルダ振り分けはタイトル変化で乱れさせない。
                    string folderName = CurrentFolderName;

                    if (_settingsManager.Current.SaveInWindowNameFolder)
                    {
                        targetDir = Path.Combine(baseDir, folderName);
                    }

                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    // ファイル名ベースは設定に応じて登録名か実ウィンドウタイトルを使う。
                    string fileBase = FileBaseNameResolver.Resolve(
                        _settingsManager.Current.UseWindowTitleForFileName,
                        selected.Title,
                        folderName);

                    string fullPath = Path.Combine(targetDir, $"{fileBase}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
```

- [ ] **Step 2: 直後のログ出力の変数名を合わせる**

同メソッド内の成功ログ（現在 `SaveAndLog($"【成功】 {safeName} -> {fullPath}");`）を、`safeName` が無くなったため次に変更:

```csharp
                    SaveAndLog($"【成功】 {fileBase} -> {fullPath}");
```

- [ ] **Step 3: ビルドを実行して成功を確認**

Run: `dotnet build`
Expected: ビルド成功（`safeName` 未定義エラーが出ないこと＝置換漏れなし）

- [ ] **Step 4: 実機で動作確認**

Run: `dotnet run`（または既存の実行手順）
手順:
1. 詳細設定は既定（ファイル名設定OFF）のまま撮影 → ファイル名が登録名で始まることを確認。
2. `%AppData%\SCtoolGui\cut_settings.json` を直接編集し `"UseWindowTitleForFileName": true` にして再起動 → 撮影 → ファイル名が実ウィンドウタイトルで始まることを確認（Task 6 でUIから切替可能になる。ここでは結線の確認）。

Expected: OFFで登録名、ONで実タイトルのファイル名になる。フォルダ名はどちらも登録名。

- [ ] **Step 5: コミット**

```bash
git add MainWindow.Capture.cs
git commit -m "撮影時のファイル名決定を FileBaseNameResolver に結線"
```

---

## Task 5: コピー処理を新ロジックに一般化 + スプリットボタン

**Files:**
- Modify: `MainWindow.Preview.cs:195-212`（`BtnCopyClipboard_Click` / `CopyPreviewToClipboard`）
- Modify: `MainWindow.xaml:318-324`（コピーボタン → スプリットボタン）
- Modify: `MainWindow.Capture.cs:130-133`（自動コピー呼び出しの整合）

**Interfaces:**
- Consumes: `CopyTargetResolver.Resolve(...)`・`CopyTargetResolver.Parse(string)`・`CopyTarget`（Task 2）、`AppSettings.CopySource`（Task 3）、`TempPreviewPath`・`_lastCapturedPath`・`HasLastCapture`（既存）
- Produces: `CopyToClipboard(CopyTarget target, bool isAuto)`（既存 `CopyPreviewToClipboard(bool)` を置換）

- [ ] **Step 1: コピー処理を一般化する**

`MainWindow.Preview.cs` の現在のコピー関連（`BtnCopyClipboard_Click` と `CopyPreviewToClipboard`）:

```csharp
        private void BtnCopyClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (!HasLastCapture) return;
            CopyPreviewToClipboard(isAuto: false);
        }

        private void CopyPreviewToClipboard(bool isAuto)
        {
            try
            {
                if (HasLastCapture)
                {
                    Clipboard.SetImage(LoadBitmap(_lastCapturedPath));
                    Log(isAuto ? "画像をクリップボードに自動コピーしました。" : "画像をクリップボードにコピーしました。");
                }
            }
            catch (Exception ex) { Log($"【エラー】 コピー失敗: {ex.Message}"); }
        }
```

を次に置き換える:

```csharp
        // スプリットボタン左「コピー」本体：詳細設定の既定対象でコピーする。
        private void BtnCopyClipboard_Click(object sender, RoutedEventArgs e)
        {
            var target = CopyTargetResolver.Parse(_settingsManager.Current.CopySource);
            CopyToClipboard(target, isAuto: false);
        }

        // スプリットボタン ▽ メニュー「一時プレビューをコピー」
        private void MenuCopyTempPreview_Click(object sender, RoutedEventArgs e)
            => CopyToClipboard(CopyTarget.TempPreview, isAuto: false);

        // スプリットボタン ▽ メニュー「最後に保存した画像をコピー」
        private void MenuCopyLastSaved_Click(object sender, RoutedEventArgs e)
            => CopyToClipboard(CopyTarget.LastSaved, isAuto: false);

        /// <summary>指定した対象の画像をクリップボードにコピーする。対象が無ければ警告ログを出す。</summary>
        private void CopyToClipboard(CopyTarget target, bool isAuto)
        {
            try
            {
                string? path = CopyTargetResolver.Resolve(
                    target,
                    TempPreviewPath, File.Exists(TempPreviewPath),
                    _lastCapturedPath, HasLastCapture);

                if (path == null)
                {
                    Log(target == CopyTarget.TempPreview
                        ? "一時プレビューがまだありません。"
                        : "保存された画像がまだありません。");
                    return;
                }

                Clipboard.SetImage(LoadBitmap(path));
                Log(isAuto ? "画像をクリップボードに自動コピーしました。" : "画像をクリップボードにコピーしました。");
            }
            catch (Exception ex) { Log($"【エラー】 コピー失敗: {ex.Message}"); }
        }
```

- [ ] **Step 2: 自動コピーの呼び出しを合わせる**

`MainWindow.Capture.cs` の自動コピー箇所（現在 `CopyPreviewToClipboard(isAuto: true);`）を、本番画像固定で呼ぶよう変更:

```csharp
                    if (_settingsManager.Current.AutoCopyClipboard)
                    {
                        // 撮影直後は保存した本番画像が正しいので、設定に依らず LastSaved を対象にする。
                        CopyToClipboard(CopyTarget.LastSaved, isAuto: true);
                    }
```

- [ ] **Step 3: コピーボタンをスプリットボタンに差し替える**

`MainWindow.xaml` の現在のコピーボタン（`<Button x:Name="BtnCopyClipboard" ...> ... </Button>`、`BtnOpenFile` と `BtnDeleteFile` の間）を次に置き換える。左本体＋右ドロップの2ボタンを1つの枠にまとめ、右ボタンの `ContextMenu` を `PlacementTarget` で開く:

```xml
                    <Border Margin="0,0,6,0" CornerRadius="4"
                            BorderBrush="{DynamicResource ControlElevationBorderBrush}" BorderThickness="1">
                        <StackPanel Orientation="Horizontal">
                            <Button x:Name="BtnCopyClipboard" Padding="10,6" IsEnabled="False"
                                    Click="BtnCopyClipboard_Click" BorderThickness="0" Background="Transparent"
                                    ToolTip="既定の対象をクリップボードにコピーします" ToolTipService.ShowOnDisabled="True">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Style="{StaticResource Icon}" Text="&#xE8C8;" Margin="0,0,6,0"/>
                                    <TextBlock Text="コピー" VerticalAlignment="Center"/>
                                </StackPanel>
                            </Button>
                            <Border Width="1" Background="{DynamicResource ControlElevationBorderBrush}"
                                    Margin="0,4"/>
                            <Button x:Name="BtnCopyDropdown" Padding="6,6" IsEnabled="False"
                                    Click="BtnCopyDropdown_Click" BorderThickness="0" Background="Transparent"
                                    ToolTip="コピーする対象を選びます" ToolTipService.ShowOnDisabled="True">
                                <TextBlock Text="&#xE70D;" Style="{StaticResource Icon}" FontSize="10"
                                           VerticalAlignment="Center"/>
                                <Button.ContextMenu>
                                    <ContextMenu>
                                        <MenuItem Header="一時プレビューをコピー" Click="MenuCopyTempPreview_Click"/>
                                        <MenuItem Header="最後に保存した画像をコピー" Click="MenuCopyLastSaved_Click"/>
                                    </ContextMenu>
                                </Button.ContextMenu>
                            </Button>
                        </StackPanel>
                    </Border>
```

- [ ] **Step 4: ドロップボタンのハンドラを追加**

`MainWindow.Preview.cs` に、右ドロップボタンで `ContextMenu` を開くハンドラを追加（`CopyToClipboard` 定義の下あたり）:

```csharp
        // スプリットボタン右「▽」：付属の ContextMenu をボタン位置に開く。
        private void BtnCopyDropdown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }
```

- [ ] **Step 5: ボタン有効化に BtnCopyDropdown を追加**

`MainWindow.Preview.cs` の `SetActionButtonsState` 内のボタン配列（現在 `new[] { BtnOpenFile, BtnCopyClipboard, BtnDeleteFile }`）に `BtnCopyDropdown` を追加:

```csharp
            foreach (var btn in new[] { BtnOpenFile, BtnCopyClipboard, BtnCopyDropdown, BtnDeleteFile })
```

また `ShowPreview` 内で `IsEnabled = true` を並べている箇所（`BtnOpenFile.IsEnabled = true;` などの並び）に次を追加:

```csharp
            BtnCopyDropdown.IsEnabled = true;
```

- [ ] **Step 6: ビルドを実行して成功を確認**

Run: `dotnet build`
Expected: ビルド成功（`CopyPreviewToClipboard` の残参照が無いこと。あればエラーで検出される）

- [ ] **Step 7: 実機で動作確認**

Run: `dotnet run`
手順:
1. ウィンドウを選び一時プレビューを表示 → 「コピー」本体クリック（既定 LastSaved・未撮影）→「保存された画像がまだありません。」のログを確認。
2. 一度撮影 → 「コピー」本体クリック → コピー成功ログ。
3. ▽ をクリック → メニューが出る → 「一時プレビューをコピー」で一時プレビューがコピーされること、「最後に保存した画像をコピー」で本番画像がコピーされることを、貼り付け先（ペイント等）で確認。
4. 自動コピー設定ONで撮影 → 撮影直後に本番画像が自動コピーされること。

Expected: 左右のクリックがそれぞれ独立して動作し、対象どおりの画像がコピーされる。

- [ ] **Step 8: コミット**

```bash
git add MainWindow.Preview.cs MainWindow.xaml MainWindow.Capture.cs
git commit -m "コピーをスプリットボタン化し対象をCopyTargetResolverで選択"
```

---

## Task 6: 詳細設定に新2項目のUIと受け渡しを追加

**Files:**
- Modify: `SettingsWindow.xaml`（ファイル名トグル + コピー対象コンボ）
- Modify: `SettingsWindow.xaml.cs`（コンストラクタ引数・IsChecked/SelectedIndex 設定・Result プロパティ・BtnSave）
- Modify: `MainWindow.xaml.cs:444-484`（`SettingsWindow` 呼び出しと結果受け取り）

**Interfaces:**
- Consumes: `AppSettings.UseWindowTitleForFileName`・`AppSettings.CopySource`（Task 3）
- Produces: `SettingsWindow.ResultUseWindowTitleForFileName` (bool)、`SettingsWindow.ResultCopySource` (string)

- [ ] **Step 1: `SettingsWindow.xaml` にUIを追加**

`SettingsWindow.xaml:33` の `ChkSaveInWindowFolder`（`Content="ウィンドウ名のフォルダに分類して保存する" Margin="0,0,0,10"`）の**直後の行**に、ファイル名トグルを同じ余白書式で追加:

```xml
            <CheckBox x:Name="ChkUseWindowTitleForFileName" Content="ファイル名に実際のウィンドウタイトルを使う（OFFなら登録名／フォルダ名は常に登録名）" Margin="0,0,0,10"/>
```

`SettingsWindow.xaml:37` の `ChkAutoCopyClipboard` 行の**直後**に、コピー対象コンボを追加。既存 `CmbTheme`（`:41-45`、`StackPanel Orientation="Horizontal" Margin="0,0,0,15"` に `TextBlock` + `ComboBox`）と同じ書式に揃える:

```xml
            <StackPanel Orientation="Horizontal" Margin="0,0,0,15">
                <TextBlock Text="コピーの既定対象:" VerticalAlignment="Center" Margin="0,0,10,0"/>
                <ComboBox x:Name="CmbCopySource" Width="200">
                    <ComboBoxItem Content="最後に保存した画像"/>
                    <ComboBoxItem Content="一時プレビュー"/>
                </ComboBox>
            </StackPanel>
```

- [ ] **Step 2: `SettingsWindow.xaml.cs` のコンストラクタ引数を追加**

コンストラクタシグネチャの末尾引数（`string previewAutoSwitch`）の後に2つ追加:

```csharp
        public SettingsWindow(string saveDir, uint modifiers, uint key, bool appTopmost, bool saveInWindowFolder, bool resetSettings, bool autoCopy, bool playShutterSound, double shutterVolume, bool alwaysRunAsAdmin, string theme, string iconPath, string verticalPreviewSide, string previewAutoSwitch, bool useWindowTitleForFileName, string copySource)
```

コンストラクタ本体の末尾（`CmbAutoSwitch.SelectedIndex = ...` の後）に初期値設定を追加:

```csharp
            ChkUseWindowTitleForFileName.IsChecked = useWindowTitleForFileName;
            CmbCopySource.SelectedIndex = copySource == "TempPreview" ? 1 : 0;
```

- [ ] **Step 3: `Result` プロパティを追加**

`ResultPreviewAutoSwitch` プロパティ宣言の近くに追加:

```csharp
        public bool ResultUseWindowTitleForFileName { get; private set; }
        public string ResultCopySource { get; private set; } = "LastSaved";
```

- [ ] **Step 4: `BtnSave_Click` に結果格納を追加**

`BtnSave_Click` 内の末尾（`ResultPreviewAutoSwitch = ...` の後、`this.DialogResult = true;` の前）に追加:

```csharp
            ResultUseWindowTitleForFileName = ChkUseWindowTitleForFileName.IsChecked == true;
            ResultCopySource = CmbCopySource.SelectedIndex == 1 ? "TempPreview" : "LastSaved";
```

- [ ] **Step 5: `MainWindow.xaml.cs` の呼び出しを更新**

`new SettingsWindow(...)` の引数末尾（`_settingsManager.Current.PreviewAutoSwitch` の後）に追加:

```csharp
                _settingsManager.Current.PreviewAutoSwitch,
                _settingsManager.Current.UseWindowTitleForFileName,
                _settingsManager.Current.CopySource) { Owner = this };
```

（元の `PreviewAutoSwitch) { Owner = this };` の閉じ括弧を上記に置き換える。）

結果受け取りブロック（`_settingsManager.Current.PreviewAutoSwitch = settingsWin.ResultPreviewAutoSwitch;` の後）に追加:

```csharp
                _settingsManager.Current.UseWindowTitleForFileName = settingsWin.ResultUseWindowTitleForFileName;
                _settingsManager.Current.CopySource = settingsWin.ResultCopySource;
```

- [ ] **Step 6: ビルドを実行して成功を確認**

Run: `dotnet build`
Expected: ビルド成功（コンストラクタ引数の数が一致し、`ChkUseWindowTitleForFileName`/`CmbCopySource` が解決すること）

- [ ] **Step 7: 実機で動作確認**

Run: `dotnet run`
手順:
1. 詳細設定を開く → 「ファイル名に実際のウィンドウタイトルを使う」チェックと「コピーの既定対象」コンボが表示される。
2. ファイル名チェックをON → 保存 → 撮影 → ファイル名が実タイトルになる。OFFに戻して登録名に戻ることも確認。
3. コピー既定対象を「一時プレビュー」に → 保存 → コピー本体クリックで一時プレビューがコピーされる。
4. 設定を閉じて再度開く → 選択状態が保持されている（JSON永続化の確認）。

Expected: UIから両設定を切替でき、撮影・コピー挙動と永続化が期待どおり。

- [ ] **Step 8: コミット**

```bash
git add SettingsWindow.xaml SettingsWindow.xaml.cs MainWindow.xaml.cs
git commit -m "詳細設定にファイル名設定とコピー既定対象のUIを追加"
```

---

## Task 7: 詳細設定トグル群のFluent風スイッチ化

**Files:**
- Modify: `SettingsWindow.xaml`（CheckBox 群に Fluent トグルスイッチ Style を適用）

**Interfaces:**
- Consumes: なし（見た目のみ。`IsChecked`/`Result*` の受け渡しは Task 6 までで確定済み）
- Produces: なし

- [ ] **Step 1: トグルスイッチ Style を定義**

`SettingsWindow.xaml` には現状 `<Window.Resources>` が無い（ルートは `<Window ...>` 直下に `<Grid Margin="20">`）。`<Window ...>` 開始タグと `<Grid Margin="20">` の間に `<Window.Resources>` を新設し、`CheckBox` を Win11 Fluent 風トグルスイッチに見せる Style を追加する。使用するブラシキー（`AccentFillColorDefaultBrush`・`TextOnAccentFillColorPrimaryBrush`・`TextFillColorSecondaryBrush`）は `MainWindow.xaml`/`SetupWizardWindow.xaml` で使用実績のあるアプリ共通テーマ辞書のキーで、`DynamicResource` で参照できる。モックの見た目（幅40・高さ20・つまみ移動・accent塗り）に合わせ、`x:Key="ToggleSwitchStyle"` として定義:

```xml
    <Window.Resources>
        <Style x:Key="ToggleSwitchStyle" TargetType="CheckBox">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="CheckBox">
                        <StackPanel Orientation="Horizontal">
                            <Border x:Name="Track" Width="40" Height="20" CornerRadius="10"
                                    BorderThickness="1.5"
                                    BorderBrush="{DynamicResource TextFillColorSecondaryBrush}"
                                    Background="Transparent">
                                <Ellipse x:Name="Knob" Width="12" Height="12" Margin="3"
                                         HorizontalAlignment="Left"
                                         Fill="{DynamicResource TextFillColorSecondaryBrush}"/>
                            </Border>
                            <ContentPresenter Margin="10,0,0,0" VerticalAlignment="Center"/>
                        </StackPanel>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="Track" Property="Background"
                                        Value="{DynamicResource AccentFillColorDefaultBrush}"/>
                                <Setter TargetName="Track" Property="BorderBrush"
                                        Value="{DynamicResource AccentFillColorDefaultBrush}"/>
                                <Setter TargetName="Knob" Property="HorizontalAlignment" Value="Right"/>
                                <Setter TargetName="Knob" Property="Fill"
                                        Value="{DynamicResource TextOnAccentFillColorPrimaryBrush}"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>
```

（`DynamicResource` のキーは既存テーマで使われているものに合わせる。既存XAMLで別名のブラシを使っている場合はそれに置換する。）

- [ ] **Step 2: 各 CheckBox に Style を適用**

`SettingsWindow.xaml` の対象 CheckBox（`ChkAppTopmost`・`ChkSaveInWindowFolder`・`ChkResetSettingsOnWindowChange`・`ChkAutoCopyClipboard`・`ChkPlayShutterSound`・`ChkAlwaysRunAsAdmin`・`ChkUseWindowTitleForFileName`）それぞれに属性を追加:

```xml
Style="{StaticResource ToggleSwitchStyle}"
```

- [ ] **Step 3: ビルドを実行して成功を確認**

Run: `dotnet build`
Expected: ビルド成功（Style キー・リソースキーが解決すること）

- [ ] **Step 4: 実機で見た目を確認**

Run: `dotnet run` → 詳細設定を開く
確認:
1. 各項目がトグルスイッチの見た目になっている（チェックボックスではない）。
2. ON/OFF でつまみが左右に動き、ON時に accent 色で塗られる。
3. テーマをLight/Dark/Systemで切り替え、いずれも視認できる配色。
4. 各トグルの ON/OFF が保存後に正しく反映される（機能が壊れていない）。

Expected: 見た目がFluント風トグルに刷新され、機能は従来どおり。

注意（[ui-verify-uipi-and-clipping] より）: 見切れやテーマ反映は実物のUIで目視確認する。古いビルドを掴んでいないか、ビルド後の起動を確実にする。

- [ ] **Step 5: コミット**

```bash
git add SettingsWindow.xaml
git commit -m "詳細設定のトグル群をFluent風スイッチに刷新"
```

---

## Task 8: 全体テストと最終確認

**Files:** なし（検証のみ）

- [ ] **Step 1: 全テストを実行**

Run: `dotnet test`
Expected: 既存 + 新規（Task 1: 4件、Task 2: 6件）すべて PASS

- [ ] **Step 2: リリースビルドで確認**

Run: `dotnet build -c Release`
Expected: エラーなし

- [ ] **Step 3: 日本語コメントの走査（ハングル混入チェック）**

新規・変更ファイルにハングルが混入していないか機械的に確認:

Run: `git diff --name-only HEAD~7 | xargs grep -lP "[\x{AC00}-\x{D7A3}]" 2>/dev/null || echo "ハングル混入なし"`
Expected: 「ハングル混入なし」

- [ ] **Step 4: 統合シナリオの実機確認**

Run: `dotnet run`
一連の流れを確認:
1. ファイル名OFF + コピー既定LastSaved（初期状態）で撮影・コピーが従来どおり動く。
2. ファイル名ONにして撮影 → ファイル名が実タイトル、フォルダは登録名。
3. コピー既定をTempPreviewに → 本体コピーで一時プレビュー、▽メニューで両方選べる。
4. 詳細設定のトグルがFluント風で、テーマ追従する。

Expected: すべて期待どおり。問題なければ完了。
