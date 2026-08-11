# 初回セットアップUX修正 + タスクバーアイコン反映 設計

作成日: 2026-08-12

## 背景・問題

実機検証（v1.0.7 インストール版）で、初回セットアップウィザードとアイコン機能に複数の欠陥が判明した。

### 判明した根本原因（すべて実機で確定）

1. **完了ボタンの見切れ**（修正済み・別コミット済み）
   `Window.Height="380"` はタイトルバー込みの全体高。`ResizeMode="NoResize"` だとクライアント領域が 341px しかなく、完了ボタン下端 355px が画面外に 14px 見切れていた。`SizeToContent="Height" + MinHeight="380"` で解消済み。

2. **ウィザードが背面に隠れ、メインが同時表示・モーダルロックされない**
   `Program.cs` が `app.Run()` の**前**に `wizard.ShowDialog()` を呼んでいる。一方 `App.OnStartup`（= `app.Run()` で発火）が `MainWindow.Show()` する。この結果、1プロセスでメインとウィザードが同時に存在し、モーダルが機能せず、起動時プレビュー撮影の前面化でウィザードが背面へ回る。ユーザーはウィザードの存在に気づけず、完了を押せない → `SetupCompleted` が永久に false のまま毎回表示される。

3. **アイコン変更がタスクバーに反映されない**
   タスクバーのアイコンは、実行中ウィンドウの `WM_SETICON`（当初の修正実装）ではなく、**ショートカット `.lnk` の `IconLocation`** が支配する。ピン留め `.lnk` の `IconLocation` が `SCtoolGui.exe,0`（exe埋め込み app.ico = 青カメラ）に固定されているため、ウィンドウアイコンをいくら変えてもタスクバー表示は変わらない。`.lnk` を書き換えれば変わることを実機で確認（ただしアイコンキャッシュのため即時反映されず、Explorer 再起動＋キャッシュ削除で反映された）。

### 確定した事実
- `WM_SETICON` はウィンドウ/タイトルバー/Alt+Tab には効くが、タスクバー表示には無関係。
- タスクバー表示 = `.lnk` の `IconLocation`。`.ico` 形式が確実（PNG 等は不可のことがある）。
- 完了ボタンを押せば `SetupCompleted=true` は正しく保存され、再起動でウィザードは出ない（問題2の解消が前提）。
- `app.ico`（青カメラ）はデフォルトアイコンで、リポジトリ内・git 追跡済み・正規。ユーザー独自画像はリポジトリ外。

## 方針

### A. セットアップウィザードのモーダル化・前面化

- ウィザード表示ロジックを `Program.cs` から `App.OnStartup` 内へ移す。
- `MainWindow` を生成・`Show()` した**直後**に、`wizard.Owner = mainWindow` を設定して `ShowDialog()` する（メイン表示直後に即モーダル）。
- これにより背後のメインは自動的にモーダル無効化される。表示前に `Activate()` で前面化を試みる（まずは Owner モーダル + Activate。ダメなら AttachThreadInput 併用へ強化）。
- ウィザード完了/キャンセル後の保存・ショートカット作成処理も `App` 側へ移設する。
- 起動時プレビュー撮影の前面化（`MainWindow.Loaded` での `BringToolToForeground`）がウィザードのモーダルを奪わないよう、ウィザード表示中は起動時前面化を抑制、またはウィザード完了後に行うよう順序を調整する。

### B. タスクバーアイコンの反映（.lnk 方式）

- **当初の WM_SETICON 実装は撤回する**。`MainWindow` の `ApplyTaskbarIcon` / `CreateHIcon` / 関連 P/Invoke・`OnSourceInitialized` のアイコン再適用・`_iconApplyPending` / `_bigIcon` / `_smallIcon` などを削除。`this.Icon` によるウィンドウ/タイトルバー反映のみ残す。
- 新規コンポーネント（仮 `TaskbarIconUpdater`）を追加する:
  - ユーザーが設定画面で選んだ画像（PNG/JPG/ico）を **`.ico` に変換**し、**`%AppData%\SCtoolGui\`** に保存する（例: `%AppData%\SCtoolGui\user_icon.ico`）。既存 `cut_settings.json` と同じ場所。リポジトリ・配布物には**一切含めない**。
  - **既知の全 `.lnk`** を走査し、`IconLocation` をこの `.ico` に更新する:
    - スタートメニュー: `%AppData%\Microsoft\Windows\Start Menu\Programs\SCtoolGui.lnk`
    - デスクトップ: `%UserProfile%\Desktop\SCtoolGui.lnk`
    - タスクバーピン留め: `%AppData%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\SCtoolGui.lnk`
  - 書換後 `SHChangeNotify`（SHCNE_ASSOCCHANGED）でシェルに通知する。
  - **反映タイミング**: 即時反映は保証しない。`SHChangeNotify` のみ行い、次回起動/サインインで確実に反映される。
- アイコンを未設定（空）に戻したら、各 `.lnk` の `IconLocation` を **exe 埋め込み（`current\SCtoolGui.exe,0` = app.ico 青カメラ）** に戻す。

### 制約（重要）

- ユーザーの独自アイコン画像および変換後 `.ico` は **`%AppData%\SCtoolGui` にのみ** 置く。リポジトリ（`SCtoolGUI\` 配下）・配布物・git 追跡対象に一切書き込まない。

## コンポーネント

- `App.xaml.cs` (`OnStartup`): MainWindow 表示 → ウィザードをモーダル表示 → 完了時の保存・ショートカット作成。
- `Program.cs`: ウィザード関連ロジックを App へ移譲し、Velopack フック・多重起動ガード・昇格・`app.Run()` のみ担う。
- `MainWindow`（`.xaml.cs` / `.Capture.cs`）: WM_SETICON 実装を撤去。`this.Icon` 設定のみ。設定変更時にアイコン `.lnk` 更新を呼ぶ。
- `TaskbarIconUpdater`（新規）: 画像→`.ico` 変換、`.lnk` 群の `IconLocation` 更新、`SHChangeNotify`、既定復帰。
- `IconConverter`（新規または上記に内包）: 任意画像→複数解像度 `.ico`（System.Drawing 非依存を優先。WPF の RenderTargetBitmap ベース）。

## エラーハンドリング

- `.lnk` が存在しない場所はスキップ（ピン留めしていない等は正常系）。
- `.ico` 変換失敗・書込失敗は握りつぶさずログするが、アプリ起動やその他機能は止めない。
- `%AppData%\SCtoolGui` 書込不可時はアイコン更新を諦め、ウィンドウアイコン（`this.Icon`）だけは維持する。

## テスト

- **ウィザード**: モーダル性（背後メインが無効化される）、完了で `SetupCompleted=true` 保存、再起動で非表示。純粋ロジック（表示判定 `ShouldShowSetupWizard`）は既存テストを維持。
- **アイコン変換**: 任意サイズ PNG → 正方 `.ico`（16/32/48/256 等）を生成できる。
- **.lnk 更新**: 指定 `.lnk` の `IconLocation` が期待パスに更新される／存在しない `.lnk` はスキップ／空設定で既定（app.ico）へ戻る。純粋な走査・パス決定ロジックを単体テスト可能に分離する。
- UI/シェル反映（実タスクバー）は実機目視で確認（自動化困難）。

## 非目標（YAGNI）

- 即時タスクバー反映のための Explorer 自動再起動やアイコンキャッシュ強制クリアは実装しない（副作用が大きい）。次回起動での反映に委ねる。
- ユーザーが手動でピン留めした未知の場所の探索はしない。既知の標準パスのみ対象。
