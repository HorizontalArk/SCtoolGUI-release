# リファクタリングTODO: refactor/update-shortcut

`refactor/full-cleanup`(フォルダ整理済み)から派生。更新・ショートカット・アイコン系が対象。
このファイルは作業完了後、`refactor/full-cleanup` にマージする前に削除する。

## 対象ファイル
- Logic/UpdateFlow.cs
- Services/AppUpdateService.cs
- Logic/ShortcutLocationResolver.cs
- Logic/ShortcutIconUpdater.cs
- Services/ShortcutInstaller.cs
- Logic/IconIcoWriter.cs
- Services/ProcessIconCache.cs

## コード整理
- 上記ファイルを対象に重複・命名・簡素化(`/simplify` 相当)をレビューして適用する
- Services/AppUpdateService.cs と Logic/UpdateFlow.cs の役割分担(Velopack呼び出し vs 純粋な判定)が
  きちんと分かれているか確認する

## テスト強化
- 既にテストあり(現状維持でOK): UpdateFlowTests, ShortcutLocationResolverTests,
  ShortcutIconTargetsTests, IconIcoWriterTests
- テストなし:
  - Services/AppUpdateService.cs … Velopack/ネットワーク依存で直接の単体テストは難しい。
    バージョン比較・prerelease判定などの純粋ロジックがあればUpdateFlowやLogicへ抽出してテストを書く
  - Services/ShortcutInstaller.cs … .lnk作成などOS依存。パス組み立て等のテスト可能部分を確認
  - Services/ProcessIconCache.cs … キャッシュの追加/取得ロジックが純粋な部分があればテスト化を検討

## 完了条件
- `dotnet build SCtoolGui.csproj` 成功
- `dotnet test Tests/SCtoolGui.Tests.csproj` 全パス
- このファイルを削除してコミットしてから `refactor/full-cleanup` へマージ
