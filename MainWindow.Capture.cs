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
            BtnCapture.Content = $"📷 スクリーンショットを撮る ({string.Join("+", keys)})";
        }

        private void BtnCapture_Click(object sender, RoutedEventArgs e) => ExecuteCapture();

        private void ExecuteCapture()
        {
            if (CmbWindows.SelectedItem is WindowItem selected) {
                if (selected.Handle == IntPtr.Zero) {
                    Log($"【警告】{selected.Title} は起動していないためキャプチャできません。");
                    return;
                }

                if (WindowManager.IsWindowMinimized(selected.Handle)) {
                    Log($"【警告】{selected.Title} は最小化されているためキャプチャできません。");
                    return;
                }

                try {
                    string baseDir = _settingsManager.Current.SaveDirectory;
                    string targetDir = baseDir;

                    // ファイル名・フォルダ名は、変化しうるタイトルではなく表示名を使う
                    string safeName = CurrentFolderName;

                    if (_settingsManager.Current.SaveInWindowNameFolder)
                    {
                        targetDir = Path.Combine(baseDir, safeName);
                    }

                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    string fullPath = Path.Combine(targetDir, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                    int topCut = CurrentCutValue;

                    ScreenCapture.SaveWindowCaptureWithExif(selected.Handle, fullPath, CapturePreviewPath, topCut);
                    _lastCapturedPath = fullPath;

                    if (CurrentTarget is TargetInfo target) target.TopCut = topCut;
                    SaveAndLog($"【成功】 {safeName} -> {fullPath}");

                    if (_settingsManager.Current.PlayShutterSound)
                    {
                        PlayAppShutterSound();
                    }

                    // previewPath ではなく fullPath(本番画像) を表示し、isFinalOutputフラグを付ける
                    ShowPreview(fullPath, isTempPreview: false, isFinalOutput: true);

                    if (_settingsManager.Current.AutoCopyClipboard)
                    {
                        CopyPreviewToClipboard(isAuto: true);
                    }

                } catch (Exception ex) { Log($"【エラー】 {ex.Message}"); }
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