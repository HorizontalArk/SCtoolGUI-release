# リファクタリングTODO: refactor/app-shell

`refactor/full-cleanup`(フォルダ整理済み)から派生。起動・テーマ・フォーカス・ログ・MainWindow本体が対象。
このファイルは作業完了後、`refactor/full-cleanup` にマージする前に削除する。

## 対象ファイル
- Program.cs
- Views/App.xaml / App.xaml.cs
- Services/ThemeManager.cs
- Services/HotKeyManager.cs
- Services/SingleInstance.cs
- Logic/FocusRestoreLogic.cs
- Logic/PreviewOrientationLogic.cs
- Logic/LogFormatter.cs
- Services/LogMessages.cs
- Views/MainWindow.xaml / MainWindow.xaml.cs
- Views/MainWindow.Capture.cs / MainWindow.Input.cs / MainWindow.Preview.cs / MainWindow.Windows.cs
- Views/SetupWizardWindow.xaml / SetupWizardWindow.xaml.cs

## コード整理
- 最優先: MainWindow.xaml.cs + 4つのpartial(Capture/Input/Preview/Windows)。分量が多く、
  責務が肥大化していないか(イベントハンドラに判定ロジックが直書きされていないか)を重点的に確認する
- 上記ファイル全体で重複・命名・簡素化(`/simplify` 相当)をレビューして適用する

## テスト強化
- 既にテストあり(現状維持でOK): FocusRestoreLogicTests, PreviewOrientationLogicTests, LogFormatterTests
- テストなし:
  - Program.cs, Views/App.xaml.cs … エントリポイント。薄ければテスト不要
  - Services/ThemeManager.cs, Services/HotKeyManager.cs, Services/SingleInstance.cs … OS依存。
    純粋ロジック部分を抽出できないか確認
  - Services/LogMessages.cs … resxアクセサ。基本的にテスト不要
  - Views/MainWindow.*.cs … 判定・整形ロジックが埋まっていればLogicへ切り出してテストを書く
  - Views/SetupWizardWindow.xaml.cs … 初回セットアップの分岐ロジックがあれば切り出しを検討

## 完了条件
- `dotnet build SCtoolGui.csproj` 成功
- `dotnet test Tests/SCtoolGui.Tests.csproj` 全パス
- このファイルを削除してコミットしてから `refactor/full-cleanup` へマージ
