# リファクタリングTODO: refactor/settings-devmode

`refactor/full-cleanup`(フォルダ整理済み)から派生。設定・開発者モード系が対象。
このファイルは作業完了後、`refactor/full-cleanup` にマージする前に削除する。

## 対象ファイル
- Logic/SettingsManager.cs
- Logic/DeveloperModeGate.cs
- Views/SettingsWindow.xaml / SettingsWindow.xaml.cs

## コード整理
- 上記ファイルを対象に重複・命名・簡素化(`/simplify` 相当)をレビューして適用する
- SettingsWindow.xaml.cs のコンストラクタ引数が多い(20個近い bool/string の羅列)ので、
  設定値をまとめたオブジェクト渡しにできないか検討する(MainWindow側の呼び出しと合わせて要確認)

## テスト強化
- 既にテストあり(現状維持でOK): DeveloperModeGateTests, DeveloperKeyPathTests, SettingsPathTests,
  PreviewSettingsTests, MigrationTests, RealDataMigrationTest, SetupCompletedTests
- テストなし:
  - Views/SettingsWindow.xaml.cs … `UpdateDeveloperTabVisibility` など判定ロジックがコードビハインドに
    直書きされている。テスト容易な形(Logicへの切り出し、または純粋関数化)を検討する

## 完了条件
- `dotnet build SCtoolGui.csproj` 成功
- `dotnet test Tests/SCtoolGui.Tests.csproj` 全パス
- このファイルを削除してコミットしてから `refactor/full-cleanup` へマージ
