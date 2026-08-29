# リファクタリングTODO: refactor/window-management

`refactor/full-cleanup`(フォルダ整理済み)から派生。ウィンドウ検出・配置系が対象。
このファイルは作業完了後、`refactor/full-cleanup` にマージする前に削除する。

## 対象ファイル
- Logic/WindowMatcher.cs
- Logic/WindowItem.cs
- Logic/WindowPlacementLogic.cs
- Logic/TargetInfo.cs
- Services/WindowManager.cs
- Views/MainWindow.Windows.cs

## コード整理
- 上記ファイルを対象に重複・命名・簡素化(`/simplify` 相当)をレビューして適用する
- Services/WindowManager.cs と Views/MainWindow.Windows.cs の間で責務(Win32呼び出し vs UI反映)が
  混ざっていないか確認する

## テスト強化
- 既にテストあり(現状維持でOK): WindowItemTests, WindowMatcherTests, WindowPlacementLogicTests
- テストなし:
  - Logic/TargetInfo.cs … 単純なデータ構造ならテスト不要。判定/変換ロジックがあれば追加を検討
  - Services/WindowManager.cs … Win32のウィンドウ列挙依存で直接テストは難しい。フィルタ・整形など
    テスト可能な部分をLogicへ抽出できないか確認
  - Views/MainWindow.Windows.cs … コードビハインド内の判定ロジックがあればLogicへ切り出す

## 完了条件
- `dotnet build SCtoolGui.csproj` 成功
- `dotnet test Tests/SCtoolGui.Tests.csproj` 全パス
- このファイルを削除してコミットしてから `refactor/full-cleanup` へマージ
