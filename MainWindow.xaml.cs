using System;
using System.Diagnostics;
using System.IO;
using System.Linq; 
using System.Windows;
using System.Windows.Threading;

namespace SCtoolGui
{
    public partial class MainWindow : Window
    {
        private SettingsManager _settingsManager = new SettingsManager();
        private DispatcherTimer _statusTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();
            _settingsManager.Load();
            
            if (_settingsManager.Current.WindowLeft.HasValue && _settingsManager.Current.WindowTop.HasValue)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _settingsManager.Current.WindowLeft.Value;
                this.Top = _settingsManager.Current.WindowTop.Value;
            }

            this.Topmost = _settingsManager.Current.AppTopmost;

            InitializeWindowList();
            InitializeCaptureAndHotKey();

            Log("アプリが起動しました。");
            CheckUpdates();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                _settingsManager.Current.WindowLeft = this.Left;
                _settingsManager.Current.WindowTop = this.Top;
            }
            
            try
            {
                // 終了時に、対象ウィンドウへ掛けた最前面固定を解除しておく
                var targetWindow = FindTargetWindow();
                if (targetWindow != null && targetWindow.Handle != IntPtr.Zero)
                {
                    WindowManager.SetAlwaysOnTop(targetWindow.Handle, false);
                }
            }
            catch { }
            
            _settingsManager.Save();
            _hotKeyManager?.Dispose();

            base.OnClosed(e);
        }

        private void Log(string msg) 
        { 
            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n"); 
            TxtLog.ScrollToEnd(); 
        }

        private void SaveAndLog(string? logMessage = null)
        {
            if (_settingsManager.Save()) {
                if (logMessage != null) Log(logMessage);
            } else {
                Log("【エラー】設定の保存に失敗しました。");
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            // ★引数に _settingsManager.Current.ShutterVolume を追加
            var settingsWin = new SettingsWindow(
                _settingsManager.Current.SaveDirectory, 
                _settingsManager.Current.HotkeyModifiers, 
                _settingsManager.Current.HotkeyKey,
                _settingsManager.Current.AppTopmost,
                _settingsManager.Current.SaveInWindowNameFolder,
                _settingsManager.Current.ResetSettingsOnWindowChange,
                _settingsManager.Current.AutoCopyClipboard,
                _settingsManager.Current.PlayShutterSound,
                _settingsManager.Current.ShutterVolume) { Owner = this }; 
            
            if (settingsWin.ShowDialog() == true) {
                _settingsManager.Current.SaveDirectory = settingsWin.ResultSaveDir;
                _settingsManager.Current.HotkeyModifiers = settingsWin.ResultModifiers;
                _settingsManager.Current.HotkeyKey = settingsWin.ResultKey;
                _settingsManager.Current.AppTopmost = settingsWin.ResultAppTopmost;
                _settingsManager.Current.SaveInWindowNameFolder = settingsWin.ResultSaveInWindowNameFolder;
                _settingsManager.Current.ResetSettingsOnWindowChange = settingsWin.ResultResetSettingsOnWindowChange;
                _settingsManager.Current.AutoCopyClipboard = settingsWin.ResultAutoCopyClipboard;
                _settingsManager.Current.PlayShutterSound = settingsWin.ResultPlayShutterSound;
                
                // ★結果を受け取る
                _settingsManager.Current.ShutterVolume = settingsWin.ResultShutterVolume;
                
                this.Topmost = _settingsManager.Current.AppTopmost;

                SaveAndLog("詳細設定を更新しました。");
                RegisterHotKey();
                UpdateButtonText();
                
                UpdateCurrentSavePathDisplay();
            }
            else {
                Log("詳細設定の変更をキャンセルしました。");
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e) 
        { 
            string targetDir = _settingsManager.Current.SaveDirectory;

            if (_settingsManager.Current.SaveInWindowNameFolder && CurrentTarget != null)
            {
                string specificDir = Path.Combine(targetDir, CurrentFolderName);

                if (Directory.Exists(specificDir))
                {
                    targetDir = specificDir;
                }
            }

            if (Directory.Exists(targetDir)) 
            {
                Process.Start("explorer.exe", targetDir);
            }
            else
            {
                Log("【警告】開く対象のフォルダが存在しません。");
            }
        }

        private async void CheckUpdates()
        {
            Log("アップデートを確認中...");
            var status = await UpdateManager.GetUpdateStatusAsync();

            if (status.HasUpdate) {
                UpdateBanner.Visibility = Visibility.Visible;
                Log($"【通知】{status.Reason} ボタンから更新できます。");
            } else {
                // 更新が無い場合も理由を出す（開発ブランチでスキップした場合など）
                Log(status.Reason);
            }
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            // 作業中の変更を巻き込まないよう、実行前に安全性を確認する
            string? blockedReason = UpdateManager.ValidateCanUpdate();
            if (blockedReason != null)
            {
                Log($"【中止】{blockedReason.Replace("\n", " ")}");
                MessageBox.Show(blockedReason, "更新を中止しました", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try { UpdateManager.ExecuteUpdateAndRestart(); }
            catch (Exception ex) { MessageBox.Show($"アップデート処理の開始に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }
}