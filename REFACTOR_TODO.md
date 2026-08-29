# リファクタリングTODO: refactor/capture

`refactor/full-cleanup`(フォルダ整理済み)から派生。撮影・ファイル名・コピー系が対象。
このファイルは作業完了後、`refactor/full-cleanup` にマージする前に削除する。

## 対象ファイル
- Logic/CaptureFileName.cs
- Logic/FileBaseNameResolver.cs
- Logic/FileNameUtil.cs
- Logic/CopyButtonState.cs
- Logic/CopyTargetResolver.cs
- Logic/FolderRenamePlanner.cs
- Services/ScreenCapture.cs
- Views/RenameWindow.xaml / RenameWindow.xaml.cs

## コード整理
- 上記ファイルを対象に重複・命名・簡素化(`/simplify` 相当)をレビューして適用する
- 特にLogic配下の小さなクラス間で似た処理(ファイル名生成・パス組み立てなど)が重複していないか確認

## テスト強化
- 既にテストあり(現状維持でOK): CaptureFileNameTests, FileBaseNameResolverTests, FileNameUtilTests,
  CopyButtonStateTests, CopyTargetResolverTests, FolderRenamePlannerTests
- テストなし:
  - Services/ScreenCapture.cs … OS/GDI依存で直接の単体テストは難しい。テスト可能なロジック(トリミング範囲計算など)が
    埋まっていないか確認し、あれば抽出してLogicへ切り出しテストを書く
  - Views/RenameWindow.xaml.cs … コードビハインドのイベントハンドラのみなら現状維持で可。判定ロジックが
    埋まっていればLogicへ切り出す

## 完了条件
- `dotnet build SCtoolGui.csproj` 成功
- `dotnet test Tests/SCtoolGui.Tests.csproj` 全パス
- このファイルを削除してコミットしてから `refactor/full-cleanup` へマージ
