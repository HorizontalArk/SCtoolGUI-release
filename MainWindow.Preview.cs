using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SCtoolGui
{
    public partial class MainWindow
    {
        private bool _isTempPreviewMode = false;
        private bool _isFinalOutputMode = false;
        private bool _isDraggingCutLine = false;

        /// <summary>最後に ShowPreview した画像の向き（自動切替の判定材料）。</summary>
        private PreviewMode? _lastShownImageOrientation;

        private static readonly string TempPreviewPath = Path.Combine(Path.GetTempPath(), "SCtool_temp_preview.jpg");

        /// <summary>最後に保存した画像が実際にディスク上に存在するか。</summary>
        private bool HasLastCapture => !string.IsNullOrEmpty(_lastCapturedPath) && File.Exists(_lastCapturedPath);

        /// <summary>キャプチャ後に自アプリを前面へ戻すべきか（設定に基づく実効値）。</summary>
        private bool ShouldRestoreFocusAfterCapture =>
            FocusRestoreLogic.AfterCapture(_settingsManager.Current.RestoreFocusToToolOnCapture);

        /// <summary>プレビュー取得後に自アプリを前面へ戻すべきか（設定に基づく実効値）。</summary>
        private bool ShouldRestoreFocusAfterPreview =>
            FocusRestoreLogic.AfterPreview(
                _settingsManager.Current.UnifyCaptureAndPreviewFocus,
                _settingsManager.Current.RestoreFocusToToolOnCapture,
                _settingsManager.Current.RestoreFocusToToolOnPreview);

        /// <summary>現在のカット量。カットOFF時や数値が不正な場合は 0。</summary>
        private int CurrentCutValue =>
            (ChkCutTab?.IsChecked == true && int.TryParse(TxtTopCut?.Text, out int val)) ? Math.Max(0, val) : 0;

        /// <summary>選択中のウィンドウを一時プレビューとして撮影し、成功したら表示する。</summary>
        /// <param name="verbose">true の場合、成功／失敗をログに出力する。</param>
        private bool CaptureTempPreview(bool verbose)
        {
            if (CmbWindows.SelectedItem is not WindowItem selected) return false;

            if (selected.Handle == IntPtr.Zero)
            {
                if (verbose) Log(LogLevel.Warning, LogMessages.PreviewNoWindow);
                return false;
            }
            if (WindowManager.IsWindowMinimized(selected.Handle))
            {
                if (verbose) Log(LogLevel.Warning, LogMessages.PreviewMinimized);
                return false;
            }

            try
            {
                if (!ScreenCapture.SavePreviewOnly(selected.Handle, TempPreviewPath))
                {
                    // 対象を前面にできなかった場合もここに来る（別画面の写り込みを防ぐため中止している）
                    if (verbose) Log(LogLevel.Error, LogMessages.PreviewCaptureFailed);
                    return false;
                }

                ShowPreview(TempPreviewPath, isTempPreview: true);
                if (verbose) Log(LogMessages.PreviewUpdated);
                return true;
            }
            catch (Exception ex)
            {
                if (verbose) Log(LogLevel.Error, LogMessages.PreviewUpdateError(ex.Message));
                return false;
            }
            finally
            {
                // プレビュー取得のため対象を前面化した可能性がある。設定で有効なときだけツールを前面へ戻す。
                if (ShouldRestoreFocusAfterPreview) BringToolToForeground();
            }
        }

        private void UpdateTempPreview() => CaptureTempPreview(verbose: true);

        /// <summary>カット境界線とオーバーレイの表示を現在の状態に合わせて更新する。</summary>
        private void UpdateCutOverlay(bool forceHide = false)
        {
            if (CutOverlayRect == null || CutBoundaryLine == null) return;

            bool show = !forceHide && ChkCutTab?.IsChecked == true && !_isFinalOutputMode;

            if (show)
            {
                int h = CurrentCutValue;
                CutOverlayRect.Height = h;
                CutOverlayRect.Visibility = Visibility.Visible;
                CutBoundaryLine.Y1 = h;
                CutBoundaryLine.Y2 = h;
                CutBoundaryLine.Visibility = Visibility.Visible;
            }
            else
            {
                CutOverlayRect.Visibility = Visibility.Collapsed;
                CutBoundaryLine.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 保存画像に対する操作ボタン（開く／コピー／▽／削除）の有効/無効を、実際の可否に合わせて更新する。
        ///
        /// 従来は見た目（Opacity/Cursor）だけを変え IsEnabled は true のままで、
        /// 「禁止表示なのに押せてアプリが判定を返す」という表記と動作の食い違いがあった。
        /// ここでは IsEnabled で本当に無効化し、対象ごとに判定を分ける：
        ///   ・開く／削除     … 保存画像が無ければ無効（保存画像に依存するため）。
        ///   ・コピー本体     … 既定対象が LastSaved かつ保存画像が無いときだけ無効
        ///                      （TempPreview/FreshPreview 既定なら保存前でも使えるので有効）。
        ///   ・▽ボタン       … 常に有効（メニューを開くため）。
        ///   ・▽の各項目      … 「最後に保存した画像」だけ保存画像に連動、他は常に有効。
        /// </summary>
        private void UpdateActionButtonsState()
        {
            bool hasLastCapture = HasLastCapture;

            // 開く・削除は保存画像に連動。無効時は対応マスクを出して禁止カーソルを見せる。
            SetButtonEnabled(BtnOpenFile, hasLastCapture, BtnOpenFileDisableMask);
            SetButtonEnabled(BtnDeleteFile, hasLastCapture, BtnDeleteFileDisableMask);

            // コピー本体は既定対象と保存画像の有無で判定。
            var defaultTarget = CopyTargetResolver.Parse(_settingsManager.Current.CopySource);
            bool copyEnabled = CopyButtonState.IsMainCopyEnabled(defaultTarget, hasLastCapture);
            // マスクの幅・高さは XAML で BtnCopyClipboard へバインド済みなので、可視状態だけ切り替える。
            SetButtonEnabled(BtnCopyClipboard, copyEnabled, BtnCopyClipboardDisableMask);

            // ▽ボタン自体は常に有効。メニュー項目のうち「最後に保存した画像」だけ保存画像に連動。
            SetButtonEnabled(BtnCopyDropdown, true);
            if (MenuCopyLastSaved != null) MenuCopyLastSaved.IsEnabled = hasLastCapture;

            // プレビュー画像はダブルクリックで開けるため、保存画像があるときだけ手のひらカーソルにする。
            PreviewContentGrid.Cursor = hasLastCapture ? Cursors.Hand : Cursors.Arrow;
        }

        /// <summary>
        /// ボタンの IsEnabled と見た目（薄さ）を設定する。
        /// 無効時の「禁止」カーソルは、WPF が IsEnabled=false のコントロールで Cursor を
        /// 無視するため、ボタンに重ねた透明オーバーレイ(disableMask)側で出す。
        /// disableMask は無効時のみ表示し、禁止カーソル表示とクリック吸収を担う。
        /// </summary>
        private static void SetButtonEnabled(System.Windows.Controls.Control? btn, bool enabled,
            System.Windows.UIElement? disableMask = null)
        {
            if (btn == null) return;
            btn.IsEnabled = enabled;
            btn.Opacity = enabled ? 1.0 : 0.4;
            if (disableMask != null)
                disableMask.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateToolTips()
        {
            if (ImgPreview == null) return;

            const string NoImageMessage = "まだ画像が保存されていません";
            bool hasLastCapture = HasLastCapture;

            // 一時プレビューかどうかに関わらず、保存された画像があるかどうかで案内を変える
            string imgTooltip = hasLastCapture
                ? "ダブルクリックで最後に保存した画像を開きます"
                : NoImageMessage;

            if (ChkCutTab.IsChecked == true && !_isFinalOutputMode)
            {
                imgTooltip += "\n\n【カット位置の調整】\n・ホイール回転で上下\n・境界線をドラッグ＆ドロップ\n・Shift＋クリックで指定位置にワープ";
            }

            ImgPreview.ToolTip = imgTooltip;

            BtnOpenFile.ToolTip = hasLastCapture ? "最後に保存した画像を既定のアプリで開きます" : NoImageMessage;
            BtnDeleteFile.ToolTip = hasLastCapture ? "最後に保存した画像をPCから完全に削除します" : NoImageMessage;

            // コピー本体は既定対象(CopySource)に従うため、ToolTip も既定対象に合わせる。
            // 有効/無効判定(CopyButtonState)と文言を一致させ、「無効なのに別案内」を防ぐ。
            var defaultTarget = CopyTargetResolver.Parse(_settingsManager.Current.CopySource);
            BtnCopyClipboard.ToolTip = defaultTarget switch
            {
                CopyTarget.TempPreview => "一時プレビューをクリップボードにコピーします",
                CopyTarget.FreshPreview => "最新のプレビューを取得してクリップボードにコピーします",
                // LastSaved: 保存画像が無いと無効になるため、その場合は理由を示す。
                _ => hasLastCapture ? "最後に保存した画像をクリップボードにコピーします" : NoImageMessage,
            };
        }

        /// <summary>プレビュー画像を差し替え、カット表示とボタン状態を現在の状態に合わせる。</summary>
        private void ShowPreview(string filePath, bool isTempPreview = false, bool isFinalOutput = false)
        {
            TxtDeleteMessage.Visibility = Visibility.Collapsed;

            ImgPreview.Source = LoadBitmap(filePath);

            if (ImgPreview.Source is BitmapSource bmp)
            {
                _lastShownImageOrientation =
                    PreviewOrientationLogic.DetectImageOrientation(bmp.PixelWidth, bmp.PixelHeight);
            }

            _isTempPreviewMode = isTempPreview;
            _isFinalOutputMode = isFinalOutput;

            UpdateCutOverlay();

            // ボタンの有効/無効は UpdateActionButtonsState が対象ごとに判定する
            // （開く・削除・コピー本体は保存画像や既定対象に連動、▽は常に有効）。
            UpdateActionButtonsState();

            UpdateToolTips();

            if (PreviewWatermarkBorder != null)
            {
                PreviewWatermarkBorder.Visibility = isTempPreview ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>ファイルを掴んだままにしないよう、メモリ上に読み切った BitmapImage を返す。</summary>
        private static BitmapImage LoadBitmap(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new Uri(filePath);
            bitmap.EndInit();
            return bitmap;
        }

        private void OpenImage(string path)
        {
            if (File.Exists(path)) {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            // _isTempPreviewMode ではなく、画像が存在しない時だけブロックする
            if (!HasLastCapture) return;
            OpenImage(_lastCapturedPath);
        }

        // スプリットボタン左「コピー」本体：詳細設定の既定対象でコピーする。
        private void BtnCopyClipboard_Click(object sender, RoutedEventArgs e)
        {
            var target = CopyTargetResolver.Parse(_settingsManager.Current.CopySource);
            CopyToClipboard(target, isAuto: false);
        }

        // スプリットボタン ▽ メニュー「一時プレビューをコピー」
        private void MenuCopyTempPreview_Click(object sender, RoutedEventArgs e)
            => CopyToClipboard(CopyTarget.TempPreview, isAuto: false);

        // スプリットボタン ▽ メニュー「最後に保存した画像をコピー」
        private void MenuCopyLastSaved_Click(object sender, RoutedEventArgs e)
            => CopyToClipboard(CopyTarget.LastSaved, isAuto: false);

        // スプリットボタン ▽ メニュー「最新のプレビューを取得してコピー」
        private void MenuCopyFreshPreview_Click(object sender, RoutedEventArgs e)
            => CopyToClipboard(CopyTarget.FreshPreview, isAuto: false);

        /// <summary>指定した対象の画像をクリップボードにコピーする。対象が無ければ警告ログを出す。</summary>
        private void CopyToClipboard(CopyTarget target, bool isAuto)
        {
            try
            {
                // FreshPreview はその場で一時プレビューを撮り直し、以降は TempPreview と同じ扱いにする。
                if (target == CopyTarget.FreshPreview)
                {
                    if (!CaptureTempPreview(verbose: true)) return; // 失敗理由は CaptureTempPreview がログ済み
                    target = CopyTarget.TempPreview;
                }

                string? path = CopyTargetResolver.Resolve(
                    target,
                    TempPreviewPath, File.Exists(TempPreviewPath),
                    _lastCapturedPath, HasLastCapture);

                if (path == null)
                {
                    Log(target == CopyTarget.TempPreview
                        ? LogMessages.ClipboardNoTempPreview
                        : LogMessages.ClipboardNoSavedImage);
                    return;
                }

                Clipboard.SetImage(LoadBitmap(path));
                Log(isAuto ? LogMessages.ClipboardCopiedAuto : LogMessages.ClipboardCopied);
            }
            catch (Exception ex) { Log(LogLevel.Error, LogMessages.ClipboardCopyFailed(ex.Message)); }
        }

        // スプリットボタン右「▽」：付属の ContextMenu をボタン位置に開く。
        private void BtnCopyDropdown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void BtnDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            if (!HasLastCapture) return;

            MessageBoxResult result = MessageBox.Show(
                "一番最後にキャプチャした画像ファイルをPCから完全に削除しますか？",
                "画像の削除確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes) {
                try {
                    string fileName = Path.GetFileName(_lastCapturedPath);
                    File.Delete(_lastCapturedPath);
                    Log(LogLevel.Success, LogMessages.FileDeleted(fileName));

                    ImgPreview.Source = null;
                    UpdateCutOverlay(forceHide: true);
                    if (PreviewWatermarkBorder != null) PreviewWatermarkBorder.Visibility = Visibility.Collapsed;

                    TxtDeleteMessage.Visibility = Visibility.Visible;

                    _lastCapturedPath = "";

                    // 削除後は保存画像が無くなるため、ボタンの有効/無効を再判定する
                    // （コピー本体は既定対象しだいで有効のまま残ることもある）。
                    UpdateActionButtonsState();
                    UpdateToolTips(); // 削除後にツールチップも更新
                }
                catch (Exception ex) {
                    Log(LogLevel.Error, LogMessages.FileDeleteFailed(ex.Message));
                }
            }
        }
    }
}