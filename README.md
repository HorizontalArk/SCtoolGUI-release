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
- [.NET 10 SDK](https://dotnet.microsoft.com/) 以降（ソースからビルドする場合）

## ビルドと実行

```
dotnet build
```

ビルド後、以下の実行ファイルが生成されます。

```
bin\Debug\net10.0-windows\SCtoolGui.exe
```

## ライセンス

[MIT License](LICENSE) の下で公開しています。
