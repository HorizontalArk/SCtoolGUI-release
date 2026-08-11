using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Velopack;

namespace SCtoolGui
{
    public partial class MainWindow : Window
    {
        private SettingsManager _settingsManager = new SettingsManager();
        private DispatcherTimer _statusTimer = new DispatcherTimer();
        private readonly AppUpdateService _updateService = new AppUpdateService();
        private UpdateInfo? _pendingUpdate;

        public MainWindow()
        {
            InitializeComponent();
            _settingsManager.Load();
            ThemeManager.Apply(_settingsManager.Current.Theme);
            ApplyWindowIcon();

            if (_settingsManager.Current.WindowLeft.HasValue && _settingsManager.Current.WindowTop.HasValue)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _settingsManager.Current.WindowLeft.Value;
                this.Top = _settingsManager.Current.WindowTop.Value;
            }

            this.Topmost = _settingsManager.Current.AppTopmost;

            ApplyPreviewOrientation(CurrentPreviewMode);

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

            SaveWindowSizeForCurrentMode();

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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        /// <summary>
        /// 撮影のため対象を前面化した後、ツール自身を前面へ戻す。
        ///
        /// SetForegroundWindow は、直前に別アプリがフォアグラウンドを取った状態では
        /// OS のフォアグラウンドロックにより無視される。そこで前面スレッドへ一時的に
        /// AttachThreadInput してから前面化することで、確実にツールを前面へ戻す。
        ///
        /// なお対象が管理者権限ウィンドウで SCtool が非管理者の場合、UIPI により
        /// AttachThreadInput/SetForegroundWindow はブロックされる。ただし管理者対象の選択時は
        /// CmbWindows_SelectionChanged で昇格を促しており（承諾すれば両者管理者になり成立）、
        /// 昇格を断った場合は catch で握りつぶし、前面復帰しないだけに留める（クラッシュしない）。
        /// </summary>
        private void BringToolToForeground()
        {
            try
            {
                var self = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (self == IntPtr.Zero) { Activate(); return; }

                IntPtr fg = GetForegroundWindow();
                uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
                uint thisThread = GetCurrentThreadId();

                if (fgThread != thisThread && fgThread != 0)
                {
                    AttachThreadInput(thisThread, fgThread, true);
                    BringWindowToTop(self);
                    SetForegroundWindow(self);
                    AttachThreadInput(thisThread, fgThread, false);
                }
                else
                {
                    BringWindowToTop(self);
                    SetForegroundWindow(self);
                }
                Activate();
            }
            catch { }
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

        /// <summary>設定のアイコン画像パスを Window.Icon に反映する。空/不正なら埋め込み既定に戻す。</summary>
        private void ApplyWindowIcon()
        {
            string path = _settingsManager.Current.IconPath;
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(path));
                else
                    this.Icon = null; // 埋め込み既定に戻す
            }
            catch { this.Icon = null; }
        }

        private PreviewMode CurrentPreviewMode =>
            _settingsManager.Current.PreviewOrientation == "Vertical"
                ? PreviewMode.Vertical : PreviewMode.Horizontal;

        /// <summary>LogCard を現在の親から外す（付け替えの前処理）。</summary>
        private void DetachLogCard()
        {
            if (LogCard.Parent is Panel p) p.Children.Remove(LogCard);
            else if (LogCard.Parent is Grid g) g.Children.Remove(LogCard);
        }

        /// <summary>プレビューの向きに応じてルートレイアウトを組み替え、窓サイズを合わせる。</summary>
        private void ApplyPreviewOrientation(PreviewMode mode)
        {
            RootLayoutGrid.RowDefinitions.Clear();
            RootLayoutGrid.ColumnDefinitions.Clear();
            DetachLogCard();

            if (mode == PreviewMode.Horizontal)
            {
                // 1列3行: 操作群(上) → プレビュー(中・可変) → ログ(下)。従来の並びを維持。
                RootLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RootLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                RootLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RootLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(OperationsPanel, 0); Grid.SetColumn(OperationsPanel, 0);
                Grid.SetRow(PreviewCard, 1); Grid.SetColumn(PreviewCard, 0);
                PreviewCard.Margin = new Thickness(0);

                // ログはプレビューの下（ウィンドウ最下部）へ
                RootLayoutGrid.Children.Add(LogCard);
                Grid.SetRow(LogCard, 2); Grid.SetColumn(LogCard, 0);
                LogCard.Margin = new Thickness(0, 12, 0, 0);
            }
            else
            {
                RootLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                bool previewRight = _settingsManager.Current.VerticalPreviewSide != "Left";
                // 操作群は幅を抑え、プレビューを広めに
                var opCol = new ColumnDefinition { Width = new GridLength(360) };
                var prevCol = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };

                if (previewRight)
                {
                    RootLayoutGrid.ColumnDefinitions.Add(opCol);   // Col0=操作
                    RootLayoutGrid.ColumnDefinitions.Add(prevCol); // Col1=プレビュー
                    Grid.SetColumn(OperationsPanel, 0); Grid.SetColumn(PreviewCard, 1);
                    PreviewCard.Margin = new Thickness(12, 0, 0, 0);
                }
                else
                {
                    RootLayoutGrid.ColumnDefinitions.Add(prevCol); // Col0=プレビュー
                    RootLayoutGrid.ColumnDefinitions.Add(opCol);   // Col1=操作
                    Grid.SetColumn(PreviewCard, 0); Grid.SetColumn(OperationsPanel, 1);
                    PreviewCard.Margin = new Thickness(0, 0, 12, 0);
                }
                Grid.SetRow(OperationsPanel, 0); Grid.SetRow(PreviewCard, 0);

                // 縦モードではログを操作群（縦積み）の末尾に置く
                OperationsPanel.Children.Add(LogCard);
                LogCard.Margin = new Thickness(0, 0, 0, 0);
            }

            ApplyWindowSizeForMode(mode);
        }

        /// <summary>モードに対応する保存済み窓サイズを適用する（無ければ既定サイズ）。</summary>
        private void ApplyWindowSizeForMode(PreviewMode mode)
        {
            var s = _settingsManager.Current;
            if (mode == PreviewMode.Horizontal)
            {
                if (s.HorizontalWindowWidth.HasValue) this.Width = s.HorizontalWindowWidth.Value;
                if (s.HorizontalWindowHeight.HasValue) this.Height = s.HorizontalWindowHeight.Value;
            }
            else
            {
                // 縦モード既定は縦長め（保存が無ければ 560x900 を初期提示）
                this.Width = s.VerticalWindowWidth ?? 560;
                this.Height = s.VerticalWindowHeight ?? 900;
            }
        }

        /// <summary>現在の窓サイズを現在モードのサイズとして記憶する。</summary>
        private void SaveWindowSizeForCurrentMode()
        {
            if (this.WindowState != WindowState.Normal) return;
            var s = _settingsManager.Current;
            if (CurrentPreviewMode == PreviewMode.Horizontal)
            {
                s.HorizontalWindowWidth = this.Width; s.HorizontalWindowHeight = this.Height;
            }
            else
            {
                s.VerticalWindowWidth = this.Width; s.VerticalWindowHeight = this.Height;
            }
        }

        private void BtnTogglePreviewOrientation_Click(object sender, RoutedEventArgs e)
        {
            // 切替前に現在モードのサイズを保存
            SaveWindowSizeForCurrentMode();

            var next = CurrentPreviewMode == PreviewMode.Horizontal
                ? PreviewMode.Vertical : PreviewMode.Horizontal;
            _settingsManager.Current.PreviewOrientation =
                next == PreviewMode.Vertical ? "Vertical" : "Horizontal";
            ApplyPreviewOrientation(next);
            _settingsManager.Save();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            bool wasAlwaysAdmin = _settingsManager.Current.AlwaysRunAsAdmin;

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
                _settingsManager.Current.ShutterVolume,
                _settingsManager.Current.AlwaysRunAsAdmin,
                _settingsManager.Current.Theme,
                _settingsManager.Current.IconPath) { Owner = this };
            
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

                _settingsManager.Current.AlwaysRunAsAdmin = settingsWin.ResultAlwaysRunAsAdmin;

                _settingsManager.Current.Theme = settingsWin.ResultTheme;
                ThemeManager.Apply(_settingsManager.Current.Theme);

                _settingsManager.Current.IconPath = settingsWin.ResultIconPath;
                ApplyWindowIcon();

                this.Topmost = _settingsManager.Current.AppTopmost;

                SaveAndLog("詳細設定を更新しました。");
                RegisterHotKey();
                UpdateButtonText();

                UpdateCurrentSavePathDisplay();

                // 「常に管理者として起動」を新たにONにした場合は、今すぐ再起動するか確認する。
                if (_settingsManager.Current.AlwaysRunAsAdmin && !wasAlwaysAdmin
                    && !ProcessElevation.IsCurrentProcessElevated())
                {
                    var answer = MessageBox.Show(
                        "設定を反映するには管理者として再起動する必要があります。\n今すぐ再起動しますか？",
                        "管理者として再起動",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (answer == MessageBoxResult.Yes)
                    {
                        _settingsManager.Save();
                        SingleInstance.Release();
                        if (ProcessElevation.RelaunchAsAdmin())
                        {
                            Application.Current.Shutdown();
                        }
                        else
                        {
                            SingleInstance.TryAcquire();
                        }
                    }
                }
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
            // Velopack でインストールされていない（dev実行など）場合は更新確認しない。
            if (!_updateService.IsInstalled)
            {
                Log("更新確認: インストール版ではないためスキップしました。");
                return;
            }

            Log("アップデートを確認中...");
            try
            {
                _pendingUpdate = await _updateService.CheckAsync();
                if (_pendingUpdate != null)
                {
                    UpdateBanner.Visibility = Visibility.Visible;
                    Log("【通知】新しいアップデートがあります。ボタンから更新できます。");
                }
                else
                {
                    Log("アプリケーションは最新です。");
                }
            }
            catch
            {
                // ネットワーク不通などは黙って諦める（起動を妨げない）。
                Log("更新の確認に失敗しました（ネットワーク等）。");
            }
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate == null) return;

            try
            {
                Log("アップデートをダウンロードして適用します...");
                BtnUpdate.IsEnabled = false;
                await _updateService.DownloadAndApplyAsync(_pendingUpdate);
                // 成功時はここに戻らず再起動する。
            }
            catch (Exception ex)
            {
                BtnUpdate.IsEnabled = true;
                MessageBox.Show($"アップデートに失敗しました:\n{ex.Message}", "エラー",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}