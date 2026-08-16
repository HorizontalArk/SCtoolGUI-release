using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SCtoolGui
{
    public partial class MainWindow
    {
        private HotKeyManager? _hotKeyManager;
        private string _lastCapturedPath = "";
        private MediaPlayer _shutterPlayer = new MediaPlayer();

        /// <summary>撮影時に ScreenCapture がカット前の全体像を書き出す一時ファイル。</summary>
        private static readonly string CapturePreviewPath = Path.Combine(Path.GetTempPath(), "SCtool_preview.jpg");

        private void InitializeCaptureAndHotKey()
        {
            UpdateButtonText();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            RegisterHotKey();
        }

        private void RegisterHotKey()
        {
            if (_hotKeyManager != null) { _hotKeyManager.OnHotKeyPressed -= ExecuteCapture; _hotKeyManager.Dispose(); }
            try {
                _hotKeyManager = new HotKeyManager(this, _settingsManager.Current.HotkeyModifiers, _settingsManager.Current.HotkeyKey);
                _hotKeyManager.OnHotKeyPressed += ExecuteCapture;

                if (!_hotKeyManager.IsRegistered)
                {
                    Log("【警告】ホットキーを登録できませんでした。他のアプリに使われている可能性があります。詳細設定で別のキーに変更してください。");
                }
            }
            catch { Log("【警告】ホットキーの登録に失敗しました。"); }
        }

        private void UpdateButtonText()
        {
            List<string> keys = new List<string>();
            if ((_settingsManager.Current.HotkeyModifiers & 0x0002) != 0) keys.Add("Ctrl");
            if ((_settingsManager.Current.HotkeyModifiers & 0x0004) != 0) keys.Add("Shift");
            if ((_settingsManager.Current.HotkeyModifiers & 0x0001) != 0) keys.Add("Alt");
            
            string keyStr = (_settingsManager.Current.HotkeyKey >= 0x70 && _settingsManager.Current.HotkeyKey <= 0x7B) 
                ? $"F{_settingsManager.Current.HotkeyKey - 0x70 + 1}" 
                : ((char)_settingsManager.Current.HotkeyKey).ToString();
            
            keys.Add(keyStr);
            // ボタンの静的テキストとアイコンは XAML 側。ここではホットキー表示(キーキャップ)だけ更新する。
            TxtCaptureHotkey.Text = string.Join(" + ", keys);
        }

        private void BtnCapture_Click(object sender, RoutedEventArgs e) => ExecuteCapture();

        /// <summary>表示中の ToolTip を閉じ、対象ウィンドウへの写り込みを防ぐ。</summary>
        private void DismissOpenToolTip()
        {
            var btn = BtnCapture;
            if (btn == null) return;

            // ToolTip がオブジェクトなら直接閉じる
            if (btn.ToolTip is System.Windows.Controls.ToolTip tip)
            {
                tip.IsOpen = false;
            }
            // 文字列 ToolTip も含め、一時無効化→再有効化で確実に閉じる
            System.Windows.Controls.ToolTipService.SetIsEnabled(btn, false);
            System.Windows.Controls.ToolTipService.SetIsEnabled(btn, true);

            // 閉じた状態を描画へ反映させる（レンダリング優先度で1サイクル回す）
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private void ExecuteCapture()
        {
            if (CmbWindows.SelectedItem is WindowItem selected) {
                // プルダウン項目の Title/Handle はプルダウンを開いた時点で固定され、その後アプリ側で
                // タイトルが変わっても追随しない。撮影の直前に実ウィンドウを取り直し、最新の
                // タイトル・ハンドルへ更新してからファイル名を決める（古いタイトルが残る不具合の対策）。
                if (FindTargetWindow() is WindowItem latest)
                {
                    selected.Title = latest.Title;
                    selected.Handle = latest.Handle;
                }

                if (selected.Handle == IntPtr.Zero) {
                    Log($"【警告】{selected.Title} は起動していないためキャプチャできません。");
                    return;
                }

                if (WindowManager.IsWindowMinimized(selected.Handle)) {
                    Log($"【警告】{selected.Title} は最小化されているためキャプチャできません。");
                    return;
                }

                try {
                    // ボタン上のマウスで開いた ToolTip が対象ウィンドウに重なると写り込むため、先に閉じる
                    DismissOpenToolTip();

                    string baseDir = _settingsManager.Current.SaveDirectory;
                    string targetDir = baseDir;

                    // フォルダ名は常に登録名（表示名）を使う。フォルダ振り分けはタイトル変化で乱れさせない。
                    string folderName = CurrentFolderName;

                    if (_settingsManager.Current.SaveInWindowNameFolder)
                    {
                        targetDir = Path.Combine(baseDir, folderName);
                    }

                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    // ファイル名ベースは設定に応じて登録名か実ウィンドウタイトルを使う。
                    string fileBase = FileBaseNameResolver.Resolve(
                        _settingsManager.Current.UseWindowTitleForFileName,
                        selected.Title,
                        folderName);

                    string fullPath = Path.Combine(targetDir, $"{fileBase}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                    int topCut = CurrentCutValue;

                    ScreenCapture.SaveWindowCaptureWithExif(selected.Handle, fullPath, CapturePreviewPath, topCut);
                    _lastCapturedPath = fullPath;

                    if (CurrentTarget is TargetInfo target) target.TopCut = topCut;
                    SaveAndLog($"【成功】 {fileBase} -> {fullPath}");

                    if (_settingsManager.Current.PlayShutterSound)
                    {
                        PlayAppShutterSound();
                    }

                    // previewPath ではなく fullPath(本番画像) を表示し、isFinalOutputフラグを付ける
                    ShowPreview(fullPath, isTempPreview: false, isFinalOutput: true);

                    if (_settingsManager.Current.AutoCopyClipboard)
                    {
                        // 撮影直後は保存した本番画像が正しいので、設定に依らず LastSaved を対象にする。
                        CopyToClipboard(CopyTarget.LastSaved, isAuto: true);
                    }

                } catch (Exception ex) { Log($"【エラー】 {ex.Message}"); }
                finally
                {
                    // 撮影のため対象を前面化した可能性があるので、成否に関わらずツールを前面へ戻す
                    BringToolToForeground();
                }
            }
        }

        private void PlayAppShutterSound()
        {
            try
            {
                string? targetSound = ResolveShutterSoundPath();

                if (targetSound == null)
                {
                    System.Media.SystemSounds.Asterisk.Play();
                    return;
                }

                _shutterPlayer.Volume = _settingsManager.Current.ShutterVolume;
                _shutterPlayer.Open(new Uri(targetSound));
                _shutterPlayer.Position = TimeSpan.Zero;
                _shutterPlayer.Play();
            }
            catch
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
        }

        /// <summary>アプリ同梱音 → Windows標準音 の順に、最初に見つかった音源のパスを返す。</summary>
        private static string? ResolveShutterSoundPath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string mediaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");

            string[] candidates =
            {
                Path.Combine(appDir, "shutter.wav"),
                Path.Combine(appDir, "shutter.mp3"),
                Path.Combine(mediaDir, "Windows Navigation Start.wav"),
                Path.Combine(mediaDir, "Windows Default.wav"),
            };

            return candidates.FirstOrDefault(File.Exists);
        }
    }
}