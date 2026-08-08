@echo off
echo リポジトリを更新中...
cd /d "%~dp0"
git pull origin main
echo.
echo 更新が完了しました。
pause