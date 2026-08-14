# ファイル名のウィンドウタイトル対応 & コピー対象の二択 設計

作成日: 2026-08-15
関連: 詳細設定トグルの見た目刷新は [ui-redesign-direction](2026-08-09-ui-redesign-direction.md)（案A: Win11 Fluent）の方向に沿う

## 背景と目的

3つの改善を1つの設定改修としてまとめて行う。詳細設定ウィンドウ（`SettingsWindow`）を同時に触るため、UIの刷新も併せて実施する。

1. **ファイル名をウィンドウタイトルに一致できるようにする**
   現状、保存名はフォルダ名とファイル名の両方が「登録した表示名（`DisplayName`）」で共通。ファイル名だけを撮影時点の実ウィンドウタイトルにできる設定を追加したい。フォルダ名は登録名のままで良い（フォルダは自由に変えられるので不要）。デフォルトは今まで通り登録名。

2. **クリップボードのコピー対象を選べるようにする**
   現状のコピーボタンは常に「最後に保存した本番画像（`_lastCapturedPath`）」をコピーする。一時プレビュー（撮影前の確認用画像）を表示中でも古い保存画像がコピーされ、「見えているものと違う」ズレが起きる。コピー対象を「一時プレビュー」と「最後に保存した画像」から選べるようにする。デフォルトは詳細設定で持ち、コピーボタンにプルダウン（スプリットボタン）を付けてその場でも二択できる。

3. **詳細設定トグル群の視覚刷新**
   詳細設定の素のWPFチェックボックス群（最前面固定など）を Win11 Fluent 風トグルスイッチに統一する。今回追加する2項目も同じスタイルで揃える。

## 用語の確定

コード上の呼称とユーザー呼称を一致させる。

| 本設計での呼称 | コード上の実体 | 説明 |
|---|---|---|
| 一時プレビュー | `TempPreviewPath`（`SCtool_temp_preview.jpg`） | 撮影前の確認用画像。透かしはUIオーバーレイのみでファイルには焼き込まれない |
| 最後に保存した画像 | `_lastCapturedPath` | 直近に撮影・保存した本番JPG |
| 登録名 / 表示名 | `CurrentTarget.DisplayName` → `CurrentFolderName` | ユーザーが登録したウィンドウの表示名。安全化済み |
| ウィンドウタイトル | `selected.Title` | 撮影時点の生のウィンドウタイトル。刻々変化しうる |

## スコープ

**今回（この spec）:**
- A. ファイル名に実ウィンドウタイトルを使う設定（デフォルトOFF）
- B. コピー対象の二択（設定デフォルト + スプリットボタンでの明示選択）
- C. 詳細設定トグル群のFluent風スイッチ化（今回追加分も含む）

**今回スコープ外:**
- メインウィンドウ全体のUI刷新（[ui-redesign-direction] 本体）。今回は詳細設定ウィンドウのトグルと、メイン側のコピーボタン単体に限定する。

---

## A. ファイル名に実ウィンドウタイトルを使う

### 現状の把握
- `MainWindow.Capture.cs:104` で `string safeName = CurrentFolderName;` を取得し、フォルダ名（`Path.Combine(baseDir, safeName)`）にもファイル名（`$"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"`, `:113`）にも同じ値を使う。
- `CurrentFolderName`（`MainWindow.Windows.cs:440`）は `DisplayName` を `FileNameUtil.ToSafeName` に通したもの。
- `selected`（`WindowItem`）は `selected.Title` に撮影時点のウィンドウタイトルを持つ。

### 変更
- `AppSettings` に `bool UseWindowTitleForFileName { get; set; } = false;` を追加（デフォルトOFF＝今まで通り登録名）。
- `ExecuteCapture` で、**フォルダ名は常に `CurrentFolderName`（登録名）** のまま。**ファイル名のベース名だけ**を分岐する:
  - ON かつ `selected.Title` が非空 → `FileNameUtil.ToSafeName(selected.Title)`
  - それ以外（OFF、またはタイトルが空） → `CurrentFolderName`（フォールバック）
- タイムスタンプ付与（`_yyyyMMdd_HHmmss`）は従来通りなので、タイトルが変化しても同名衝突・上書きは起きない。

### 保存パスの例
```
…/Screenshots/[MyGame/]<ファイル名ベース>_20260815_143022.jpg
                └ フォルダ: 常に登録名（SaveInWindowNameFolder が ON のときのみ付く）
                            <ファイル名ベース>: ON=生タイトル / OFF=登録名
```

### 実装メモ
- ファイル名ベースを決める小さなヘルパを設ける想定（例: `ResolveFileBaseName(WindowItem selected)`）。テストしやすいよう、`ToSafeName` 適用と空フォールバックの分岐をここに閉じる。

---

## B. コピー対象の二択（一時プレビュー / 最後に保存した画像）

### 現状の把握
- `MainWindow.Preview.cs:201` `CopyPreviewToClipboard(bool isAuto)` は `HasLastCapture`（＝`_lastCapturedPath` の存在）を見て `Clipboard.SetImage(LoadBitmap(_lastCapturedPath))` する。一時プレビュー表示中でも対象は本番保存画像。
- 手動コピーは `BtnCopyClipboard_Click`（`:195`）から `isAuto:false` で呼ぶ。
- 撮影直後の自動コピーは `ExecuteCapture`（`MainWindow.Capture.cs:130`）から `AutoCopyClipboard` 設定に応じて `isAuto:true` で呼ぶ。
- 一時プレビューのファイルパスは `TempPreviewPath`（`MainWindow.Preview.cs:19`）。`SavePreviewOnly` は透かしを焼き込まず素のJPGを保存するので、これをコピーしても透かしは入らない。

### 変更

**設定:**
- `AppSettings` に `string CopySource { get; set; } = "LastSaved";` を追加（`"LastSaved"` / `"TempPreview"`）。詳細設定でデフォルトを選ぶ。

**コピー処理の一般化:**
- コピー対象パスを決めるロジックを切り出す（例: `ResolveCopyPath(string source)`）:
  - `"TempPreview"` → `TempPreviewPath`（`File.Exists` の場合のみ有効）
  - `"LastSaved"` → `_lastCapturedPath`（`HasLastCapture` の場合のみ有効）
- `CopyToClipboard(string source, bool isAuto)` に一般化。対象が存在すればコピー、存在しなければログで警告（例:「一時プレビューがまだありません」）。
- 既存 `CopyPreviewToClipboard(isAuto)` は「設定 `CopySource` のデフォルトでコピー」を意味する薄いラッパにする。

**UI（スプリットボタン）:**
- メインウィンドウのコピーボタンをスプリットボタン化する。左「コピー」本体 + 右「▽」ドロップの2つの独立したクリック領域（ヒットテスト）を持つ。
  - 左「コピー」本体クリック → 詳細設定の既定（`CopySource`）でコピー。
  - 右「▽」→ メニューを開き、「一時プレビューをコピー」「最後に保存した画像をコピー」を明示選択して即コピー。メニュー選択は設定を変更せず、その場限りの上書き実行。
- 対象が存在しない選択肢は無効表示（グレーアウト）またはクリック時にログ警告。

**自動コピー:**
- 撮影後の自動コピー（`AutoCopyClipboard`）は従来通り、保存直後の本番画像（`_lastCapturedPath`）を対象とする。`CopySource` 設定の影響を受けない（撮影直後は一時プレビューより本番画像が正しいため）。

---

## C. 詳細設定トグル群のFluent風スイッチ化

### 現状の把握
- `SettingsWindow.xaml` の設定は素の `CheckBox`（`ChkAppTopmost` / `ChkSaveInWindowFolder` / `ChkResetSettingsOnWindowChange` / `ChkAutoCopyClipboard` / `ChkPlayShutterSound` / `ChkAlwaysRunAsAdmin`）。
- 受け渡しはコンストラクタ引数と `Result*` プロパティ方式（`SettingsWindow.xaml.cs`）。呼び出しは `MainWindow.xaml.cs:451` 付近。

### 変更方針
- WPFの `Style`/`ControlTemplate` で `CheckBox` を Win11 Fluent 風トグルスイッチの見た目にする。**機能・受け渡しロジック（`IsChecked` / `Result*`）は変えず、見た目だけ差し替える**ため、追加項目も同じ Style を当てるだけで揃う。
- 今回追加する2項目も同スタイルで並べる。A のファイル名設定はON/OFFのトグルスイッチ。B の `CopySource` は「一時プレビュー / 最後に保存した画像」の2択なので、トグルではなくコンボボックス（既存の `CmbTheme` 等と同じ見た目）で表現する。
- テーマ（Light/Dark/System）に追従する配色。既存のテーマ機構に合わせる。

### モック
操作可能なモックで左「コピー」/右「▽」のヒットテスト、ファイル名プレビュー、Fluentトグルを確認済み（ユーザー承認済み）。

---

## データ・設定のまとめ（`AppSettings` 追加分）

| プロパティ | 型 | デフォルト | 意味 |
|---|---|---|---|
| `UseWindowTitleForFileName` | `bool` | `false` | ファイル名を実ウィンドウタイトルにする。OFFで登録名 |
| `CopySource` | `string` | `"LastSaved"` | コピーの既定対象。`"LastSaved"` / `"TempPreview"` |

- `SettingsWindow` のコンストラクタ引数と `Result*` プロパティに上記2項目を追加。
- 既存JSONに項目が無い場合はデフォルト値が入る（`System.Text.Json` の既定挙動）ため、追加のマイグレーションは不要。

## エラー処理・エッジケース

- ファイル名: `selected.Title` が空 → 登録名にフォールバック。`ToSafeName` で無効文字置換・長さ制限・末尾トリム済み。
- コピー: 対象ファイルが存在しない → コピーせずログ警告。既存の try/catch（`CopyPreviewToClipboard`）を踏襲。
- 自動コピー: 保存直後に本番画像が確実に存在する経路のみ通す（従来通り）。

## テスト方針

- **A**: ファイル名ベース決定ヘルパの単体テスト（ON/OFF × タイトル空/非空 × 無効文字）。既存 `FileNameUtilTests` と同じ枠組み。
- **B**: コピー対象パス決定ヘルパの単体テスト（`CopySource` 値 × 各ファイルの存在/非存在）。`Clipboard` 実操作はUI依存のため、パス決定ロジックのみ切り出してテスト。
- **C**: 見た目の変更のため自動テスト対象外。実機でトグルの見た目・テーマ追従・On/Off の受け渡しを目視確認（[ui-verify-uipi-and-clipping] の注意点に留意）。
- 実機確認: ファイル名がON/OFFで期待通り変わること、スプリットボタンの左/右クリックがそれぞれ正しく動くこと、一時プレビュー表示中に「一時プレビューをコピー」で見えている画像がコピーされること。
