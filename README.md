# SCtoolGui

指定したウィンドウをホットキーで撮影する、Windows 向けのスクリーンショットツールです。

## 主な機能

- 撮影対象のウィンドウを選んでホットキーで撮影
- 撮影前に対象ウィンドウを前面化し、成否を確認してから撮影
- 保存先フォルダの指定、ウィンドウ名ごとのフォルダ分け
- JPEG 品質の指定
- 常に最前面表示（トップモースト）

## 動作環境

- Windows 10 / 11

## インストール

[Releases](https://github.com/HorizontalArk/SCtoolGUI-release/releases) から最新の `Setup.exe` をダウンロードして実行してください。ユーザー領域にインストールされ、スタートメニューに登録されます。新しいバージョンが出た場合は、アプリ内の「更新」通知からワンクリックで更新できます。

## ソースからビルドする場合

[.NET 10 SDK](https://dotnet.microsoft.com/) 以降が必要です。

```
dotnet build
```

## ライセンス

[MIT License](LICENSE) の下で公開しています。
