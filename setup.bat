@echo off
chcp 65001 > nul
cd /d "%~dp0"

echo ===================================================
echo  SCtoolGui - 初期環境セットアップ
echo ===================================================
echo.

rem 1. .NET SDK がインストールされているかチェック
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo 【エラー】.NET SDK が見つかりませんでした。
    echo 本ツールのビルドには .NET 10.0 SDK 以降が必要です。
    echo Microsoftの公式サイトからインストールしてください。
    echo.
    pause
    exit /b
)

rem 2. 初回ビルドの実行（復元も自動で行われます）
echo [1/2] 依存関係の復元およびアプリケーションをビルド中...
echo ---------------------------------------------------
dotnet build
echo ---------------------------------------------------

if %errorlevel% neq 0 (
    echo.
    echo 【エラー】ビルドに失敗しました。
    echo ソースコードにエラーがあるか、SDKのバージョンが異なる可能性があります。
    echo.
    pause
    exit /b
)

echo.
echo [2/2] セットアップが正常に完了しました！
echo.
echo 【実行ファイル生成先】
echo bin\Debug\net10.0-windows\SCtoolGui.exe
echo.
echo 普段の起動は上記のexeを直接叩くか、ショートカットを作成してご利用ください。
echo.
pause
exit