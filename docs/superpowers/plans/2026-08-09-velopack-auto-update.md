# Velopack 自動更新への移行 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** git ベースの自作アップデータを廃し、Velopack による「Setup.exe インストール＋アプリ内ワンクリック更新」に移行する。

**Architecture:** WPF より前に `VelopackApp.Build().Run()` を実行する明示的エントリポイントを設ける。更新は `AppUpdateService` が public releases repo を参照して確認し、既存の「バナー＋ボタン」UXでDL・適用・再起動する。設定ファイルは更新でフォルダが変わっても失われないよう `%AppData%` へ移す。CI はタグ push で `vpk` によりインストーラを生成・公開する。

**Tech Stack:** .NET 10 / WPF, Velopack（NuGet + `vpk` グローバルツール）, GitHub Actions。

## Global Constraints

- 対象フレームワーク: `net10.0-windows`（WPF, `WinExe`）。
- コメントは日本語で書く（既存コードに合わせる）。
- 更新元 releases repo URL: `https://github.com/HorizontalArk/SCtoolGUI-release`（＝現在の開発リポジトリ）。
- Velopack ではアプリを単一ファイル化しない（`PublishSingleFile` を使わない）。`vpk` がフォルダをパッケージ化する。
- 作業ブランチは `dev`。各タスク末尾でコミットする。
- ビルド確認: `dotnet build SCtoolGui.csproj -c Debug`。テスト: `dotnet test Tests/SCtoolGui.Tests.csproj`。

---

### Task 1: 設定ファイルを %AppData% へ移し、旧位置から移行する

**Files:**
- Modify: `SettingsManager.cs`（`_settingsFile` の決定方法を差し替え、移行用の public static メソッドを追加）
- Test: `Tests/SettingsPathTests.cs`（新規）

**Interfaces:**
- Produces: `public static string SettingsManager.ResolveSettingsFile(string appDataRoot, string legacyDir)` — `appDataRoot/SCtoolGui/cut_settings.json` を返す。フォルダを作成し、返り値のファイルが無く `legacyDir/cut_settings.json` が在れば一度だけコピーする。

- [ ] **Step 1: 失敗するテストを書く**

`Tests/SettingsPathTests.cs` を新規作成:

```csharp
using System.IO;
using SCtoolGui;

namespace SCtoolGui.Tests
{
    public class SettingsPathTests
    {
        // 各テストで隔離された一時ディレクトリを使う
        private static (string appData, string legacy) MakeTempDirs()
        {
            string root = Path.Combine(Path.GetTempPath(), "sctool_test_" + Path.GetRandomFileName());
            string appData = Path.Combine(root, "appdata");
            string legacy = Path.Combine(root, "legacy");
            Directory.CreateDirectory(appData);
            Directory.CreateDirectory(legacy);
            return (appData, legacy);
        }

        [Fact]
        public void 新パスは_AppData配下のSCtoolGui_cut_settings_json()
        {
            var (appData, legacy) = MakeTempDirs();

            string path = SettingsManager.ResolveSettingsFile(appData, legacy);

            Assert.Equal(Path.Combine(appData, "SCtoolGui", "cut_settings.json"), path);
            Assert.True(Directory.Exists(Path.Combine(appData, "SCtoolGui")));
        }

        [Fact]
        public void 旧位置に設定があれば新パスへ移行される()
        {
            var (appData, legacy) = MakeTempDirs();
            File.WriteAllText(Path.Combine(legacy, "cut_settings.json"), "{\"HotkeyKey\":83}");

            string path = SettingsManager.ResolveSettingsFile(appData, legacy);

            Assert.True(File.Exists(path));
            Assert.Contains("83", File.ReadAllText(path));
        }

        [Fact]
        public void 新パスに既存があれば旧位置で上書きしない()
        {
            var (appData, legacy) = MakeTempDirs();
            string newPath = Path.Combine(appData, "SCtoolGui", "cut_settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.WriteAllText(newPath, "NEW");
            File.WriteAllText(Path.Combine(legacy, "cut_settings.json"), "OLD");

            SettingsManager.ResolveSettingsFile(appData, legacy);

            Assert.Equal("NEW", File.ReadAllText(newPath));
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj`
Expected: コンパイルエラー（`ResolveSettingsFile` 未定義）で失敗。

- [ ] **Step 3: 実装を追加**

`SettingsManager.cs` の `_settingsFile` 定義（42行目付近）を差し替える:

```csharp
        private readonly string _settingsFile = ResolveSettingsFile(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDomain.CurrentDomain.BaseDirectory);

        /// <summary>
        /// 設定ファイルの保存先を決める。Velopackはバージョンごとに別フォルダへ
        /// インストールするため、exe隣ではなく %AppData%\SCtoolGui に置く。
        /// 旧位置（exe隣）に設定が在れば一度だけ移行する。
        /// </summary>
        public static string ResolveSettingsFile(string appDataRoot, string legacyDir)
        {
            string dir = Path.Combine(appDataRoot, "SCtoolGui");
            Directory.CreateDirectory(dir);
            string newPath = Path.Combine(dir, "cut_settings.json");

            if (!File.Exists(newPath))
            {
                string legacy = Path.Combine(legacyDir, "cut_settings.json");
                if (File.Exists(legacy))
                {
                    try { File.Copy(legacy, newPath); } catch { }
                }
            }
            return newPath;
        }
```

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj`
Expected: 全テスト PASS（既存38 + 新規3）。

- [ ] **Step 5: コミット**

```bash
git add SettingsManager.cs Tests/SettingsPathTests.cs
git commit -m "設定ファイルを%AppData%へ移し旧位置から移行する"
```

---

### Task 2: Velopack を追加し、更新フックを実行するエントリポイントを設ける

**Files:**
- Modify: `SCtoolGui.csproj`（Velopack 参照と `<StartupObject>` を追加）
- Create: `Program.cs`

**Interfaces:**
- Produces: `SCtoolGui.Program.Main` — アプリのエントリポイント。`VelopackApp.Build().Run()` を最初に実行し、その後 WPF `App` を起動する。

- [ ] **Step 1: Velopack パッケージを追加**

Run: `dotnet add SCtoolGui.csproj package Velopack`
Expected: `SCtoolGui.csproj` に `<PackageReference Include="Velopack" Version="..." />` が追加される。

- [ ] **Step 2: エントリポイントを指定**

`SCtoolGui.csproj` の最初の `<PropertyGroup>` 内（`<ApplicationManifest>` の次の行）に追加:

```xml
    <StartupObject>SCtoolGui.Program</StartupObject>
```

（WPF が自動生成する `App.Main` と、これから作る `Program.Main` の二重エントリを、`StartupObject` で `Program` に確定させる。）

- [ ] **Step 3: Program.cs を作成**

`Program.cs` を新規作成:

```csharp
using System;
using Velopack;

namespace SCtoolGui
{
    /// <summary>
    /// アプリのエントリポイント。Velopack のインストール/更新フックは
    /// WPF が立ち上がる前に処理しないといけないため、ここで最初に Run する。
    /// フック実行時（インストール直後など）は Velopack 側が処理して即終了する。
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
```

- [ ] **Step 4: ビルドが通ることを確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: ビルド成功（エントリポイントの曖昧さエラーが出ない）。

- [ ] **Step 5: dev 実行で起動を確認**

Run: `dotnet run --project SCtoolGui.csproj`
Expected: MainWindow が表示され、クラッシュしない（未インストールなので Velopack フックは何もしない）。確認したらウィンドウを閉じる。

- [ ] **Step 6: コミット**

```bash
git add SCtoolGui.csproj Program.cs
git commit -m "Velopackを追加し更新フック実行のエントリポイントを設ける"
```

---

### Task 3: AppUpdateService（Velopack ラッパー）を作る

**Files:**
- Create: `AppUpdateService.cs`

**Interfaces:**
- Consumes: `Velopack.UpdateManager`, `Velopack.Sources.GithubSource`, `Velopack.UpdateInfo`。
- Produces:
  - `bool AppUpdateService.IsInstalled`
  - `string? AppUpdateService.CurrentVersion`
  - `Task<UpdateInfo?> AppUpdateService.CheckAsync()`
  - `Task AppUpdateService.DownloadAndApplyAsync(UpdateInfo info)`（戻らず再起動する）

- [ ] **Step 1: AppUpdateService.cs を作成**

```csharp
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace SCtoolGui
{
    /// <summary>Velopack による自動更新のラッパー。</summary>
    public class AppUpdateService
    {
        private const string ReleasesRepoUrl = "https://github.com/HorizontalArk/SCtoolGUI-release";
        private readonly UpdateManager _mgr;

        public AppUpdateService()
        {
            // prerelease は取り込まない（第3引数 false）。公開repoなのでトークン不要（null）。
            _mgr = new UpdateManager(new GithubSource(ReleasesRepoUrl, null, false));
        }

        /// <summary>Velopack でインストールされた状態か。dev 実行時は false。</summary>
        public bool IsInstalled => _mgr.IsInstalled;

        /// <summary>現在のバージョン文字列。未インストール時は null。</summary>
        public string? CurrentVersion => _mgr.CurrentVersion?.ToString();

        /// <summary>更新があれば UpdateInfo を返す。無ければ null。</summary>
        public Task<UpdateInfo?> CheckAsync() => _mgr.CheckForUpdatesAsync();

        /// <summary>更新をDLして適用し、アプリを再起動する。この呼び出しからは戻らない。</summary>
        public async Task DownloadAndApplyAsync(UpdateInfo info)
        {
            await _mgr.DownloadUpdatesAsync(info);
            _mgr.ApplyUpdatesAndRestart(info);
        }
    }
}
```

- [ ] **Step 2: ビルドが通ることを確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: ビルド成功（Velopack の型が解決される）。

- [ ] **Step 3: コミット**

```bash
git add AppUpdateService.cs
git commit -m "Velopackラッパー AppUpdateService を追加"
```

---

### Task 4: MainWindow を AppUpdateService に繋ぎ替える

**Files:**
- Modify: `MainWindow.xaml.cs`（`CheckUpdates()` 140-152行、`BtnUpdate_Click` 154-167行、フィールド 12-13行付近）

**Interfaces:**
- Consumes: `AppUpdateService`（Task 3）, `Velopack.UpdateInfo`。

- [ ] **Step 1: using とフィールドを追加**

`MainWindow.xaml.cs` 冒頭の using 群に追加:

```csharp
using Velopack;
```

フィールド定義（12行目 `_settingsManager` の下）に追加:

```csharp
        private readonly AppUpdateService _updateService = new AppUpdateService();
        private UpdateInfo? _pendingUpdate;
```

- [ ] **Step 2: CheckUpdates() を差し替える**

既存の `CheckUpdates()`（140-152行）を丸ごと次に置き換える:

```csharp
        private async void CheckUpdates()
        {
            // Velopack でインストールされていない（dev実行など）場合は更新確認しない。
            if (!_updateService.IsInstalled)
            {
                Log("更新確認: インストール版ではないためスキップしました。");
                return;
            }

            Log("アップデートを確認中...");
            try
            {
                _pendingUpdate = await _updateService.CheckAsync();
                if (_pendingUpdate != null)
                {
                    UpdateBanner.Visibility = Visibility.Visible;
                    Log("【通知】新しいアップデートがあります。ボタンから更新できます。");
                }
                else
                {
                    Log("アプリケーションは最新です。");
                }
            }
            catch
            {
                // ネットワーク不通などは黙って諦める（起動を妨げない）。
                Log("更新の確認に失敗しました（ネットワーク等）。");
            }
        }
```

- [ ] **Step 3: BtnUpdate_Click を差し替える**

既存の `BtnUpdate_Click`（154-167行）を丸ごと次に置き換える:

```csharp
        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate == null) return;

            try
            {
                Log("アップデートをダウンロードして適用します...");
                BtnUpdate.IsEnabled = false;
                await _updateService.DownloadAndApplyAsync(_pendingUpdate);
                // 成功時はここに戻らず再起動する。
            }
            catch (Exception ex)
            {
                BtnUpdate.IsEnabled = true;
                MessageBox.Show($"アップデートに失敗しました:\n{ex.Message}", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
```

- [ ] **Step 4: ビルドが通ることを確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: ビルド成功。`UpdateManager`（旧）への参照が MainWindow から消えている。

- [ ] **Step 5: dev 実行で確認**

Run: `dotnet run --project SCtoolGui.csproj`
Expected: 起動し、ログに「インストール版ではないためスキップ」と出る。バナーは出ない。クラッシュしない。確認したら閉じる。

- [ ] **Step 6: コミット**

```bash
git add MainWindow.xaml.cs
git commit -m "MainWindowの更新処理をVelopackベースに繋ぎ替え"
```

---

### Task 5: 旧 git アップデータを撤去する

**Files:**
- Delete: `UpdateManager.cs`, `update.bat`

- [ ] **Step 1: 旧ファイルを削除**

```bash
git rm UpdateManager.cs update.bat
```

- [ ] **Step 2: 参照が残っていないか確認**

Run: `dotnet build SCtoolGui.csproj -c Debug`
Expected: ビルド成功（`UpdateManager` への参照がどこにも残っていない）。もしエラーが出たら、その箇所の旧 API 呼び出しを消す。

- [ ] **Step 3: テスト全体が通ることを確認**

Run: `dotnet test Tests/SCtoolGui.Tests.csproj`
Expected: 全テスト PASS。

- [ ] **Step 4: コミット**

```bash
git add -A
git commit -m "gitベースの旧アップデータ(UpdateManager/update.bat)を撤去"
```

---

### Task 6: CI ワークフローを Velopack(vpk) に置換する

**Files:**
- Modify: `.github/workflows/release.yml`（全面置換）

- [ ] **Step 1: release.yml を Velopack 版に置き換える**

`.github/workflows/release.yml` の内容を次で全置換:

```yaml
# v* タグを push すると、Velopack でインストーラ一式を作って Release に公開する。
name: Release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

jobs:
  build:
    runs-on: windows-latest

    steps:
      - name: リポジトリを取得
        uses: actions/checkout@v4

      - name: .NET SDK をセットアップ
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: vpk ツールを導入
        run: dotnet tool install -g vpk

      - name: バージョン番号を算出
        id: ver
        shell: pwsh
        run: |
          $v = "${{ github.ref_name }}".TrimStart('v')
          "version=$v" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8

      # Velopack は単一ファイル化しない。フォルダ一式を発行する。
      - name: 発行（self-contained フォルダ）
        run: >
          dotnet publish SCtoolGui.csproj
          -c Release
          -r win-x64
          --self-contained true
          -p:Version=${{ steps.ver.outputs.version }}
          -o publish

      # 既存リリースを取得しておくと、差分パッケージが生成される（初回は何も無くてOK）。
      - name: 既存リリースを取得（差分の土台）
        run: >
          vpk download github
          --repoUrl https://github.com/HorizontalArk/SCtoolGUI-release
          --token ${{ secrets.GITHUB_TOKEN }}
        continue-on-error: true

      - name: Velopack パッケージを作成
        run: >
          vpk pack
          --packId SCtoolGui
          --packVersion ${{ steps.ver.outputs.version }}
          --packDir publish
          --mainExe SCtoolGui.exe
          --packTitle SCtoolGui

      - name: GitHub Release へ公開
        run: >
          vpk upload github
          --repoUrl https://github.com/HorizontalArk/SCtoolGUI-release
          --token ${{ secrets.GITHUB_TOKEN }}
          --tag ${{ github.ref_name }}
          --releaseName ${{ github.ref_name }}
          --publish
```

- [ ] **Step 2: YAML の健全性を目視確認**

`name:` が1回、`on:`/`permissions:`/`jobs:` が各1回であることを確認（前回の重複事故防止）。

Run: `git diff .github/workflows/release.yml`
Expected: 旧 zip ステップが消え、`vpk download/pack/upload` に置き換わっている。

- [ ] **Step 3: コミットして push（workflow スコープPATで通る）**

```bash
git add .github/workflows/release.yml
git commit -m "CIをVelopack(vpk)ベースのリリースに置換"
git push origin dev
```

Expected: push 成功（PAT に workflow スコープがあるため弾かれない）。

---

### Task 7: README のインストール手順を更新する

**Files:**
- Modify: `README.md`

- [ ] **Step 1: インストール節を書き換える**

`README.md` の「ビルドと実行」節を、配布物の入手手順に置き換える:

```markdown
## インストール

[Releases](https://github.com/HorizontalArk/SCtoolGUI-release/releases) から最新の `Setup.exe` をダウンロードして実行してください。ユーザー領域にインストールされ、スタートメニューに登録されます。新しいバージョンが出た場合は、アプリ内の「更新」通知からワンクリックで更新できます。

## ソースからビルドする場合

[.NET 10 SDK](https://dotnet.microsoft.com/) 以降が必要です。

```
dotnet build
```
```

- [ ] **Step 2: コミット**

```bash
git add README.md
git commit -m "READMEをSetup.exe配布の手順に更新"
```

---

### Task 8: v0.0.1 を撤去し、v1.0.0 を出して E2E 検証する

**Files:**（コード変更なし。リリース操作と検証）

- [ ] **Step 1: テスト用 v0.0.1 を削除**

GitHub の Releases 画面で `v0.0.1` の Release を削除。続いてタグを削除:

```bash
git push origin :refs/tags/v0.0.1
git tag -d v0.0.1
```

- [ ] **Step 2: dev を main へ統合**

```bash
git checkout main
git merge --ff-only dev
git push origin main
```

（`--ff-only` が失敗する場合は `git merge dev` で統合してから push。）

- [ ] **Step 3: v1.0.0 タグを打って push**

```bash
git tag v1.0.0
git push origin v1.0.0
```

- [ ] **Step 4: CI の成否を確認**

Run: `curl -s "https://api.github.com/repos/HorizontalArk/SCtoolGUI-release/actions/runs?per_page=1"`
Expected: 最新 run が `status=completed`, `conclusion=success` になるまで待つ。

- [ ] **Step 5: リリース資産を確認**

Run: `curl -s "https://api.github.com/repos/HorizontalArk/SCtoolGUI-release/releases/tags/v1.0.0"`
Expected: `Setup.exe`、`*-full.nupkg`、`releases.win.json`（および `RELEASES`）が資産に含まれる。

- [ ] **Step 6: 手動インストールと更新の E2E（受け入れ基準）**

1. `Setup.exe` をダウンロードして実行 → アプリが起動することを確認。
2. コードに小さな変更（例: ログ文言）を加え、`v1.0.1` タグを push。CI 成功を確認。
3. インストール済みアプリを起動 → 「更新あり」バナー → 「アップデートして再起動」ボタン → 新バージョンで再起動されることを確認。
4. 設定（保存先やホットキー）が更新後も保持されていることを確認。

- [ ] **Step 7: 完了をメモリへ記録**

`distribution-public-release-repo` メモリの「残タスク」から C を外し、完了として更新する。

---

## Self-Review

- **Spec coverage:** 設計の各項目 — エントリポイント(Task2)、AppUpdateService(Task3)、UI繋ぎ替え(Task4)、設定移設(Task1)、旧updater撤去(Task5)、CI置換(Task6)、README(Task7)、v0.0.1撤去とv1.0.0のE2E(Task8) — すべてタスクに対応。エラー処理は Task4 の CheckUpdates/BtnUpdate_Click に、未インストール分岐も同所に含む。
- **Placeholder scan:** TBD/TODO なし。コードは各ステップに実物を記載。Velopack と vpk のバージョンはコマンドで最新解決（意図的にピン留めしない）。
- **Type consistency:** `ResolveSettingsFile(appDataRoot, legacyDir)`、`AppUpdateService.{IsInstalled,CurrentVersion,CheckAsync,DownloadAndApplyAsync}`、`_pendingUpdate: UpdateInfo?` はタスク間で一致。
