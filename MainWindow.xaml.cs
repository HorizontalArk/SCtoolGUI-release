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

        /// <summary>Prompt を既に出した対象キー（対象ごと1回まで誘導するため）。</summary>
        private readonly System.Collections.Generic.HashSet<string> _autoSwitchPromptedTargets = new();

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

            // 起動時プレビュー撮影で対象ウィンドウを前面化した結果、SCtool が背面へ回ることがある。
            // コンストラクタ内ではまだ自ウィンドウのHWNDが確定しておらず前面復帰が効かないため、
            // 表示完了後に一度だけツールを前面へ戻す。
            Loaded += (s, e) =>
                Dispatcher.BeginInvoke(new Action(BringToolToForeground),
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        protected override void OnClosed(EventArgs e)
        {
            // WM_SETICON で送った独自 HICON を破棄（リーク防止）。
            if (_bigIcon != IntPtr.Zero) { DestroyIcon(_bigIcon); _bigIcon = IntPtr.Zero; }
            if (_smallIcon != IntPtr.Zero) { DestroyIcon(_smallIcon); _smallIcon = IntPtr.Zero; }

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

        /// <summary>
        /// 設定のアイコン画像パスをウィンドウへ反映する。空/不正なら埋め込み既定に戻す。
        ///
        /// WPF の <see cref="Window.Icon"/> だけを差し替えると、タイトルバーや Alt+Tab には効くが、
        /// タスクバーボタンのアイコン（大アイコン）が更新されないことがある。そこで WPF 側の設定に加え、
        /// Win32 の WM_SETICON で大(32px)・小(16px)アイコンを HWND へ明示送信し、タスクバーにも確実に反映する。
        ///
        /// HWND 未生成（初回コンストラクタ時点）では WM_SETICON を送れないため、その場合は
        /// <see cref="OnSourceInitialized"/> で再適用する（<see cref="_iconApplyPending"/> フラグで通知）。
        /// </summary>
        private void ApplyWindowIcon()
        {
            string path = _settingsManager.Current.IconPath;
            bool hasCustom = !string.IsNullOrEmpty(path) && File.Exists(path);

            // WPF 側（タイトルバー・Alt+Tab 用）。null なら埋め込み既定(app.ico)に戻る。
            try
            {
                this.Icon = hasCustom
                    ? new System.Windows.Media.Imaging.BitmapImage(new Uri(path))
                    : null;
            }
            catch { this.Icon = null; hasCustom = false; }

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                // HWND 未生成。SourceInitialized で再適用させる。
                _iconApplyPending = true;
                return;
            }

            ApplyTaskbarIcon(hwnd, hasCustom ? path : null);
        }

        /// <summary>ApplyWindowIcon が HWND 未生成時に呼ばれたら、SourceInitialized で再適用するためのフラグ。</summary>
        private bool _iconApplyPending;

        /// <summary>WM_SETICON で送った独自 HICON。差し替え・破棄のため保持する（リーク防止）。</summary>
        private IntPtr _bigIcon, _smallIcon;

        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>
        /// タスクバー用に大小 HICON を WM_SETICON で送る。path が null なら独自アイコンを外し、
        /// 埋め込み既定(app.ico)へ戻す（NULL を送ると OS がクラスアイコンにフォールバックする）。
        /// </summary>
        private void ApplyTaskbarIcon(IntPtr hwnd, string? path)
        {
            IntPtr newBig = IntPtr.Zero, newSmall = IntPtr.Zero;
            if (path != null)
            {
                try
                {
                    newBig = CreateHIcon(path, 32);
                    newSmall = CreateHIcon(path, 16);
                }
                catch { newBig = newSmall = IntPtr.Zero; }
            }

            SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, newBig);
            SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, newSmall);

            // 直前まで使っていた独自アイコンを破棄（リーク防止）。差し替え後に破棄する。
            if (_bigIcon != IntPtr.Zero) DestroyIcon(_bigIcon);
            if (_smallIcon != IntPtr.Zero) DestroyIcon(_smallIcon);
            _bigIcon = newBig;
            _smallIcon = newSmall;
        }

        /// <summary>
        /// 画像ファイルを指定サイズ(px)へデコードし、BGRA32 の DIB から HICON を生成する。
        /// System.Drawing に依存せず WPF + Win32(CreateIconIndirect) のみで作る。呼び出し側で DestroyIcon する。
        /// </summary>
        private static IntPtr CreateHIcon(string path, int size)
        {
            var decoded = new System.Windows.Media.Imaging.BitmapImage();
            decoded.BeginInit();
            decoded.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            decoded.UriSource = new Uri(path);
            decoded.EndInit();

            // 指定サイズの正方形へ、アスペクトを保って中央配置で描画する。
            var target = new System.Windows.Media.Imaging.RenderTargetBitmap(
                size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var visual = new System.Windows.Media.DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                double scale = Math.Min((double)size / decoded.PixelWidth, (double)size / decoded.PixelHeight);
                double w = decoded.PixelWidth * scale, h = decoded.PixelHeight * scale;
                double x = (size - w) / 2, y = (size - h) / 2;
                dc.DrawImage(decoded, new Rect(x, y, w, h));
            }
            target.Render(visual);

            // BGRA32 ピクセルを取り出す。
            var bgra = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                target, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int stride = size * 4;
            byte[] pixels = new byte[stride * size];
            bgra.CopyPixels(pixels, stride, 0);

            // カラービットマップ（AND マスクは 32bpp アルファがあるので全 0 でよい）から HICON を作る。
            IntPtr color = CreateBgraDib(size, size, pixels);
            IntPtr mask = CreateBitmap(size, size, 1, 1, IntPtr.Zero); // モノクロマスク（未使用領域は 0）
            try
            {
                var info = new ICONINFO { fIcon = true, hbmMask = mask, hbmColor = color };
                return CreateIconIndirect(ref info);
            }
            finally
            {
                if (color != IntPtr.Zero) DeleteObject(color);
                if (mask != IntPtr.Zero) DeleteObject(mask);
            }
        }

        /// <summary>BGRA32 のトップダウン DIB セクションを作り、ピクセルを書き込んで返す。</summary>
        private static IntPtr CreateBgraDib(int width, int height, byte[] bgra)
        {
            var bmi = new BITMAPINFO
            {
                biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // 負でトップダウン
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB
            };
            IntPtr bits;
            IntPtr hbm = CreateDIBSection(IntPtr.Zero, ref bmi, 0 /*DIB_RGB_COLORS*/, out bits, IntPtr.Zero, 0);
            if (hbm != IntPtr.Zero && bits != IntPtr.Zero)
                System.Runtime.InteropServices.Marshal.Copy(bgra, 0, bits, bgra.Length);
            return hbm;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct ICONINFO { public bool fIcon; public int xHotspot; public int yHotspot; public IntPtr hbmMask; public IntPtr hbmColor; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth; public int biHeight; public ushort biPlanes;
            public ushort biBitCount; public uint biCompression; public uint biSizeImage;
            public int biXPelsPerMeter; public int biYPelsPerMeter; public uint biClrUsed; public uint biClrImportant;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public uint biSize; public int biWidth; public int biHeight; public ushort biPlanes;
            public ushort biBitCount; public uint biCompression; public uint biSizeImage;
            public int biXPelsPerMeter; public int biYPelsPerMeter; public uint biClrUsed; public uint biClrImportant;
            // カラーテーブルは 32bpp では不要
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr CreateIconIndirect(ref ICONINFO icon);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr CreateBitmap(int w, int h, uint planes, uint bpp, IntPtr bits);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private PreviewMode CurrentPreviewMode =>
            _settingsManager.Current.PreviewOrientation == "Vertical"
                ? PreviewMode.Vertical : PreviewMode.Horizontal;

        /// <summary>ルート Grid の左右マージン合計（Margin="16" × 2）。</summary>
        private const double RootMargin = 32;
        /// <summary>縦モード初期表示時に追加するプレビュー列の幅。</summary>
        private const double DefaultVerticalPreviewWidth = 380;

        /// <summary>縦モードの操作群カラムの最小幅（キャプチャボタン行が見切れない下限）。</summary>
        private const double OperationsColumnMinWidth = 500;
        /// <summary>縦モード初期表示時の操作群カラム幅（下限より少し広め。ここから可変）。</summary>
        private const double DefaultOperationsColumnWidth = 540;

        /// <summary>縦モードで操作群とプレビューの間に置く境界ドラッグ用スプリッター。
        /// 横モードでは不要なので都度生成・除去する。</summary>
        private GridSplitter? _verticalSplitter;

        /// <summary>LogCard を現在の親から外す（付け替えの前処理）。</summary>
        private void DetachLogCard()
        {
            if (LogCard.Parent is Panel p) p.Children.Remove(LogCard);
            else if (LogCard.Parent is Grid g) g.Children.Remove(LogCard);
        }

        /// <summary>スプリッターがあればルートから外す。</summary>
        private void RemoveVerticalSplitter()
        {
            if (_verticalSplitter != null)
            {
                RootLayoutGrid.Children.Remove(_verticalSplitter);
                _verticalSplitter = null;
            }
        }

        /// <summary>プレビューの向きに応じてルートレイアウトを組み替え、窓サイズを合わせる。</summary>
        private void ApplyPreviewOrientation(PreviewMode mode)
        {
            RootLayoutGrid.RowDefinitions.Clear();
            RootLayoutGrid.ColumnDefinitions.Clear();
            DetachLogCard();
            RemoveVerticalSplitter();

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
                // 縦モードは3列: 操作群 / スプリッター(境界ドラッグ) / プレビュー。
                // 操作群列はピクセル幅（保存値または既定）で、スプリッターで自由に変えられる。
                RootLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                bool previewRight = _settingsManager.Current.VerticalPreviewSide != "Left";

                double opWidth = _settingsManager.Current.VerticalOperationsWidth ?? DefaultOperationsColumnWidth;
                var opCol = new ColumnDefinition { Width = new GridLength(opWidth), MinWidth = OperationsColumnMinWidth };
                var splitterCol = new ColumnDefinition { Width = GridLength.Auto };
                var prevCol = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 };

                _verticalSplitter = new GridSplitter
                {
                    Width = 6,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = System.Windows.Media.Brushes.Transparent,
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                    ShowsPreview = true,
                    Cursor = System.Windows.Input.Cursors.SizeWE,
                    ToolTip = "ドラッグで操作群とプレビューの幅を調整"
                };
                _verticalSplitter.DragCompleted += VerticalSplitter_DragCompleted;

                if (previewRight)
                {
                    // Col0=操作 / Col1=スプリッター / Col2=プレビュー
                    RootLayoutGrid.ColumnDefinitions.Add(opCol);
                    RootLayoutGrid.ColumnDefinitions.Add(splitterCol);
                    RootLayoutGrid.ColumnDefinitions.Add(prevCol);
                    Grid.SetColumn(OperationsPanel, 0);
                    Grid.SetColumn(_verticalSplitter, 1);
                    Grid.SetColumn(PreviewCard, 2);
                    PreviewCard.Margin = new Thickness(6, 0, 0, 0);
                }
                else
                {
                    // Col0=プレビュー / Col1=スプリッター / Col2=操作
                    RootLayoutGrid.ColumnDefinitions.Add(prevCol);
                    RootLayoutGrid.ColumnDefinitions.Add(splitterCol);
                    RootLayoutGrid.ColumnDefinitions.Add(opCol);
                    Grid.SetColumn(PreviewCard, 0);
                    Grid.SetColumn(_verticalSplitter, 1);
                    Grid.SetColumn(OperationsPanel, 2);
                    PreviewCard.Margin = new Thickness(0, 0, 6, 0);
                }
                Grid.SetRow(OperationsPanel, 0);
                Grid.SetRow(_verticalSplitter, 0);
                Grid.SetRow(PreviewCard, 0);
                RootLayoutGrid.Children.Add(_verticalSplitter);

                // 縦モードではログを操作群（縦積み）の末尾に置く
                OperationsPanel.Children.Add(LogCard);
                LogCard.Margin = new Thickness(0, 0, 0, 0);
            }

            ApplyWindowSizeForMode(mode);
        }

        /// <summary>スプリッターのドラッグ完了時、操作群カラムの実幅を保存する。</summary>
        private void VerticalSplitter_DragCompleted(object sender,
            System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // ドラッグ直後は列の ActualWidth がまだ更新前のことがあるため、
            // レイアウト確定後（Loaded 相当の低優先度）に読み取って保存する。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                int opColIndex = Grid.GetColumn(OperationsPanel);
                if (opColIndex >= 0 && opColIndex < RootLayoutGrid.ColumnDefinitions.Count)
                {
                    double w = RootLayoutGrid.ColumnDefinitions[opColIndex].ActualWidth;
                    if (w > 0)
                    {
                        _settingsManager.Current.VerticalOperationsWidth = w;
                        _settingsManager.Save();
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
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
                // 縦モード既定: 操作群（控えめ幅）＋スプリッター＋プレビュー列を「横に追加」した幅。
                // 高さは縦長スクショが大きく見えるよう縦長めに。
                double opWidth = s.VerticalOperationsWidth ?? DefaultOperationsColumnWidth;
                double defaultWidth = opWidth + 6 + DefaultVerticalPreviewWidth + RootMargin;
                this.Width = s.VerticalWindowWidth ?? defaultWidth;
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
                _settingsManager.Current.IconPath,
                _settingsManager.Current.VerticalPreviewSide,
                _settingsManager.Current.PreviewAutoSwitch) { Owner = this };
            
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

                _settingsManager.Current.VerticalPreviewSide = settingsWin.ResultVerticalPreviewSide;
                _settingsManager.Current.PreviewAutoSwitch = settingsWin.ResultPreviewAutoSwitch;
                // 縦時の左右が変わった場合、縦モードなら再適用して反映する
                if (CurrentPreviewMode == PreviewMode.Vertical) ApplyPreviewOrientation(PreviewMode.Vertical);

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