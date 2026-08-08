# Velopack による自動更新への移行 — 設計

- 日付: 2026-08-09
- 対象: SCtoolGui（WPF / .NET 10）
- 目的: git ベースの自作アップデータを廃し、利用者が **git も .NET SDK も持たずに**「ワンクリック更新」できる配布へ移行する。

## 決定事項（合意済み）

- **方式: Approach A（フル Velopack・インストーラ一本化）**。プレーンzip配布は廃止。
- **更新UX: 通知＋ワンクリック**。起動時に更新を確認し、あれば `UpdateBanner` を表示、ボタンでDL＋適用＋再起動。
- **初回正式リリース: v1.0.0**。テスト用の `v0.0.1`（プレーンzip、Velopack非互換）は Release・タグとも削除してから始める。
- 配布/更新元は既存の public repo **`HorizontalArk/SCtoolGUI-release`**（＝現在の開発リポジトリ）。公開repoなのでクライアント側の更新確認に認証は不要。

## 全体像

利用者は `Setup.exe` を1回実行 → `%LocalAppData%` にインストール＆スタートメニュー登録。以後は新版が出るとアプリ内で「更新あり」→クリックで差分DL＆再起動。開発側はコードを書き、`vX.Y.Z` タグを push すると CI がインストーラ一式を作って Release に公開する。

## 構成要素

| 対象 | 変更 |
|---|---|
| `SCtoolGui.csproj` | `Velopack` パッケージ参照を追加。Velopack のため **PublishSingleFile は使わない**（Velopack が自前でパッケージ化する）。 |
| `Program.cs`（新規） | 明示的エントリポイント。`[STAThread] Main` の先頭で `VelopackApp.Build().Run();` を実行し、その後に WPF `App` を起動する。WPF の自動 Main 生成を止めるため、`App.xaml` のビルドアクションを ApplicationDefinition→Page に変更し、`<StartupObject>SCtoolGui.Program</StartupObject>` を設定する。 |
| `AppUpdateService.cs`（新規） | Velopack の `UpdateManager` + `GithubSource(releases repo)` をラップ。API: `bool IsInstalled`、`string? CurrentVersion`、`Task<UpdateInfo?> CheckAsync()`、`Task DownloadAndApplyAsync(UpdateInfo)`。クラス名は Velopack の `UpdateManager` と衝突させない。 |
| `MainWindow.xaml.cs` | `CheckUpdates()` と `BtnUpdate_Click` を `AppUpdateService` に繋ぎ替え。バナー＋ボタンのUXは維持。チェックで得た `UpdateInfo` を保持し、ボタン押下で使う。 |
| `SettingsManager.cs` | 設定保存先を exe隣 → `%AppData%\SCtoolGui\cut_settings.json` に変更（下記）。 |
| 削除 | `UpdateManager.cs`（git版）、`update.bat`、`auto_restart_updater.bat` 生成ロジック。`setup.bat`（開発ビルド用）は残す。 |
| `.github/workflows/release.yml` | `publish→zip→release` を Velopack の `download→pack→upload` に置換（下記）。 |

## 更新フロー

```
起動
 └ Program.Main: VelopackApp.Build().Run()   # 更新直後のフック処理はここで完結・即終了しうる
 └ WPF起動 → MainWindow
     └ CheckUpdates()（起動時・非同期）
         ├ AppUpdateService.IsInstalled == false（dev実行 / 未インストール）→ 何もしない
         └ CheckAsync()
             ├ 例外/更新なし → バナー出さずログのみ
             └ 更新あり(UpdateInfo) → _pendingUpdate に保持し UpdateBanner を表示
     └ [利用者] 更新ボタン
         └ DownloadAndApplyAsync(_pendingUpdate)  # 差分DL→適用→新版で再起動
```

- 2回目以降は**差分パッケージ**のみDL（初回 `Setup.exe` は約60MB、以後は小さい）。

## 設定ファイルの移設と移行

- 理由: Velopack はバージョンごとの別フォルダにアプリを置くため、exe隣の `cut_settings.json` は**更新のたびに失われる**。安定した per-user 位置へ移す。
- 新パス: `Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), "SCtoolGui", "cut_settings.json")`。読み書き前にフォルダを作成。
- 移行: 新パスが無く、旧パス（`BaseDirectory\cut_settings.json`）が在れば初回にコピー。旧ファイルは消さない（既存の移行方針に合わせ、旧版で開き直しても失わないため）。
- `shutter.mp3` は変更不要（バージョンごとのアプリフォルダに毎回同梱され、`BaseDirectory` から読める）。
- スクリーンショットの保存先（利用者が選ぶ `SaveDirectory`）は影響なし。

## CI/CD ワークフロー（release.yml 差し替え）

- トリガ: `push` タグ `v*`。`permissions: contents: write`。runner: `windows-latest`。
- ステップ:
  1. checkout / setup-dotnet `10.0.x`
  2. `dotnet tool install -g vpk`
  3. バージョン算出（タグ `vX.Y.Z` → `X.Y.Z`）
  4. `dotnet publish SCtoolGui.csproj -c Release -r win-x64 --self-contained true -p:Version=<ver> -o publish`（**PublishSingleFile なし**）
  5. `vpk download github --repoUrl <releases repo> --token $GITHUB_TOKEN`（既存リリースを取得して差分生成の土台に。初回は何も無くてOK）
  6. `vpk pack --packId SCtoolGui --packVersion <ver> --packDir publish --mainExe SCtoolGui.exe --packTitle SCtoolGui`
  7. `vpk upload github --repoUrl <releases repo> --token $GITHUB_TOKEN --tag v<ver> --releaseName v<ver> --publish`
- トークン: 同一repoなので **`${{ secrets.GITHUB_TOKEN }}`**（`contents: write`）で足りる想定。不足時のみ PAT に切替。

## エラー処理・エッジケース

- ネットワーク不通 / GitHub 到達不可: `CheckAsync` を try-catch。バナーは出さずログのみ。
- リリースが1件も無い: 更新なし扱い（バナー出さない）。
- 未インストール（`dotnet run` や dev ビルド）: `IsInstalled == false` で更新UIをスキップ。
- DL/適用の失敗: catch して MessageBox 表示、現行版のまま継続。

## テスト

- 単体（既存 Tests プロジェクトに追加）: `SettingsManager` の保存先解決と、旧→新パスの移行ロジック。
- 手動E2E（受け入れ基準）: `v1.0.0` タグ→ CI がインストーラ生成 → `Setup.exe` でインストール → 続けて `v1.0.1` を出す → アプリ内の更新ボタンで新版へ入れ替わることを確認。

## 事前の一回だけの準備（利用者側の作業）

- `release.yml` を Velopack 版に書き換える push には **`workflow` スコープ付き認証**が要る（現在のGCM資格情報には無い）。実装前に `repo`+`workflow` スコープの PAT を資格情報に入れておくと、以後の workflow 編集がすべて通る。用意できない場合は今回も Web UI 経由で差し替える（ただし内容が長いので PAT 推奨）。

## スコープ外（今回やらない）

- コード署名（SignPath）による SmartScreen 警告解消 — 別タスク。
- ポータブルzipの併存 — YAGNI。
