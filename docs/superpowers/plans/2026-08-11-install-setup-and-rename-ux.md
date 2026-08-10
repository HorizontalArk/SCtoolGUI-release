# 初回セットアップウィザード＋名前変更/アクティブ化UX改善 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** インストール時のショートカット作成と画像保存場所を初回ウィザードで任意化し、名前変更時のフォルダ孤立・ToolTip写り込み・切り替え時の対象アクティブ化という3つのUX問題を解消する。

**Architecture:** Velopackの自動ショートカット作成を`--shortcuts None`で無効化し、初回起動時に表示する`SetupWizardWindow`でユーザーに選ばせて`ShortcutInstaller`（Velopack SDKラッパ）が作成する。名前変更・キャプチャ経路は既存の`MainWindow` partial クラス群に手を入れる。フォルダ移動判定とショートカット位置解決はテスト可能な純粋ロジックとして分離する。

**Tech Stack:** .NET 10 (net10.0-windows), WPF, Velopack 1.2.0, xUnit（`Tests/`）。

## Global Constraints

- コメント・UI文言・ログは日本語（このリポジトリの規約。ハングル混入禁止）。
- 新規/変更UIは直書き色を使わず `DynamicResource`（Fluentテーマ）でテーマ連動させる。
- 対象フレームワーク `net10.0-windows`、SDK 10.0.x。
- Velopack 非管理環境（ポータブル/`dotnet run`）では `AppUpdateService.IsInstalled == false`。ショートカット作成・ウィザードはこの値で分岐する。
- テストは xUnit の `[Fact]`、日本語メソッド名（既存 `Tests/*.cs` に倣う）。
- ビルド確認: `dotnet build SCtoolGui.csproj -c Debug`。テスト: `dotnet test Tests/SCtoolGui.Tests.csproj`。
- コミットは各タスク末尾。co-author 行を付す。

---

## ファイル構成

**新規:**
- `ShortcutLocationResolver.cs` — チェック状態(desktop,startMenu) → 作成すべきショートカット記述への純粋変換（テスト対象）。
- `ShortcutInstaller.cs` — `ShortcutLocationResolver` の結果を Velopack SDK で実際に作成する薄いラッパ。
- `FolderRenamePlanner.cs` — 名前変更時に旧フォルダを移動すべきか/衝突かを判定する純粋ロジック（テスト対象）。
- `SetupWizardWindow.xaml` / `SetupWizardWindow.xaml.cs` — 初回セットアップUI。
- `Tests/ShortcutLocationResolverTests.cs`
- `Tests/FolderRenamePlannerTests.cs`
- `Tests/SetupCompletedTests.cs` — 設定モデルの往復と初回判定ロジック。

**変更:**
- `SettingsManager.cs` — `AppSettings` に `SetupCompleted` 追加。初回判定ヘルパ追加。
- `Program.cs` — Load後にウィザード表示分岐。
- `MainWindow.Windows.cs:341` `BtnRenameTarget_Click` — フォルダ移動確認。
- `MainWindow.Capture.cs` `ExecuteCapture` — ToolTip dismiss ＋ 撮影後にツールを前面へ。
- `MainWindow.Preview.cs` `CaptureTempPreview` — プレビュー撮影後にツールを前面へ。
- `MainWindow.xaml.cs`（または `MainWindow.Windows.cs`）— `BringToolToForeground` ヘルパ追加。
- `.github/workflows/release.yml` — `vpk pack` に `--shortcuts None`。

---

## Task 1: 設定モデルに SetupCompleted と初回判定を追加

**Files:**
- Modify: `SettingsManager.cs:8-47`（`AppSettings`）, `SettingsManager.cs`（`SettingsManager` に static ヘルパ追加）
- Test: `Tests/SetupCompletedTests.cs`（新規）

**Interfaces:**
- Produces:
  - `AppSettings.SetupCompleted { get; set; } = false`（bool プロパティ）
  - `static bool SettingsManager.ShouldShowSetupWizard(bool setupCompleted, bool isInstalled)` — 初回ウィザードを出すべきか。`!setupCompleted && isInstalled` のとき true。

- [ ] **Step 1: 失敗するテストを書く**

`Tests/SetupCompletedTests.cs`:
```csharp
using System.Text.Json;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class SetupCompletedTests
    {
        [Fact]
        public void SetupCompletedの既定はfalse()
        {
            var s = new AppSettings();
            Assert.False(s.SetupCompleted);
        }

        [Fact]
        public void SetupCompletedはシリアライズ往復で保持される()
        {
            var s = new AppSettings { SetupCompleted = true };
            string json = JsonSerializer.Serialize(s);
            var back = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.True(back.SetupCompleted);
        }

        [Fact]
        public void 旧設定JSONにフラグが無ければfalse扱い()
        {
            // 既存ユーザーの設定には SetupCompleted が無い
            string json = "{\"SaveDirectory\":\"C:/x\"}";
            var back = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.False(back.SetupCompleted);
        }

        [Theory]
        [InlineData(false, true, true)]   // 未完了＆インストール済み → 出す
        [InlineData(true, true, false)]   // 完了済み → 出さない
        [InlineData(false, false, false)] // ポータブル/開発 → 出さない
        [InlineData(true, false, false)]
        public void 初回ウィザード表示判定(bool completed, bool installed, bool expected)
        {
            Assert.Equal(expected, SettingsManager.ShouldShowSetupWizard(completed, installed));
        }
    }
}
```

- [ ] **Step 2: 失敗を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter SetupCompletedTests`
Expected: コンパイルエラー（`SetupCompleted` / `ShouldShowSetupWizard` 未定義）

- [ ] **Step 3: 実装**

`SettingsManager.cs` の `AppSettings` 末尾（`IconPath` の後、`SettingsManager.cs:46` 付近）に追加:
```csharp
        /// <summary>初回セットアップウィザードを完了したか。false の間は初回起動時に表示する。</summary>
        public bool SetupCompleted { get; set; } = false;
```

`SettingsManager` クラス内（`ResolveSettingsFile` の近く）に追加:
```csharp
        /// <summary>
        /// 初回セットアップウィザードを表示すべきか。
        /// 未完了かつ Velopack インストール済みのときのみ表示する
        /// （ポータブル版・開発実行ではショートカット作成が無意味なため出さない）。
        /// </summary>
        public static bool ShouldShowSetupWizard(bool setupCompleted, bool isInstalled)
            => !setupCompleted && isInstalled;
```

- [ ] **Step 4: テスト成功を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter SetupCompletedTests`
Expected: PASS（4メソッド／Theoryは4ケース）

- [ ] **Step 5: コミット**

```bash
git add SettingsManager.cs Tests/SetupCompletedTests.cs
git commit -m "初回セットアップ判定とSetupCompleted設定を追加

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: ショートカット位置の解決ロジック（純粋）

**Files:**
- Create: `ShortcutLocationResolver.cs`
- Test: `Tests/ShortcutLocationResolverTests.cs`

**Interfaces:**
- Produces:
  - `enum ShortcutChoice { None = 0, Desktop = 1, StartMenu = 2 }`（`[Flags]`）
  - `static class ShortcutLocationResolver`
    - `static ShortcutChoice Resolve(bool desktop, bool startMenu)` — チェック状態を Flags に畳む。
    - `static bool HasAny(ShortcutChoice c)` — いずれか選択されているか。

この enum は SDK 非依存の自前型。SDK の `ShortcutLocation` への変換は Task 3（`ShortcutInstaller`）が担う。こうしてロジックを SDK から切り離しテスト可能にする。

- [ ] **Step 1: 失敗するテストを書く**

`Tests/ShortcutLocationResolverTests.cs`:
```csharp
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class ShortcutLocationResolverTests
    {
        [Fact]
        public void 両方ON()
        {
            var r = ShortcutLocationResolver.Resolve(desktop: true, startMenu: true);
            Assert.Equal(ShortcutChoice.Desktop | ShortcutChoice.StartMenu, r);
            Assert.True(ShortcutLocationResolver.HasAny(r));
        }

        [Fact]
        public void デスクトップのみ()
        {
            var r = ShortcutLocationResolver.Resolve(desktop: true, startMenu: false);
            Assert.Equal(ShortcutChoice.Desktop, r);
        }

        [Fact]
        public void 両方OFFはNone()
        {
            var r = ShortcutLocationResolver.Resolve(desktop: false, startMenu: false);
            Assert.Equal(ShortcutChoice.None, r);
            Assert.False(ShortcutLocationResolver.HasAny(r));
        }
    }
}
```

- [ ] **Step 2: 失敗を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter ShortcutLocationResolverTests`
Expected: コンパイルエラー（型未定義）

- [ ] **Step 3: 実装**

`ShortcutLocationResolver.cs`:
```csharp
using System;

namespace SCtoolGui
{
    /// <summary>作成するショートカットの位置。SDK 非依存の自前 Flags 型。</summary>
    [Flags]
    public enum ShortcutChoice
    {
        None = 0,
        Desktop = 1,
        StartMenu = 2,
    }

    /// <summary>ウィザードのチェック状態をショートカット位置に畳む純粋ロジック。</summary>
    public static class ShortcutLocationResolver
    {
        public static ShortcutChoice Resolve(bool desktop, bool startMenu)
        {
            var c = ShortcutChoice.None;
            if (desktop) c |= ShortcutChoice.Desktop;
            if (startMenu) c |= ShortcutChoice.StartMenu;
            return c;
        }

        public static bool HasAny(ShortcutChoice c) => c != ShortcutChoice.None;
    }
}
```

- [ ] **Step 4: テスト成功を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter ShortcutLocationResolverTests`
Expected: PASS（3メソッド）

- [ ] **Step 5: コミット**

```bash
git add ShortcutLocationResolver.cs Tests/ShortcutLocationResolverTests.cs
git commit -m "ショートカット位置解決ロジックを追加

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: ショートカット作成ラッパ（Velopack SDK）

**Files:**
- Create: `ShortcutInstaller.cs`

**Interfaces:**
- Consumes: `ShortcutChoice`（Task 2）, `AppUpdateService.IsInstalled`（`AppUpdateService.cs:21`）
- Produces:
  - `static class ShortcutInstaller`
    - `static void Create(ShortcutChoice choice, bool isInstalled)` — `isInstalled==false` または `choice==None` なら何もしない。それ以外は Velopack SDK でショートカット作成。

**注意（実装時に確認）:** Velopack の `Velopack.Windows.Shortcuts.CreateShortcutForThisExe(ShortcutLocation)` を使う。`ShortcutLocation` は `Desktop` / `StartMenuRoot` / `StartMenu` / `Startup` / `None` を持つ `[Flags]` 想定（CLI が `Desktop,StartMenuRoot` を受けることから）。ビルドが通らなければ `CreateShortcutForThisExe` を各位置ごとに個別呼び出しする形にフォールバックする。「スタートメニュー」は `StartMenuRoot`（フォルダを作らずルート）にマップする。

このタスクは SDK 呼び出しのため単体テスト対象外。ビルドが通ることと、Task 8 の実機確認で担保する。

- [ ] **Step 1: 実装を書く**

`ShortcutInstaller.cs`:
```csharp
using Velopack.Windows;

namespace SCtoolGui
{
    /// <summary>
    /// ウィザードの選択に従い Velopack でショートカットを作成する薄いラッパ。
    /// Velopack 非管理環境（ポータブル/開発）や未選択のときは何もしない。
    /// </summary>
    public static class ShortcutInstaller
    {
        public static void Create(ShortcutChoice choice, bool isInstalled)
        {
            if (!isInstalled) return;
            if (!ShortcutLocationResolver.HasAny(choice)) return;

            var locations = ToVelopack(choice);
            new Shortcuts().CreateShortcutForThisExe(locations);
        }

        private static ShortcutLocation ToVelopack(ShortcutChoice choice)
        {
            var loc = ShortcutLocation.None;
            if (choice.HasFlag(ShortcutChoice.Desktop)) loc |= ShortcutLocation.Desktop;
            // 「スタートメニュー」はフォルダを作らないルート直下に置く
            if (choice.HasFlag(ShortcutChoice.StartMenu)) loc |= ShortcutLocation.StartMenuRoot;
            return loc;
        }
    }
}
```

- [ ] **Step 2: ビルド確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: 成功。もし `CreateShortcutForThisExe` / `ShortcutLocation` / `Shortcuts` が解決できない、または Flags OR がコンパイルエラーなら、上記「注意」に従い個別呼び出しへ修正して再ビルド。

- [ ] **Step 3: コミット**

```bash
git add ShortcutInstaller.cs
git commit -m "Velopackショートカット作成ラッパを追加

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: セットアップウィザードUI

**Files:**
- Create: `SetupWizardWindow.xaml`, `SetupWizardWindow.xaml.cs`

**Interfaces:**
- Consumes: `ShortcutChoice`（Task 2）
- Produces:
  - `class SetupWizardWindow : Window`
    - コンストラクタ `SetupWizardWindow(string defaultSaveDirectory, bool defaultSaveInWindowNameFolder)`
    - `bool CreateDesktopShortcut { get; }` / `bool CreateStartMenuShortcut { get; }`
    - `string SelectedSaveDirectory { get; }`
    - `bool SaveInWindowNameFolder { get; }`
    - `DialogResult == true` で「完了」。

既存の `RenameWindow.xaml` を色の付け方の参考にしつつ、**直書き色は使わず** `MainWindow.xaml` と同じ `DynamicResource`（`Card` / `CardTitle` / `ToggleSwitch` 等のスタイルがあれば流用）でテーマ連動させる。フォルダ選択は WPF 標準に無いため `Microsoft.Win32.OpenFolderDialog`（.NET 8+ / net10 で利用可）を使う。

- [ ] **Step 1: XAML を書く**

`SetupWizardWindow.xaml`:
```xml
<Window x:Class="SCtoolGui.SetupWizardWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="SCtool のセットアップ" Height="380" Width="460"
        WindowStartupLocation="CenterScreen" ResizeMode="NoResize"
        ShowInTaskbar="True"
        Background="{DynamicResource CardBackgroundFillColorDefaultBrush}">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="ようこそ" FontSize="18" FontWeight="SemiBold"
                   Foreground="{DynamicResource TextFillColorPrimaryBrush}"/>
        <TextBlock Grid.Row="1" Margin="0,4,0,16" TextWrapping="Wrap"
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                   Text="ショートカットの作成と、画像の保存先を選んでください。あとから設定でも変更できます。"/>

        <StackPanel Grid.Row="2" Margin="0,0,0,12">
            <TextBlock Text="ショートカットを作成" FontWeight="SemiBold" Margin="0,0,0,6"
                       Foreground="{DynamicResource TextFillColorPrimaryBrush}"/>
            <CheckBox x:Name="ChkDesktop" Content="デスクトップに作成" IsChecked="True" Margin="0,2"/>
            <CheckBox x:Name="ChkStartMenu" Content="スタートメニューに作成" IsChecked="True" Margin="0,2"/>
        </StackPanel>

        <StackPanel Grid.Row="3" Margin="0,0,0,12">
            <TextBlock Text="画像の保存先" FontWeight="SemiBold" Margin="0,0,0,6"
                       Foreground="{DynamicResource TextFillColorPrimaryBrush}"/>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBox x:Name="TxtSaveDir" Grid.Column="0" IsReadOnly="True"
                         VerticalContentAlignment="Center" Padding="6"/>
                <Button x:Name="BtnBrowse" Grid.Column="1" Content="参照" Margin="8,0,0,0"
                        Padding="14,4" Click="BtnBrowse_Click"/>
            </Grid>
        </StackPanel>

        <CheckBox x:Name="ChkPerAppFolder" Grid.Row="4" Margin="0,0,0,12"
                  Content="アプリごとにフォルダを分けて保存する"/>

        <StackPanel Grid.Row="6" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="完了" Width="100" Height="32" IsDefault="True" Click="BtnDone_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 2: コードビハインドを書く**

`SetupWizardWindow.xaml.cs`:
```csharp
using System.Windows;
using Microsoft.Win32;

namespace SCtoolGui
{
    public partial class SetupWizardWindow : Window
    {
        public bool CreateDesktopShortcut => ChkDesktop.IsChecked == true;
        public bool CreateStartMenuShortcut => ChkStartMenu.IsChecked == true;
        public string SelectedSaveDirectory { get; private set; }
        public bool SaveInWindowNameFolder => ChkPerAppFolder.IsChecked == true;

        public SetupWizardWindow(string defaultSaveDirectory, bool defaultSaveInWindowNameFolder)
        {
            InitializeComponent();
            SelectedSaveDirectory = defaultSaveDirectory;
            TxtSaveDir.Text = defaultSaveDirectory;
            ChkPerAppFolder.IsChecked = defaultSaveInWindowNameFolder;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog { Title = "画像の保存先を選択" };
            if (!string.IsNullOrEmpty(SelectedSaveDirectory))
                dlg.InitialDirectory = SelectedSaveDirectory;
            if (dlg.ShowDialog() == true)
            {
                SelectedSaveDirectory = dlg.FolderName;
                TxtSaveDir.Text = dlg.FolderName;
            }
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
```

- [ ] **Step 3: ビルド確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: 成功。使用する DynamicResource キーは `MainWindow.xaml` で実在確認済み: `CardBackgroundFillColorDefaultBrush` / `TextFillColorPrimaryBrush` / `TextFillColorSecondaryBrush`。存在しないキーは実行時に既定色になるだけでビルドは通るが、テーマ連動のため必ず実在キーを使う。

- [ ] **Step 4: コミット**

```bash
git add SetupWizardWindow.xaml SetupWizardWindow.xaml.cs
git commit -m "初回セットアップウィザードUIを追加

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: 起動フローにウィザードを組み込む

**Files:**
- Modify: `Program.cs:25-34`（設定Load部の直後）

**Interfaces:**
- Consumes: `SettingsManager.ShouldShowSetupWizard`（Task 1）, `SetupWizardWindow`（Task 4）, `ShortcutInstaller.Create`（Task 3）, `AppUpdateService.IsInstalled`（`AppUpdateService.cs:21`）, `ShortcutLocationResolver.Resolve`（Task 2）

**実装確定:** ウィザードのテーマ(Fluent)ブラシは `App.xaml` のマージ辞書由来のため、`App.InitializeComponent()` の**後**に出す必要がある。よって `var app = new App(); app.InitializeComponent();` の直後、`app.Run();` の前に分岐を置く（当初案の「SingleInstance 前」ではテーマ未読込で色が既定になる）。

- [ ] **Step 1: 実装**

`Program.cs` の `app.InitializeComponent();` の直後、`app.Run();` の前に追加:
```csharp
            // 初回セットアップウィザード（インストール版の初回のみ）。
            try
            {
                var setupSettings = new SettingsManager();
                setupSettings.Load();
                bool isInstalled = new AppUpdateService().IsInstalled;

                if (SettingsManager.ShouldShowSetupWizard(setupSettings.Current.SetupCompleted, isInstalled))
                {
                    var wizard = new SetupWizardWindow(
                        setupSettings.Current.SaveDirectory,
                        setupSettings.Current.SaveInWindowNameFolder);

                    if (wizard.ShowDialog() == true)
                    {
                        setupSettings.Current.SaveDirectory = wizard.SelectedSaveDirectory;
                        setupSettings.Current.SaveInWindowNameFolder = wizard.SaveInWindowNameFolder;
                        setupSettings.Current.SetupCompleted = true;
                        setupSettings.Save();

                        var choice = ShortcutLocationResolver.Resolve(
                            wizard.CreateDesktopShortcut, wizard.CreateStartMenuShortcut);
                        ShortcutInstaller.Create(choice, isInstalled);
                    }
                    // キャンセル/閉じるは SetupCompleted=false のまま（次回再表示）
                }
            }
            catch { }
```

- [ ] **Step 2: ビルド確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: 成功。

- [ ] **Step 3: 実機スモーク（開発実行ではスキップされること）**

Run: `dotnet run --project SCtoolGui.csproj`
Expected: `dotnet run` は `IsInstalled==false` なのでウィザードは出ず、通常のメイン画面が起動する（`ShouldShowSetupWizard` が false）。起動を確認したら閉じる。

- [ ] **Step 4: コミット**

```bash
git add Program.cs
git commit -m "起動時に初回セットアップウィザードを表示する分岐を追加

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: 名前変更時のフォルダ移動判定（純粋ロジック）

**Files:**
- Create: `FolderRenamePlanner.cs`
- Modify: `Tests/SCtoolGui.Tests.csproj`（`<Compile Include="..\FolderRenamePlanner.cs" .../>` を追加。テストプロジェクトはプロジェクト参照ではなくソースをリンクして取り込む方式のため必須）
- Test: `Tests/FolderRenamePlannerTests.cs`

**Interfaces:**
- Consumes: `FileNameUtil.ToSafeName`（`FileNameUtil.cs:18`）
- Produces:
  - `enum FolderRenameAction { NoMove, Move, ConflictSkip }`
  - `record FolderRenamePlan(FolderRenameAction Action, string? OldPath, string? NewPath)`
  - `static class FolderRenamePlanner`
    - `static FolderRenamePlan Plan(string baseDir, string oldName, string newName, bool perAppFolder, Func<string,bool> exists)`
      - `perAppFolder==false` → `NoMove`
      - 安全名が同じ → `NoMove`
      - 旧フォルダが存在しない → `NoMove`
      - 旧存在＆新も存在 → `ConflictSkip`
      - 旧存在＆新なし → `Move`（OldPath/NewPath 設定）

`exists` を注入することで `Directory.Exists` に依存せずテストする。

- [ ] **Step 1: 失敗するテストを書く**

`Tests/FolderRenamePlannerTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class FolderRenamePlannerTests
    {
        private static Func<string,bool> ExistsIn(params string[] paths)
        {
            var set = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
            return p => set.Contains(p);
        }

        [Fact]
        public void フォルダ分けOFFなら移動しない()
        {
            var plan = FolderRenamePlanner.Plan(@"C:\base", "旧", "新", perAppFolder: false, ExistsIn());
            Assert.Equal(FolderRenameAction.NoMove, plan.Action);
        }

        [Fact]
        public void 旧フォルダが無ければ移動しない()
        {
            var plan = FolderRenamePlanner.Plan(@"C:\base", "旧", "新", perAppFolder: true, ExistsIn());
            Assert.Equal(FolderRenameAction.NoMove, plan.Action);
        }

        [Fact]
        public void 旧のみ存在すれば移動()
        {
            string old = Path.Combine(@"C:\base", FileNameUtil.ToSafeName("旧"));
            var plan = FolderRenamePlanner.Plan(@"C:\base", "旧", "新", perAppFolder: true, ExistsIn(old));
            Assert.Equal(FolderRenameAction.Move, plan.Action);
            Assert.Equal(old, plan.OldPath);
            Assert.Equal(Path.Combine(@"C:\base", FileNameUtil.ToSafeName("新")), plan.NewPath);
        }

        [Fact]
        public void 新も存在すれば衝突スキップ()
        {
            string old = Path.Combine(@"C:\base", FileNameUtil.ToSafeName("旧"));
            string neu = Path.Combine(@"C:\base", FileNameUtil.ToSafeName("新"));
            var plan = FolderRenamePlanner.Plan(@"C:\base", "旧", "新", perAppFolder: true, ExistsIn(old, neu));
            Assert.Equal(FolderRenameAction.ConflictSkip, plan.Action);
        }

        [Fact]
        public void 安全名が同一なら移動しない()
        {
            string old = Path.Combine(@"C:\base", FileNameUtil.ToSafeName("a/b"));
            // "a/b" と "a:b" は安全名が同じ "a_b"
            var plan = FolderRenamePlanner.Plan(@"C:\base", "a/b", "a:b", perAppFolder: true, ExistsIn(old));
            Assert.Equal(FolderRenameAction.NoMove, plan.Action);
        }
    }
}
```

- [ ] **Step 2: 失敗を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter FolderRenamePlannerTests`
Expected: コンパイルエラー（型未定義）

- [ ] **Step 3: 実装（＋Testsプロジェクトにリンク追加）**

`Tests/SCtoolGui.Tests.csproj` の `<Compile Include>` 群に追加:
```xml
    <Compile Include="..\FolderRenamePlanner.cs" Link="Source\FolderRenamePlanner.cs" />
```

`FolderRenamePlanner.cs`:
```csharp
using System;
using System.IO;

namespace SCtoolGui
{
    public enum FolderRenameAction { NoMove, Move, ConflictSkip }

    public record FolderRenamePlan(FolderRenameAction Action, string? OldPath, string? NewPath);

    /// <summary>
    /// 名前変更（呼び名変更）時に、旧名の画像フォルダを新名へ移動すべきか判定する純粋ロジック。
    /// アプリごとフォルダ分けが有効で、旧フォルダが在り、新フォルダが未作成のときだけ移動する。
    /// </summary>
    public static class FolderRenamePlanner
    {
        public static FolderRenamePlan Plan(
            string baseDir, string oldName, string newName, bool perAppFolder, Func<string, bool> exists)
        {
            if (!perAppFolder)
                return new FolderRenamePlan(FolderRenameAction.NoMove, null, null);

            string oldSafe = FileNameUtil.ToSafeName(oldName);
            string newSafe = FileNameUtil.ToSafeName(newName);
            if (string.Equals(oldSafe, newSafe, StringComparison.OrdinalIgnoreCase))
                return new FolderRenamePlan(FolderRenameAction.NoMove, null, null);

            string oldPath = Path.Combine(baseDir, oldSafe);
            string newPath = Path.Combine(baseDir, newSafe);

            if (!exists(oldPath))
                return new FolderRenamePlan(FolderRenameAction.NoMove, null, null);
            if (exists(newPath))
                return new FolderRenamePlan(FolderRenameAction.ConflictSkip, oldPath, newPath);

            return new FolderRenamePlan(FolderRenameAction.Move, oldPath, newPath);
        }
    }
}
```

- [ ] **Step 4: テスト成功を確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj --filter FolderRenamePlannerTests`
Expected: PASS（5メソッド）

- [ ] **Step 5: コミット**

```bash
git add FolderRenamePlanner.cs Tests/FolderRenamePlannerTests.cs
git commit -m "名前変更時のフォルダ移動判定ロジックを追加

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: 名前変更ハンドラにフォルダ移動を組み込む

**Files:**
- Modify: `MainWindow.Windows.cs:341-363`（`BtnRenameTarget_Click`）

**Interfaces:**
- Consumes: `FolderRenamePlanner.Plan`（Task 6）, `_settingsManager.Current.SaveDirectory` / `SaveInWindowNameFolder`

`BtnRenameTarget_Click` の `DisplayName` 変更・保存の**前**に、旧名フォルダの移動計画を立て、`Move` なら確認ダイアログ→`Directory.Move`。`ConflictSkip` はログのみ。移動の可否に関わらず `DisplayName` 変更と保存は従来通り行う。

- [ ] **Step 1: 実装**

`MainWindow.Windows.cs` の `BtnRenameTarget_Click`（`:355` `string oldName = ...` から `:359` の SaveAndLog まで）を次に置き換え:
```csharp
            string oldName = target.DisplayName;
            if (dialog.ResultName == oldName) return;
            string newName = dialog.ResultName;

            // アプリごとフォルダ分けが有効なら、旧名フォルダを新名へ移せるか判定する
            var plan = FolderRenamePlanner.Plan(
                _settingsManager.Current.SaveDirectory, oldName, newName,
                _settingsManager.Current.SaveInWindowNameFolder,
                System.IO.Directory.Exists);

            if (plan.Action == FolderRenameAction.Move)
            {
                var ask = MessageBox.Show(
                    $"既存の画像フォルダ「{oldName}」を新しい名前「{newName}」に移動しますか？\n" +
                    "「いいえ」を選ぶと、以後は新しい名前のフォルダに保存されます（旧フォルダは残ります）。",
                    "画像フォルダの移動", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ask == MessageBoxResult.Yes)
                {
                    try
                    {
                        System.IO.Directory.Move(plan.OldPath!, plan.NewPath!);
                        Log($"画像フォルダを [{oldName}] から [{newName}] へ移動しました。");
                    }
                    catch (Exception ex)
                    {
                        Log($"【警告】画像フォルダの移動に失敗しました: {ex.Message}");
                    }
                }
            }
            else if (plan.Action == FolderRenameAction.ConflictSkip)
            {
                Log($"【注意】新しい名前「{newName}」のフォルダが既にあるため、旧フォルダの移動は行いませんでした。");
            }

            target.DisplayName = newName;
            SaveAndLog($"呼び名を [{oldName}] から [{target.DisplayName}] に変更しました。");
```

（`MessageBox` / `MessageBoxButton` 等は `System.Windows`。`MainWindow.Windows.cs` は既に `System.Windows` を using 済みか実装時に確認し、無ければ追加する。）

- [ ] **Step 2: ビルド確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: 成功。

- [ ] **Step 3: コミット**

```bash
git add MainWindow.Windows.cs
git commit -m "名前変更時に旧画像フォルダを確認のうえ移動する

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 8: ツール前面復帰ヘルパ＋撮影後の復帰＋ToolTip写り込み対策

**Files:**
- Modify: `MainWindow.xaml.cs`（`BringToolToForeground` ヘルパ追加）, `MainWindow.Capture.cs`（`ExecuteCapture`）, `MainWindow.Preview.cs`（`CaptureTempPreview`）

**Interfaces:**
- Produces: `private void BringToolToForeground()` — 自ウィンドウ(MainWindow)を確実に前面へ。`Activate()` に加え Win32 `SetForegroundWindow(自ハンドル)` を呼ぶ。

`ScreenCapture` は前面化して撮るだけで戻さない。撮影を行う両経路（本番/プレビュー）の直後にツールを前面へ戻し、あわせてキャプチャ前に ToolTip を閉じる。ToolTip はマウスがボタン上にある間表示され続け、対象ウィンドウに重なると写り込むため、キャプチャ実行の入口で強制的に閉じる。

- [ ] **Step 1: 前面復帰ヘルパを追加**

`MainWindow.xaml.cs` に Win32 宣言とヘルパを追加（クラス内、既存 using に `System.Windows.Interop` と `System.Runtime.InteropServices` を追加）:
```csharp
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(System.IntPtr hWnd);

        /// <summary>撮影のため対象を前面化した後、ツール自身を前面へ戻す。</summary>
        private void BringToolToForeground()
        {
            try
            {
                Activate();
                var h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (h != System.IntPtr.Zero) SetForegroundWindow(h);
            }
            catch { }
        }
```

- [ ] **Step 2: ToolTip 強制クローズのヘルパを追加**

`MainWindow.Capture.cs` の `ExecuteCapture` の直前（クラス内）に追加:
```csharp
        /// <summary>表示中の ToolTip を閉じ、対象ウィンドウへの写り込みを防ぐ。</summary>
        private void DismissOpenToolTip()
        {
            // キャプチャボタンの ToolTip を一時的に無効化→再有効化して確実に閉じる
            var btn = BtnCapture;
            if (btn?.ToolTip is System.Windows.Controls.ToolTip tip)
            {
                tip.IsOpen = false;
            }
            System.Windows.Controls.ToolTipService.SetIsEnabled(btn, false);
            // 描画に反映させる（1サイクル回す）
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.Render);
            System.Windows.Controls.ToolTipService.SetIsEnabled(btn, true);
        }
```

（`BtnCapture` はキャプチャボタンの x:Name。実装時に `MainWindow.xaml` で実際のボタン名を確認し合わせる。`ToolTip` が文字列直指定の場合 `is ToolTip` は false になるので、その場合は `ToolTipService.SetIsEnabled` の一時無効化のみで閉じる。）

- [ ] **Step 3: ExecuteCapture に組み込む**

`MainWindow.Capture.cs` の `ExecuteCapture()` 内、`try {` の直後（`:77`）に ToolTip クローズを追加し、キャプチャ完了後（`ShowPreview`/クリップボード処理の後、`:111` 付近）に前面復帰を追加:

`try {` 直後:
```csharp
                DismissOpenToolTip();
```

`ExecuteCapture` の `catch` の直前（成功パス末尾、クリップボードコピーの後）:
```csharp
                    // 撮影のため対象を前面化したので、ツールを前面へ戻す
                    BringToolToForeground();
```

- [ ] **Step 4: CaptureTempPreview に組み込む**

`MainWindow.Preview.cs` の `CaptureTempPreview`、`ShowPreview(TempPreviewPath, isTempPreview: true);`（`:51`）の直後に追加:
```csharp
                // プレビューのため対象を前面化したので、ツールを前面へ戻す
                BringToolToForeground();
```

- [ ] **Step 5: ビルド確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: 成功。

- [ ] **Step 6: 実機確認（verify）**

Run: `dotnet run --project SCtoolGui.csproj`
手順:
1. ツールを対象ウィンドウに重ねて配置し、ウィンドウ指定を切り替える → 対象が一瞬出ても最後にツールが前面へ戻ることを確認。
2. キャプチャボタンにマウスを乗せ ToolTip を出した状態でキャプチャ → 保存画像/プレビューに ToolTip や別ウィンドウが写り込まないことを確認。
3. キャプチャ後にツールが前面へ戻ることを確認。

- [ ] **Step 7: コミット**

```bash
git add MainWindow.xaml.cs MainWindow.Capture.cs MainWindow.Preview.cs
git commit -m "撮影後にツールを前面へ戻し、キャプチャ時のToolTip写り込みを防止

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 9: ビルド設定で自動ショートカット作成を無効化

**Files:**
- Modify: `.github/workflows/release.yml:53-60`（`vpk pack` ステップ）

**Interfaces:** なし（CIビルド設定）

- [ ] **Step 1: `--shortcuts None` を追加**

`.github/workflows/release.yml` の `vpk pack` ステップ（`:54-60`）に `--shortcuts None` を追加:
```yaml
      - name: Velopack パッケージを作成
        run: >
          vpk pack
          --packId SCtoolGui
          --packVersion ${{ steps.ver.outputs.version }}
          --packDir publish
          --mainExe SCtoolGui.exe
          --packTitle SCtoolGui
          --shortcuts None
```

- [ ] **Step 2: YAML 妥当性の目視確認**

Run: `git diff .github/workflows/release.yml`
Expected: `--shortcuts None` の1行のみ追加。インデントが既存の `--packTitle` 行と揃っていること。

- [ ] **Step 3: コミット**

```bash
git add .github/workflows/release.yml
git commit -m "インストール時の自動ショートカット作成を無効化(初回ウィザードで作成)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 10: 全体ビルド＆全テスト＆実機確認

**Files:** なし（検証）

- [ ] **Step 1: 全テスト**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj`
Expected: すべて PASS（既存 + 新規 SetupCompleted/ShortcutLocationResolver/FolderRenamePlanner）。

- [ ] **Step 2: リリース相当ビルド**

Run: `dotnet publish SCtoolGui.csproj -c Release -r win-x64 --self-contained true -p:Version=1.0.5 -o "%TEMP%/sctool-verify"`
（Git Bash では `-o "$TEMP/sctool-verify"`）
Expected: 成功し `SCtoolGui.exe` が生成される。

- [ ] **Step 3: 実機確認（名前変更フォルダ移動）**

Run: `dotnet run --project SCtoolGui.csproj`
手順: アプリごとフォルダ分けON→あるアプリで1枚撮影しフォルダ作成→名前変更→「移動しますか？」で「はい」→旧フォルダが新名へ移動し中身が残ることを確認。

- [ ] **Step 4: 最終コミット（必要なら）**

検証で修正が入った場合のみ該当ファイルをコミット。修正なしなら不要。

---

## 実装後の完了確認

- [ ] 4テーマ（A: ウィザード / B: フォルダ移動 / C: ToolTip / D: 前面復帰）が spec 通り動く
- [ ] `dotnet test` 全 PASS
- [ ] リリースは未実施（タグ push はユーザー判断。`release-flow-tag-push-ci` 参照）
